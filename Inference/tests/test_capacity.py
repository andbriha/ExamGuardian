import asyncio
import unittest
from fastapi import HTTPException
from core.capacity import CapacityGate


class Capacity(unittest.IsolatedAsyncioTestCase):
    async def test_second_student_waits_and_succeeds(self):
        gate = CapacityGate('test', waiting=2, timeout=1)
        order = []
        async def second():
            async with gate.slot(): order.append('second')
        async with gate.slot():
            task = asyncio.create_task(second())
            await asyncio.sleep(.02)
            self.assertEqual(gate.pending, 1)
            self.assertEqual(order, [])
        await task
        self.assertEqual(order, ['second'])
        self.assertEqual(gate.active, 0)

    async def test_queue_is_bounded_and_cancel_does_not_leak(self):
        gate = CapacityGate('test', waiting=1, timeout=1)
        async def waiter():
            async with gate.slot(): pass
        async with gate.slot():
            task = asyncio.create_task(waiter())
            await asyncio.sleep(.02)
            with self.assertRaises(HTTPException) as caught: await waiter()
            self.assertEqual(caught.exception.status_code, 429)
            self.assertEqual(caught.exception.headers['Retry-After'], '1')
            task.cancel()
            with self.assertRaises(asyncio.CancelledError): await task
            self.assertEqual(gate.pending, 0)
        await waiter()
        self.assertEqual(gate.active, 0)

    async def test_timeout_releases_waiter_and_model_exception_releases_slot(self):
        gate = CapacityGate('test', timeout=.01)
        async with gate.slot():
            with self.assertRaises(HTTPException):
                async with gate.slot(): self.fail('must not enter')
        with self.assertRaises(ValueError):
            async with gate.slot(): raise ValueError('model failure')
        async with gate.slot(): self.assertEqual(gate.active, 1)
        self.assertEqual(gate.pending, 0)
