from functools import lru_cache
from models.liveness_analyzer import LivenessAnalyzer
from models.identity_analyzer  import IdentityAnalyzer
from models.object_detector    import ObjectDetector
from models.audio_analyzer     import AudioAnalyzer
from core.config import settings

@lru_cache
def get_liveness_analyzer() -> LivenessAnalyzer:
    return LivenessAnalyzer(yaw_threshold=settings.yaw_threshold)

@lru_cache
def get_identity_analyzer() -> IdentityAnalyzer:
    return IdentityAnalyzer(threshold=0.65)

@lru_cache
def get_object_detector() -> ObjectDetector:
    return ObjectDetector(confidence=settings.yolo_confidence)

@lru_cache
def get_audio_analyzer() -> AudioAnalyzer:
    return AudioAnalyzer(
        aggressiveness=settings.vad_aggressiveness,
        threshold_seconds=settings.vad_threshold_seconds,
    )