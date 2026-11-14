"""Bounded per-attempt PCM window. Never splice audio across capture gaps."""
import time
from core.outbox import connect


def append_audio(session, pcm, rate, start):
    end = start + len(pcm) / (rate * 2)
    with connect() as db:
        db.execute("CREATE TABLE IF NOT EXISTS audio_windows (session INTEGER PRIMARY KEY, pcm BLOB, rate INTEGER, end REAL, touched REAL)")
        db.execute("BEGIN IMMEDIATE")
        db.execute("DELETE FROM audio_windows WHERE touched<?", (time.time()-60,))
        row = db.execute("SELECT pcm,rate,end FROM audio_windows WHERE session=?", (session,)).fetchone()
        if row and start < row[2] - .001:
            return None  # replay/overlap must not count speech twice
        continuous = row is not None and row[1] == rate and abs(start-row[2]) < .001
        window = ((row[0] if continuous else b'') + pcm)[-rate*2*10:]
        db.execute("INSERT OR REPLACE INTO audio_windows VALUES (?,?,?,?,?)", (session,window,rate,end,time.time()))
        return window, end-len(window)/(rate*2), end, continuous
