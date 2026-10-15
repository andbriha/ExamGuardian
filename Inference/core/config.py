from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    core_api_url:    str   = "http://api:8080/api/violations"
    jwt_secret_key:  str   = ""
    jwt_issuer: str = ""
    jwt_audience: str = ""
    identity_threshold: float = 0.40
    yaw_threshold:   float = 0.19
    pitch_threshold: float = 0.17
    yolo_confidence: float = 0.50
    vad_aggressiveness:    int   = 2
    vad_threshold_seconds: float = 3.0
    ai_api_key: str = ""

    class Config:
        env_file = ".env"
        extra = "allow"

settings = Settings()