# core/notifier.py
import httpx
import logging
import json
from datetime import datetime, timezone
from core.config import settings

logger = logging.getLogger(__name__)

async def notify_violation(
    student_id:   int,
    session_id:   int,
    violation_type: str,
    confidence:   float = 1.0,
    relative_ts:  int   = 0,
    evidence_url: str | None = None,
    metadata:     dict | None = None,
) -> None:
    target_url = settings.core_api_url
    if not target_url.endswith("/violations"):
        target_url = f"{target_url.rstrip('/')}/violations"

    async with httpx.AsyncClient(timeout=10.0) as client:
        try:
            response = await client.post(
                target_url,
                json={
                    "examSessionId":           session_id,
                    "violationType":           violation_type,
                    "confidence":              confidence,
                    "relativeTimestampSeconds": relative_ts,
                    "evidenceUrl":             evidence_url,
                    "metadata": json.dumps(metadata) if metadata else None,
                },
                headers={"X-Api-Key": settings.ai_api_key},
            )
            if response.status_code >= 400:
                logger.warning("[Notifier] Core API returned status %d: %s", response.status_code, response.text)
            else:
                logger.info("[Notifier] Sent violation %s for session %d", violation_type, session_id)
        except Exception as e:
            logger.error("[Notifier] Failed to notify Core API: %s", e)