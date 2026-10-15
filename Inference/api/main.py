import os
os.environ.setdefault("OPENCV_IO_MAX_IMAGE_PIXELS", "16000000")
from core.model_integrity import verify_weights
import cv2
import numpy as np
import asyncio
from contextlib import suppress
from core.outbox import delivery_loop, status
import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from api.routes import frame, audio, verify_session
from api.deps   import (get_liveness_analyzer, get_identity_analyzer,
                         get_object_detector, get_audio_analyzer)

logging.basicConfig(level=logging.INFO)

@asynccontextmanager
async def lifespan(app: FastAPI):
    verify_weights()
    # Прогрів усіх моделей до першого запиту
    liveness = get_liveness_analyzer()
    get_identity_analyzer()
    detector = get_object_detector()
    get_audio_analyzer()
    warmup = cv2.imencode(".jpg",np.full((480,640,3),127,dtype=np.uint8))[1].tobytes()
    for _ in range(3):
        liveness.analyze_continuous(warmup)
        detector.detect(warmup)
    app.state.model_hashes=verify_weights(require_all=True)
    worker = asyncio.create_task(delivery_loop())
    try:
        yield
    finally:
        worker.cancel()
        with suppress(asyncio.CancelledError):
            await worker
        liveness._detector.close()

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
@app.get("/health/ready")
async def ready():
    delivery = status()
    return {"ready": True, "delivery": delivery, "degraded": delivery["failed"]>0, "models": app.state.model_hashes}
