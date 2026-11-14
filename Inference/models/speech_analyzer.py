"""Offline Ukrainian ASR. Transcription is not speaker identification."""
import json
import logging
import os
from functools import lru_cache
from pathlib import Path

from core.speech_challenges import WORDS, phrase_matches


class SpeechAnalyzer:
    def __init__(self):
        self.model = None
        path = Path(os.environ.get("SPEECH_MODEL_PATH", "models/vosk-model-small-uk-v3-small"))
        if path.is_dir():
            try:
                from vosk import Model
                self.model = Model(str(path))
            except Exception:
                logging.getLogger(__name__).exception("Speech model unavailable")

    @property
    def available(self):
        return self.model is not None

    def transcribe(self, pcm, sample_rate=16000):
        if not self.available:
            raise RuntimeError("Speech model unavailable")
        from vosk import KaldiRecognizer
        # A recognizer belongs to one request, never shared between students.
        recognizer = KaldiRecognizer(self.model, sample_rate)
        recognizer.SetWords(True)
        segments = []
        for offset in range(0, len(pcm), 8000):
            if recognizer.AcceptWaveform(pcm[offset:offset + 8000]):
                segments.append(json.loads(recognizer.Result()))
        segments.append(json.loads(recognizer.FinalResult()))
        words = [word for segment in segments for word in segment.get("result", [])]
        return {"transcript": " ".join(segment.get("text", "") for segment in segments).strip(), "words": words,
                "confidence": min((w.get("conf", 0) for w in words), default=0)}


@lru_cache(maxsize=1)
def get_speech_analyzer():
    return SpeechAnalyzer()
