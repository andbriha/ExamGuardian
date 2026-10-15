from contextlib import closing
import hashlib
import json
import os
import secrets
import sqlite3
import time
from pathlib import Path
import jwt
from fastapi import APIRouter, File, UploadFile, Depends, Query, HTTPException
from fastapi.concurrency import run_in_threadpool
from api.auth import get_current_user
from api.deps import get_liveness_analyzer, get_identity_analyzer, get_audio_analyzer, verification_slot, audio_slot
from core.config import settings
from core.challenges import TASKS, new_sequence, advance
from models.speech_analyzer import WORDS, get_speech_analyzer, phrase_matches

router = APIRouter()
LIVENESS_TASKS = list(TASKS)
STATE_PATH = Path(os.environ.get("VERIFICATION_STATE_PATH", "state/verification.sqlite3"))
STATE_PATH.parent.mkdir(parents=True, exist_ok=True)
with closing(sqlite3.connect(STATE_PATH)) as db, db:
    db.execute("BEGIN IMMEDIATE")
    db.execute("CREATE TABLE IF NOT EXISTS challenges (id TEXT PRIMARY KEY, student TEXT, exam INTEGER, task TEXT, expires REAL, baseline_yaw REAL, baseline_pitch REAL, last_hash TEXT, target_since REAL, used INTEGER DEFAULT 0)")

    columns = {row[1] for row in db.execute("PRAGMA table_info(challenges)")}
    for name, definition in {"sequence": "TEXT", "step": "INTEGER DEFAULT 0", "motion": "TEXT DEFAULT '{}'",
                             "phrase": "TEXT", "speech_passed": "INTEGER DEFAULT 0",
                             "audio_buffer": "BLOB", "audio_at": "REAL DEFAULT 0"}.items():
        if name not in columns:
            db.execute(f"ALTER TABLE challenges ADD COLUMN {name} {definition}")
    db.execute("CREATE TABLE IF NOT EXISTS challenge_frames (challenge TEXT, hash TEXT, PRIMARY KEY(challenge, hash))")

@router.get("/verify/challenge")
async def get_challenge(exam_id: int = Query(..., gt=0), user_info: dict = Depends(get_current_user), client_protocol: int | None = None):
    if client_protocol not in (None, 2, 3):
        raise HTTPException(426, "Verification requires client protocol 2 or 3")
    sequence = new_sequence()
    phrase = None
    if client_protocol == 3 and (await run_in_threadpool(get_speech_analyzer)).available:
        # Keep both a pose and expression challenge, followed by speech.
        sequence = [secrets.choice(TASKS[:4]), secrets.choice(TASKS[4:]), "say_phrase"]
        phrase = " ".join(secrets.SystemRandom().sample(WORDS, 3))
    challenge_id, task = secrets.token_urlsafe(32), sequence[0]
    expires = time.time() + 240
    images=Path(os.environ.get("INFERENCE_IMAGES_PATH","images"))
    photo=images/(user_info["id"]+".jpg")
    references=images/"references"; references.mkdir(parents=True,exist_ok=True)
    if photo.is_file(): (references/(challenge_id+".jpg")).write_bytes(photo.read_bytes())
    with closing(sqlite3.connect(STATE_PATH)) as db, db:
        expired=db.execute("SELECT id FROM challenges WHERE expires < ?",(time.time()-300,)).fetchall()
        for (old_id,) in expired: (references/(old_id+".jpg")).unlink(missing_ok=True)
        db.execute("DELETE FROM challenges WHERE expires < ?", (time.time() - 300,))
        db.execute("DELETE FROM challenge_frames WHERE challenge NOT IN (SELECT id FROM challenges)")
        db.execute("INSERT INTO challenges (id,student,exam,task,expires,sequence) VALUES (?,?,?,?,?,?)", (challenge_id,user_info["id"],exam_id,task,expires,json.dumps(sequence)))
        db.execute("UPDATE challenges SET phrase=? WHERE id=?", (phrase, challenge_id))
    return {"challenge_id": challenge_id, "task": task, "expires_at": expires, "step": 1, "total_steps": len(sequence),
            "protocol_version": client_protocol or 2, "speech_recognition": phrase is not None}


@router.get("/verify/capabilities")
async def verification_capabilities():
    return {"protocol_version": 2, "tasks": list(TASKS), "total_steps": 3,
            "supported_protocols": [2, 3], "automatic_frames": True, "audio_activity": True,
            "pipeline_version": "fast-motion-1", "recommended_frame_interval_ms": 125,
            "speech_recognition": (await run_in_threadpool(get_speech_analyzer)).available}


@router.post("/verify/audio")
async def verification_audio(challenge_id: str, file: UploadFile = File(...),
                             user_info: dict = Depends(get_current_user), analyzer=Depends(get_audio_analyzer),
                             _slot=Depends(audio_slot)):
    def valid_challenge():
        with closing(sqlite3.connect(STATE_PATH)) as db:
            db.row_factory = sqlite3.Row
            row = db.execute("SELECT * FROM challenges WHERE id=?", (challenge_id,)).fetchone()
        if not row or row["student"] != user_info["id"] or row["expires"] <= time.time() or row["used"]:
            raise HTTPException(409, "Challenge expired, used or invalid")
        return row
    row = valid_challenge()
    # Up to two seconds, little-endian signed PCM16, mono, 16 kHz.
    data = await file.read(64001)
    if not 960 <= len(data) <= 64000 or len(data) % 2:
        raise HTTPException(400, "Expected 30ms to 2s PCM16 mono at 16000 Hz")
    result = await run_in_threadpool(analyzer.analyze, data, 16000)
    valid_challenge()
    if row["task"] == "say_phrase" and row["phrase"]:
        speech = get_speech_analyzer()
        if not speech.available:
            raise HTTPException(503, "Speech recognition unavailable; retry later")
        # Only collect audio after a live frontal anchor for this step.
        motion = json.loads(row["motion"] or "{}")
        if motion.get("live_until", 0) <= time.time() or "baseline" not in motion:
            return {"speech_detected": result["speech_detected"], "speech_seconds": result["speech_seconds"], "speech_ready": False}
        previous = bytes(row["audio_buffer"] or b"") if time.time()-row["audio_at"] < 4 else b""
        buffer = (previous + data)[-192000:]  # rolling six seconds, bridges chunk boundaries
        transcript = await run_in_threadpool(speech.transcribe, buffer)
        matched = phrase_matches(transcript["transcript"], row["phrase"]) and transcript["confidence"] >= .65
        with closing(sqlite3.connect(STATE_PATH)) as db, db:
            db.execute("BEGIN IMMEDIATE")
            current = valid_challenge()
            live = json.loads(current["motion"] or "{}")
            if (current["task"] != "say_phrase" or current["audio_at"] != row["audio_at"]
                    or live.get("live_until") != motion.get("live_until") or live.get("live_until", 0) <= time.time()
                    or "baseline" not in live):
                raise HTTPException(409, "Audio step changed")
            digest = "audio:" + hashlib.sha256(data).hexdigest()
            inserted = db.execute("INSERT OR IGNORE INTO challenge_frames VALUES (?,?)", (challenge_id, digest))
            if not inserted.rowcount:
                return {"speech_detected": result["speech_detected"], "speech_seconds": result["speech_seconds"], "speech_ready": True, "phrase_matched": bool(current["speech_passed"])}
            db.execute("UPDATE challenges SET audio_buffer=?,audio_at=?,speech_passed=MAX(speech_passed,?) WHERE id=?",
                       (buffer, time.time(), int(matched), challenge_id))
        return {**transcript, "speech_detected": result["speech_detected"], "speech_seconds": result["speech_seconds"],
                "speech_ready": True, "phrase_matched": matched or bool(row["speech_passed"])}
    # Microphone feedback is never identity proof and cannot grant admission.
    return {"speech_detected": result["speech_detected"], "speech_seconds": result["speech_seconds"]}

@router.post("/verify/identity-and-liveness")
async def verify_identity_and_liveness(task: str, challenge_id: str, current: UploadFile = File(...),
    user_info: dict = Depends(get_current_user), identity=Depends(get_identity_analyzer), liveness=Depends(get_liveness_analyzer),
    _slot=Depends(verification_slot)):
    started = time.perf_counter()
    with closing(sqlite3.connect(STATE_PATH)) as db, db:
        db.row_factory = sqlite3.Row
        row = db.execute("SELECT * FROM challenges WHERE id=?", (challenge_id,)).fetchone()
    if not row or row["student"] != user_info["id"] or row["task"] != task or row["expires"] < time.time() or row["used"]:
        raise HTTPException(409, "Challenge expired, used or invalid. Request a new challenge.")
    cur_bytes = await current.read(4 * 1024 * 1024 + 1)
    if len(cur_bytes) > 4 * 1024 * 1024: raise HTTPException(413, "Image too large")
    frame_hash = hashlib.sha256(cur_bytes).hexdigest()
    photo = Path(os.environ.get("INFERENCE_IMAGES_PATH", "images")) / "references" / (challenge_id + ".jpg")
    if not photo.is_file():
        return {"verified": False, "stage": "identity", "reason": "no_photo_on_server", "identity_confirmed": False}
    pose = await run_in_threadpool(liveness.analyze_continuous, cur_bytes)
    quality = pose.get("quality_reason") or (None if pose.get("has_face") else "no_face")
    # Identity and passive anti-spoofing bracket a short, continuous motion
    # window. Intermediate frames run only the fast landmark/quality pipeline.
    now = time.time()
    previous_motion = row["motion"] or "{}"
    state = json.loads(previous_motion)
    anchored = state.get("live_until", 0) > now and now-state.get("last_seen", now) <= 5
    if not anchored:
        state = {}
    neutral = (abs(pose.get("yaw", 1)) < .13 and pose.get("eyes_closed", 1) < .3
               and pose.get("mouth", 1) < .25 and pose.get("smile", 1) < .35)
    if quality:
        result = {"match": False, "reason": quality}
    elif not anchored and not neutral:
        result = {"match": False, "reason": "look_forward_first"}
    elif anchored:
        result = {"match": True, "cached_anchor": True}
    else:
        result = await run_in_threadpool(identity.compare, photo.read_bytes(), cur_bytes, check_spoof=True)
    response = {"verified": False, "stage": "quality" if quality else "identity",
                "reason": result.get("reason") or "face_mismatch", "id_distance": result.get("distance"),
                "identity_confirmed": result.get("match", False),
                "identity_checkpoint": not anchored and not quality and neutral}
    if response["reason"] == "look_forward_first": response["stage"] = "liveness"
    now = time.time()
    complete = False
    if result.get("match"):
        if not anchored:
            state = {"live_until": now+30}
        complete, reason = advance(state, task, pose, now)
        if task == "say_phrase" and "baseline" in state:
            complete = bool(row["speech_passed"] and neutral and result.get("match"))
            reason = "confirm_live_face" if row["speech_passed"] else "say_phrase"
        response.update(stage="liveness", reason=reason)
        if complete:
            response["identity_checkpoint"] = True
            final = await run_in_threadpool(identity.compare, photo.read_bytes(), cur_bytes, check_spoof=True)
            response["id_distance"] = final.get("distance")
            if not final.get("match"):
                complete = False
                response.update(reason="confirm_live_face", identity_confirmed=False)
                if final.get("reason") != "spoof_suspected":
                    # Preserve the completed movement while retrying a frontal
                    # checkpoint. Admission still requires a fresh live match.
                    state.setdefault("confirm_since", now)
                    if now - state["confirm_since"] > 8:
                        state = {}
                        response.update(stage="identity", reason=final.get("reason") or "face_mismatch")
    else:
        # Short blur/exposure fluctuations pause, never advance, a gesture.
        if anchored and "baseline" in state and quality in ("too_blurry", "too_dark", "too_bright"):
            state.setdefault("quality_since", now)
            if now - state["quality_since"] > 2:
                state = {}
            if state:
                response.update(stage="liveness", reason="quality_paused")
        else:
            state = {}
    if not quality:
        state.pop("quality_since", None)
    if complete and state.get("live_until",0) <= time.time():
        complete=False;state={}
        response.update(reason="look_forward_first",identity_confirmed=False)
    response["processing_ms"] = round((time.perf_counter()-started)*1000, 1)
    with closing(sqlite3.connect(STATE_PATH)) as db, db:
        db.row_factory = sqlite3.Row
        db.execute("BEGIN IMMEDIATE")
        # Reload after inference: another worker may have advanced/consumed this challenge.
        row = db.execute("SELECT * FROM challenges WHERE id=?", (challenge_id,)).fetchone()
        if not row or row["used"] or row["expires"] < time.time() or row["task"] != task or (row["motion"] or "{}") != previous_motion:
            raise HTTPException(409, "Challenge changed or expired. Request a new challenge.")
        inserted = db.execute("INSERT OR IGNORE INTO challenge_frames VALUES (?,?)", (challenge_id, frame_hash))
        if not inserted.rowcount:
            response.update(stage="liveness", reason="fresh_frame_required")
            return response
        sequence = json.loads(row["sequence"]) if row["sequence"] else [task]
        response.update(step=row["step"]+1, total_steps=len(sequence))
        db.execute("UPDATE challenges SET motion=? WHERE id=?", (json.dumps(state), challenge_id))
        if not state:
            db.execute("UPDATE challenges SET speech_passed=0,audio_buffer=NULL WHERE id=?", (challenge_id,))
        if not complete:
            return response
        step = row["step"] + 1
        if step < len(sequence):
            db.execute("UPDATE challenges SET step=?,task=?,motion='{}' WHERE id=?", (step,sequence[step],challenge_id))
            response.update(reason="next_action", next_task=sequence[step], step=step+1,
                            phrase=row["phrase"] if sequence[step] == "say_phrase" else None)
            return response
        db.execute("UPDATE challenges SET used=1,audio_buffer=NULL WHERE id=? AND used=0", (challenge_id,))
    key = os.environ.get("JWT__KEY") or settings.jwt_secret_key
    issuer = os.environ.get("JWT__ISSUER") or settings.jwt_issuer
    admission = jwt.encode({"sub": user_info["id"], "exam_id": str(row["exam"]), "purpose": "admission", "jti": challenge_id,
        "iss": issuer, "aud": "ExamGuardianAdmission", "iat": int(now), "exp": int(now)+120}, key, algorithm="HS256")
    return {**response,"verified": True,"stage": "ok","reason": "Passed","admission_token": admission}
