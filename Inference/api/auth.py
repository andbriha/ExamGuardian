import os
from uuid import UUID
import jwt
import httpx
from fastapi import HTTPException, Security
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from core.config import settings

security = HTTPBearer(auto_error=False)

def get_current_user(creds: HTTPAuthorizationCredentials | None = Security(security)) -> dict:
    if not creds:
        raise HTTPException(401, "Authentication required")
    key = os.environ.get("JWT__KEY") or settings.jwt_secret_key
    issuer = os.environ.get("JWT__ISSUER") or settings.jwt_issuer
    audience = os.environ.get("JWT__AUDIENCE") or settings.jwt_audience
    if not key or not issuer or not audience:
        raise HTTPException(503, "Authentication is not configured")
    try:
        payload = jwt.decode(creds.credentials, key, algorithms=["HS256"], issuer=issuer, audience=audience,
                             options={"require": ["exp", "sub", "iss", "aud"]})
        user_id = str(UUID(payload["sub"]))
        role = payload.get("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") or payload.get("role")
        if role != "Student":
            raise HTTPException(403, "Student access required")
        return {"id": user_id, "login": payload.get("login", "")}
    except (jwt.InvalidTokenError, ValueError, KeyError):
        raise HTTPException(401, "Invalid or expired token")

async def require_active_session(user: dict, session_id: int, session_token: str) -> dict:
    if session_id <= 0:
        raise HTTPException(400, "A session is required")
    url = settings.core_api_url.rsplit("/violations", 1)[0] + f"/internal/sessions/{session_id}"
    try:
        async with httpx.AsyncClient(timeout=5) as client:
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
        async with httpx.AsyncClient(timeout=5) as client:
            response = await client.post(url,json={"studentId": user["id"], "channel": channel},headers={"X-Api-Key": settings.ai_api_key})
            if response.status_code != 204: raise HTTPException(503,"Analysis receipt not acknowledged")
    except httpx.HTTPError:
        raise HTTPException(503,"Analysis receipt unavailable")
