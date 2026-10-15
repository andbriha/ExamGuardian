"""Offline Ukrainian ASR. Transcription is not speaker identification."""
import json
import logging
import os
import re
from functools import lru_cache
from pathlib import Path

WORDS = ("сонце", "книга", "вода", "школа", "місто", "ранок", "небо", "дерево")


def phrase_matches(text, phrase):
    # Whole words in order; never substring or fuzzy matches for admission.
    normalize = lambda s: re.findall(r"[^\W\d_]+", s.casefold(), re.UNICODE)
    return normalize(text) == normalize(phrase)


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
        recognizer.AcceptWaveform(pcm)
        result = json.loads(recognizer.FinalResult())
        words = result.get("result", [])
        return {"transcript": result.get("text", ""),
                "confidence": min((w.get("conf", 0) for w in words), default=0)}


@lru_cache(maxsize=1)
def get_speech_analyzer():
    return SpeechAnalyzer()
