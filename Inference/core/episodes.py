"""Capture-time episodes. A missing sample is unknown, never positive evidence."""
import json
import time
from uuid import uuid4
from core.outbox import connect

VIDEO_TYPES = frozenset({"face_missing", "multiple_faces", "extra_person", "looking_down",
    "looking_away_left", "looking_away_right", "cell_phone", "book", "laptop", "analysis_unavailable"})


def advance(state, candidates, evaluated, capture_time, unknown=()):
    """Pure reducer; emits initial, periodic and closing updates with one event id."""
    updates = []
    watermarks = state.setdefault('_watermarks', {})
    for kind in set(evaluated) | set(unknown):
        if capture_time < watermarks.get(kind, -1) or (capture_time == watermarks.get(kind, -1) and kind not in unknown):
            continue
        watermarks[kind] = capture_time
        old = state.get(kind)
        if kind in unknown:
            if old and old.get('sent'):
                updates.append(event(kind, old, 'coverage_gap'))
            state.pop(kind, None)
            continue
        candidate = candidates.get(kind)
        max_gap = 25 if kind == "identity_uncertain" else (11 if kind == 'voice_activity' else 6)
        if old and (candidate is None or capture_time - old["seen"] > max_gap):
            if old.get("sent"):
                updates.append(event(kind, old, "coverage_gap" if capture_time - old["seen"] > max_gap else "ended"))
            state.pop(kind, None)
            old = None
        if candidate is None:
            continue
        if old is None:
            old = state[kind] = {"id": str(uuid4()), "start": capture_time, "seen": capture_time,
                                 "count": 0, "sent": False, "revision": 0}
        old.update(seen=capture_time, count=old["count"] + 1, candidate=candidate)
        duration = capture_time - old["start"]
        minimum = 0 if kind == 'voice_activity' else (10 if kind == "analysis_unavailable" else 2)
        samples = 1 if kind == 'voice_activity' else (2 if kind == "identity_uncertain" else 3)
        if old["count"] >= samples and duration >= minimum:
            if not old["sent"] or capture_time - old["published"] >= 15:
                old["sent"] = True
                old["published"] = capture_time
                updates.append(event(kind, old, "observed"))
    return updates


def event(kind, state, status):
    state["revision"] += 1
    return {**state["candidate"], "type": kind, "event_id": state["id"],
            "relative_ts": int(state["start"]),
            "metadata": {**state["candidate"].get("metadata", {}), "review_required": True,
                "episode": {"revision": state["revision"], "start": state["start"],
                            "last_observed": state["seen"], "samples": state["count"], "status": status}}}


def observe(session, observations, capture_time, evaluated=VIDEO_TYPES, unknown=(), evidence_writer=None):
    # Separate domain keys prevent video from erasing audio/identity histories.
    with connect() as db:
        db.execute("CREATE TABLE IF NOT EXISTS episodes (session INTEGER PRIMARY KEY, state TEXT, touched REAL)")
        db.execute("BEGIN IMMEDIATE")
        row = db.execute("SELECT state FROM episodes WHERE session=?", (session,)).fetchone()
        state = json.loads(row[0]) if row else {}
        candidates = {}
        for value in observations:
            kind = value['type']
            if kind not in candidates or value.get('confidence', 0) > candidates[kind].get('confidence', 0):
                candidates[kind] = value
        updates = advance(state, candidates, evaluated, capture_time, unknown)
        # Episode state and its latest delivery payload commit together. A crash
        # cannot mark an episode published without putting it in the outbox.
        for update in updates:
            url = evidence_writer(update) if evidence_writer else None
            payload = {"eventId": update['event_id'], "examSessionId": session,
                       "violationType": update['type'], "confidence": update.get('confidence', 0),
                       "relativeTimestampSeconds": int(update['metadata'].get('window_start', update['relative_ts'])),
                       "evidenceUrl": url, "metadata": json.dumps(update['metadata'])}
            db.execute("INSERT INTO outbox(id,payload) VALUES (?,?) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload, attempts=0, next_try=0, error=NULL, dead=0", (update['event_id'], json.dumps(payload)))
        db.execute("INSERT OR REPLACE INTO episodes VALUES (?,?,?)", (session, json.dumps(state), time.time()))
        db.execute("DELETE FROM episodes WHERE touched<?", (time.time()-86400,))
        return updates


def face_observations(face):
    reason = face.get('quality_reason')
    if reason and reason not in ('no_face', 'multiple_faces'):
        return [{"type": "analysis_unavailable", "confidence": 0.0,
                 "metadata": {"reason": reason, "desc": "Якість кадру недостатня для перевірки обличчя. Це технічний стан, не порушення."}}]
    return face.get('violations', [])
