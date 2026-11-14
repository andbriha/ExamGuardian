import sqlite3
import tempfile
import unittest
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from fastapi import HTTPException
from core.verification_store import state_connection


class VerificationStore(unittest.TestCase):
    def test_locked_writer_returns_retry_and_rolls_back(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'state.db'
            with state_connection(path) as db:
                db.execute('CREATE TABLE counter(value INTEGER)')
                db.execute('INSERT INTO counter VALUES (0)')
            with state_connection(path) as writer:
                writer.execute('BEGIN IMMEDIATE')
                writer.execute('UPDATE counter SET value=1')
                with self.assertRaises(HTTPException) as caught:
                    with state_connection(path, timeout=.01) as other:
                        other.execute('BEGIN IMMEDIATE')
                self.assertEqual(caught.exception.status_code, 503)
                self.assertEqual(caught.exception.headers['Retry-After'], '1')
            with self.assertRaises(ValueError):
                with state_connection(path) as db:
                    db.execute('UPDATE counter SET value=99')
                    raise ValueError('abort')
            with state_connection(path) as db:
                self.assertEqual(db.execute('SELECT value FROM counter').fetchone()[0],1)

    def test_parallel_atomic_updates_do_not_lose_progress(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'state.db'
            with state_connection(path) as db:
                db.execute('CREATE TABLE counter(value INTEGER)')
                db.execute('INSERT INTO counter VALUES (0)')
            def update(_):
                with state_connection(path, timeout=2) as db:
                    db.execute('BEGIN IMMEDIATE')
                    value=db.execute('SELECT value FROM counter').fetchone()[0]
                    db.execute('UPDATE counter SET value=?',(value+1,))
            with ThreadPoolExecutor(max_workers=4) as pool: list(pool.map(update,range(20)))
            with state_connection(path) as db:
                self.assertEqual(db.execute('SELECT value FROM counter').fetchone()[0],20)
