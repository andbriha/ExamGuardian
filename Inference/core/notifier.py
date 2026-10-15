import json
from uuid import uuid4
from core.outbox import enqueue

async def notify_violation(student_id: int, session_id: int, violation_type: str, confidence: float = 1.0,
    relative_ts: int = 0, evidence_url: str | None = None, metadata: dict | None = None, event_id: str | None = None):
    event_id = event_id or str(uuid4())
    enqueue(event_id, {"eventId": event_id, "examSessionId": session_id, "violationType": violation_type,
        "confidence": confidence, "relativeTimestampSeconds": relative_ts, "evidenceUrl": evidence_url,
        "metadata": json.dumps(metadata) if metadata else None})
