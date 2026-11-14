import unittest
from unittest.mock import AsyncMock, Mock, patch
import httpx
from fastapi import HTTPException
from api.auth import require_active_session


class SessionAuthority(unittest.IsolatedAsyncioTestCase):
    async def test_core_checks_capability_and_revocation(self):
        client=AsyncMock()
        with patch('api.auth.core_client', new=AsyncMock(return_value=client)):
            client.get.return_value=Mock(status_code=200,json=lambda:{'id':12})
            self.assertEqual(await require_active_session({'id':'student'},12,'capability'),{'id':12})
            self.assertEqual(client.get.call_args.kwargs['headers']['X-Session-Token'],'capability')
            for status,expected in [(403,403),(404,403),(500,503)]:
                client.get.return_value=Mock(status_code=status)
                with self.assertRaises(HTTPException) as error: await require_active_session({'id':'student'},12,'capability')
                self.assertEqual(error.exception.status_code,expected)
            client.get.side_effect=httpx.ConnectError('offline')
            with self.assertRaises(HTTPException) as error: await require_active_session({'id':'student'},12,'capability')
            self.assertEqual(error.exception.status_code,503)
