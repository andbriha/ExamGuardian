"""Bounded admission queues; model concurrency is deliberately hardware-limited."""
import asyncio
from contextlib import asynccontextmanager
from fastapi import HTTPException


class CapacityGate:
    def __init__(self, name, waiting=16, timeout=5.0):
        self.name, self.waiting_limit, self.timeout = name, waiting, timeout
        self.semaphore = asyncio.Semaphore(1)
        self.pending = self.active = self.rejected = 0

    def snapshot(self):
        return dict(active=self.active, waiting=self.pending, rejected=self.rejected,
                    waiting_limit=self.waiting_limit, wait_timeout_seconds=self.timeout)

    def overloaded(self):
        self.rejected += 1
        return HTTPException(429, f"{self.name} capacity reached. Retry shortly.", headers={"Retry-After": "1"})

    @asynccontextmanager
    async def slot(self):
        if self.pending >= self.waiting_limit:
            raise self.overloaded()
        self.pending += 1
        try:
            await asyncio.wait_for(self.semaphore.acquire(), timeout=self.timeout)
        except asyncio.TimeoutError:
            raise self.overloaded()
        finally:
            self.pending -= 1
        self.active += 1
        try:
            yield
        finally:
            self.active -= 1
            self.semaphore.release()
