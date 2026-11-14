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
# These shared model instances are not independent inference workers. Increasing
# semaphore counts alone can race model state or multiply GPU memory use.
from core.capacity import CapacityGate
_gates = {name: CapacityGate(name, settings.inference_queue_limit, settings.inference_wait_seconds)
          for name in ("analysis", "verification", "audio")}


def capacity_status():
    return {name: gate.snapshot() for name, gate in _gates.items()}


async def verification_slot():
    async with _gates["verification"].slot():
        yield


async def audio_slot():
    async with _gates["audio"].slot():
        yield


async def analysis_slot():
    async with _gates["analysis"].slot():
        yield
