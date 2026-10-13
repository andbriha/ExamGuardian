import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from api.routes import frame,  audio, verify_session
from api.deps   import (get_liveness_analyzer, get_identity_analyzer,
                         get_object_detector, get_audio_analyzer)

logging.basicConfig(level=logging.INFO)

@asynccontextmanager
async def lifespan(app: FastAPI):
    # Прогрів усіх моделей до першого запиту
    get_liveness_analyzer()
    get_identity_analyzer()
    get_object_detector()
    get_audio_analyzer()
    yield

app = FastAPI(
    title       = "Inference API",
    description = "Мікросервіс інтелектуального аналізу системи прокторингу",
    version     = "2.0.0",
    lifespan    = lifespan,
)

app.include_router(frame.router)

app.include_router(audio.router)
app.include_router(verify_session.router)

@app.get("/")
async def root():
    return {"status": "running", "version": "2.0.0"}