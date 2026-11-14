"""Real JWKS and revocation round-trip against run-core-contracts.ps1's disposable API."""
import os
from pathlib import Path
import sys
import unittest
from uuid import uuid4
from test_core_contract import URL, KEY, request

if URL != 'http://127.0.0.1:55111' or not KEY:
    raise RuntimeError('Only the disposable contract API is supported')
os.environ.update(
    CORE_API_URL=URL + '/api/violations',
    JWT_JWKS_URL=URL + '/.well-known/jwks.json',
    JWT__ISSUER='ExamGuardianTest', JWT__AUDIENCE='ExamGuardianTestClient',
    AI_API_KEY='examguardian-integration-test-ai-key',
)
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'Inference'))
from api.auth import get_current_user
from fastapi import HTTPException
from fastapi.security import HTTPAuthorizationCredentials


class InferenceCoreAuth(unittest.TestCase):
    def test_real_public_key_and_immediate_logout_revocation(self):
        status, body = request('/api/Auth/login', 'POST', {
            'login': 'admin', 'password': os.environ['EXAMGUARDIAN_TEST_PASSWORD']})
        self.assertEqual(status, 200, body)
        admin = body['response']['accessToken']
        login = 'jwks_' + uuid4().hex
        password = 'Disposable-JWKS-Password-2026!'
        status, student = request('/api/Students', 'POST', {
            'fullName': 'JWKS Contract', 'login': login, 'password': password, 'group': 'JWKS'}, admin)
        self.assertEqual(status, 201, student)
        status, body = request('/api/Auth/login', 'POST', {'login': login, 'password': password})
        self.assertEqual(status, 200, body)
        token = body['response']['accessToken']
        credentials = HTTPAuthorizationCredentials(scheme='Bearer', credentials=token)
        self.assertEqual(get_current_user(credentials)['id'], student['id'])
        self.assertEqual(request('/api/Auth/logout', 'POST', {}, token)[0], 204)
        with self.assertRaises(HTTPException) as rejected:
            get_current_user(credentials)
        self.assertEqual(rejected.exception.status_code, 401)


if __name__ == '__main__':
    unittest.main(verbosity=2)
