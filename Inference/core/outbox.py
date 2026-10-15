"""Durable, idempotent delivery of observations. Failed deliveries stay visible."""
from contextlib import contextmanager
import asyncio
import json
import logging
import os
import sqlite3
import time
from pathlib import Path
import httpx
from core.config import settings

logger = logging.getLogger(__name__)
STATE_PATH = Path(os.environ.get("OBSERVATION_STATE_PATH", "state/observations.sqlite3"))
STATE_PATH.parent.mkdir(parents=True, exist_ok=True)

@contextmanager
def connect():
    db = sqlite3.connect(STATE_PATH, timeout=10)
    db.execute("CREATE TABLE IF NOT EXISTS outbox (id TEXT PRIMARY KEY, payload TEXT, attempts INTEGER DEFAULT 0, next_try REAL DEFAULT 0, error TEXT, dead INTEGER DEFAULT 0)")
    db.execute("CREATE TABLE IF NOT EXISTS streaks (session INTEGER, kind TEXT, count INTEGER, last_seen REAL, last_sent REAL, PRIMARY KEY(session,kind))")
    try:
        with db:
            yield db
    finally:
        db.close()

def enqueue(event_id: str, payload: dict):
    with connect() as db:
        db.execute("INSERT OR IGNORE INTO outbox(id,payload) VALUES (?,?)", (event_id, json.dumps(payload)))

def should_emit(session: int, kind: str) -> bool:
    now = time.time()
    with connect() as db:
        db.execute("BEGIN IMMEDIATE")
        row = db.execute("SELECT count,last_seen,last_sent FROM streaks WHERE session=? AND kind=?", (session,kind)).fetchone()
        count = row[0]+1 if row and now-row[1] < 3 else 1
        last_sent = row[2] if row else 0
        ready = count >= (3 if kind.startswith("looking_") or kind == "face_missing" else 1) and now-last_sent > 15
        db.execute("INSERT OR REPLACE INTO streaks VALUES (?,?,?,?,?)", (session,kind,count,now,now if ready else last_sent))
        db.execute("DELETE FROM streaks WHERE last_seen < ?", (now-3600,))
    return ready

async def delivery_loop():
    async with httpx.AsyncClient(timeout=10) as client:
        while True:
            with connect() as db:
                pending = db.execute("SELECT id,payload,attempts FROM outbox WHERE dead=0 AND next_try<=? ORDER BY next_try LIMIT 20", (time.time(),)).fetchall()
            for event_id, payload, attempts in pending:
                error, dead = None, False
                try:
                    response = await client.post(settings.core_api_url, json=json.loads(payload), headers={"X-Api-Key": settings.ai_api_key})
                    if response.status_code < 300:
                        with connect() as db: db.execute("DELETE FROM outbox WHERE id=?", (event_id,))
                        continue
                    error = f"HTTP {response.status_code}"
                    dead = response.status_code in (400, 401, 403, 404, 422)
                except httpx.HTTPError as exc:
                    error = type(exc).__name__
                with connect() as db:
                    db.execute("UPDATE outbox SET attempts=?,next_try=?,error=?,dead=? WHERE id=?", (attempts+1,time.time()+min(60,2**min(attempts,6)),error,int(dead),event_id))
                logger.warning("Observation %s delivery failed: %s", event_id, error)
            await asyncio.sleep(1)

def status():
    with connect() as db:
        pending = db.execute("SELECT COUNT(*) FROM outbox WHERE dead=0").fetchone()[0]
        failed = db.execute("SELECT COUNT(*) FROM outbox WHERE dead=1").fetchone()[0]
    return {"pending": pending, "failed": failed}
