from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    core_api_url:    str   = "http://api:8080/api/violations"
    jwt_secret_key:  str   = "super_secret_exam_guardian_jwt_signing_key_2026_x99"
    yaw_threshold:   float = 0.03
    yolo_confidence: float = 0.50
    vad_aggressiveness:    int   = 2
    vad_threshold_seconds: float = 3.0
    ai_api_key: str = "exam_guard_secret_ai_token_abc123"

    class Config:
        env_file = ".env"
        extra = "allow"

settings = Settings()