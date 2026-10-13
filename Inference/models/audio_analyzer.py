# models/audio_analyzer.py
import webrtcvad
import numpy as np
import logging

logger = logging.getLogger(__name__)

class AudioAnalyzer:
    """Детектує голосову активність у аудіофрагменті.
    Якщо студент говорить довше threshold_seconds — потенційна підказка."""

    def __init__(self, aggressiveness: int = 2, threshold_seconds: float = 3.0):
        self._vad = webrtcvad.Vad(aggressiveness)  # 0-3, більше = суворіше
        self.threshold_seconds = threshold_seconds

    def analyze(self, pcm_bytes: bytes, sample_rate: int = 16000) -> dict:
        """pcm_bytes — сирі PCM байти, 16kHz, 16bit, mono"""
        frame_duration = 30   # мс
        frame_size     = int(sample_rate * frame_duration / 1000) * 2  # bytes

        frames         = [
            pcm_bytes[i:i + frame_size]
            for i in range(0, len(pcm_bytes) - frame_size, frame_size)
        ]

        speech_frames = sum(
            1 for f in frames
            if len(f) == frame_size and self._vad.is_speech(f, sample_rate)
        )

        speech_seconds = speech_frames * frame_duration / 1000
        is_violation   = speech_seconds > self.threshold_seconds

        logger.debug("[Audio] speech=%.2fs violation=%s", speech_seconds, is_violation)

        return {
            "speech_detected":  speech_frames > 0,
            "speech_seconds":   round(speech_seconds, 2),
            "violation":        is_violation,
            "type":             "voice_activity" if is_violation else None,
        }