import os
from functools import lru_cache
from uuid import UUID
import jwt
import httpx
from fastapi import HTTPException, Security
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from core.config import settings

security = HTTPBearer(auto_error=False)
_core_sync_client = httpx.Client(timeout=5)
_core_async_client = None

@lru_cache(maxsize=1)
def access_key_client():
    url = os.environ.get("JWT_JWKS_URL", "http://api:8080/.well-known/jwks.json")
    return jwt.PyJWKClient(url, cache_keys=True, lifespan=60, timeout=5)

def access_verification_key(token):
    # URL is deployment-owned, never taken from untrusted jku/x5u headers.
    return access_key_client().get_signing_key_from_jwt(token).key

def require_login_session(user_id, session_id):
    url = settings.core_api_url.rsplit("/violations", 1)[0] + f"/internal/auth-sessions/{session_id}"
    try:
        response = _core_sync_client.get(url, params={"studentId": user_id}, headers={"X-Api-Key": settings.ai_api_key})
    except httpx.HTTPError:
        raise HTTPException(503, "Authentication authority unavailable")
    if response.status_code == 401: raise HTTPException(401, "Session revoked")
    if response.status_code != 204: raise HTTPException(503, "Authentication authority unavailable")

async def core_client():
    global _core_async_client
    if _core_async_client is None: _core_async_client = httpx.AsyncClient(timeout=5)
    return _core_async_client

async def close_auth_clients():
    global _core_async_client
    _core_sync_client.close()
    if _core_async_client is not None: await _core_async_client.aclose()
    _core_async_client = None

def get_current_user(creds: HTTPAuthorizationCredentials | None = Security(security)) -> dict:
    if not creds:
        raise HTTPException(401, "Authentication required")
    issuer = os.environ.get("JWT__ISSUER") or settings.jwt_issuer
    audience = os.environ.get("JWT__AUDIENCE") or settings.jwt_audience
    if not issuer or not audience:
        raise HTTPException(503, "Authentication is not configured")
    try:
        if jwt.get_unverified_header(creds.credentials).get("alg") != "RS256": raise jwt.InvalidAlgorithmError("Unsupported algorithm")
        payload = jwt.decode(creds.credentials, access_verification_key(creds.credentials), algorithms=["RS256"], issuer=issuer, audience=audience,
                             options={"require": ["exp", "sub", "iss", "aud", "sid"]})
        user_id = str(UUID(payload["sub"]))
        role = payload.get("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") or payload.get("role")
        if role != "Student":
            raise HTTPException(403, "Student access required")
        require_login_session(user_id, str(UUID(payload["sid"])))
        return {"id": user_id, "login": payload.get("login", "")}
    except jwt.PyJWKClientConnectionError:
        raise HTTPException(503, "Signing keys unavailable")
    except (jwt.PyJWKClientError, jwt.InvalidTokenError, ValueError, KeyError):
        raise HTTPException(401, "Invalid or expired token")

async def require_active_session(user: dict, session_id: int, session_token: str) -> dict:
    if session_id <= 0:
        raise HTTPException(400, "A session is required")
    url = settings.core_api_url.rsplit("/violations", 1)[0] + f"/internal/sessions/{session_id}"
    try:
        client = await core_client()
        response = await client.get(url, params={"studentId": user["id"]}, headers={"X-Api-Key": settings.ai_api_key,"X-Session-Token":session_token})
    except httpx.HTTPError:
        raise HTTPException(503, "Session validation unavailable")
    if response.status_code in (403,404):
        raise HTTPException(403, "Session is not active or does not belong to you")
    if response.status_code != 200:
        raise HTTPException(503, "Session validation unavailable")
    return response.json()

async def acknowledge_analysis(user: dict, session_id: int, channel: str):
    url = settings.core_api_url.rsplit("/violations",1)[0] + f"/internal/sessions/{session_id}/observed"
    try:
        client = await core_client()
        response = await client.post(url,json={"studentId": user["id"], "channel": channel},headers={"X-Api-Key": settings.ai_api_key})
        if response.status_code != 204: raise HTTPException(503,"Analysis receipt not acknowledged")
    except httpx.HTTPError:
        raise HTTPException(503,"Analysis receipt unavailable")
