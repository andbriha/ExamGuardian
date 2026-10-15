import asyncio
from fastapi import HTTPException
from functools import lru_cache
from models.liveness_analyzer import LivenessAnalyzer
from models.identity_analyzer  import IdentityAnalyzer
from models.object_detector    import ObjectDetector
from models.audio_analyzer     import AudioAnalyzer
from core.config import settings

@lru_cache
def get_liveness_analyzer() -> LivenessAnalyzer:
    return LivenessAnalyzer(yaw_threshold=settings.yaw_threshold, pitch_threshold=settings.pitch_threshold)

@lru_cache
def get_identity_analyzer() -> IdentityAnalyzer:
    return IdentityAnalyzer(threshold=settings.identity_threshold)

@lru_cache
def get_object_detector() -> ObjectDetector:
    return ObjectDetector(confidence=settings.yolo_confidence)

@lru_cache
def get_audio_analyzer() -> AudioAnalyzer:
    return AudioAnalyzer(
        aggressiveness=settings.vad_aggressiveness,
        threshold_seconds=settings.vad_threshold_seconds,
    )
_analysis_lock = asyncio.Semaphore(1)
_verification_lock = asyncio.Semaphore(1)
_audio_lock = asyncio.Semaphore(1)

async def verification_slot():
    # Continuous YOLO analysis must not stall active gesture sampling.
    try:
        await asyncio.wait_for(_verification_lock.acquire(), timeout=5)
    except asyncio.TimeoutError:
        raise HTTPException(429, "Verification capacity reached")
    try:
        yield
    finally:
        _verification_lock.release()

async def audio_slot():
    # ASR and VAD have their own bounded queue, independent of camera sampling.
    try:
        await asyncio.wait_for(_audio_lock.acquire(), timeout=5)
    except asyncio.TimeoutError:
        raise HTTPException(429, "Audio capacity reached")
    try:
        yield
    finally:
        _audio_lock.release()

async def analysis_slot():
    try:
        await asyncio.wait_for(_analysis_lock.acquire(), timeout=5)
    except asyncio.TimeoutError:
        raise HTTPException(429, "Analysis capacity reached. Retry shortly.")
    try:
        yield
    finally:
        _analysis_lock.release()
