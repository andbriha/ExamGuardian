"""Short, atomic challenge transactions with explicit overload responses.

Keep a connection local to its caller; check_same_thread=False is unnecessary.
Do not enable WAL blindly: the deployed SQLite must first include the WAL-reset
fix (3.51.3+, or a vendor backport). No inference runs inside these transactions.
"""
from contextlib import contextmanager
import sqlite3
from fastapi import HTTPException


@contextmanager
def state_connection(path, timeout=.05):
    db = sqlite3.connect(path, timeout=timeout)
    try:
        with db:
            yield db
    except sqlite3.OperationalError as error:
        code = getattr(error, "sqlite_errorcode", 0) & 0xff
        if code in (sqlite3.SQLITE_BUSY, sqlite3.SQLITE_LOCKED):
            raise HTTPException(503, "Verification state busy. Retry the current step.",
                                headers={"Retry-After": "1"}) from error
        raise
    finally:
        db.close()
