"""Offline CTC phoneme recognition. Does not force audio into the requested answer."""
import json
import logging
import os
from functools import lru_cache
from pathlib import Path
from threading import Lock

import numpy as np
from core.syllable_challenges import normalize


class PhonemeAnalyzer:
    def __init__(self):
        self.model = None
        self.lock = Lock()
        path = Path(os.getenv("PHONEME_MODEL_PATH", str(Path(__file__).parent / "phoneme-model")))
        try:
            import torch
            from transformers import Wav2Vec2FeatureExtractor, Wav2Vec2ForCTC
            self.extractor = Wav2Vec2FeatureExtractor.from_pretrained(path, local_files_only=True)
            self.vocabulary = {v: k for k, v in json.loads((path / "vocab.json").read_text()).items()}
            self.groups = {}
            for token_id, token in self.vocabulary.items():
                self.groups.setdefault(normalize(token), []).append(token_id)
            self.model = Wav2Vec2ForCTC.from_pretrained(path, local_files_only=True).eval()
        except (ImportError, OSError, ValueError):
            logging.exception("Phoneme model unavailable; new challenges use visual actions")

    @property
    def available(self):
        return self.model is not None

    def transcribe(self, pcm, sample_rate=16000):
        import torch
        if not self.available:
            raise RuntimeError("Phoneme recognizer unavailable")
        if sample_rate != 16000 or len(pcm) % 2:
            raise ValueError("Expected PCM16 mono at 16000 Hz")
        samples = np.frombuffer(pcm, dtype="<i2").astype(np.float32) / 32768
        if len(samples) < 1600 or np.sqrt(np.mean(samples ** 2)) < .003:
            return {"transcript": "", "phonemes": []}
        # Give the CTC encoder right context for the final vowel even when the
        # client chunk ends immediately after it. Never fill in missing phones.
        samples = np.pad(samples, (0, 4000))
        with self.lock, torch.inference_mode():
            inputs = self.extractor(samples, sampling_rate=16000, return_tensors="pt")
            probabilities = self.model(**inputs).logits[0].softmax(-1)
            # Sum probability mass for equivalent vowel-length/accent variants.
            # All other phonemes remain candidates; the target is never an input.
            vocabulary = list(self.groups)
            grouped = torch.stack([probabilities[:, ids].sum(-1) for ids in self.groups.values()], dim=-1)
            confidences, ids = grouped.max(-1)
            ids, confidences = ids.tolist(), confidences.tolist()
        phones, last = [], None
        for token_id, confidence in zip(ids, confidences):
            token = vocabulary[token_id]
            if token_id == last:
                if phones and token not in ("<pad>", "<s>", "</s>", "|"):
                    phones[-1]["confidence"] = max(phones[-1]["confidence"], confidence)
                continue
            last = token_id
            if token in ("<pad>", "<s>", "</s>", "|"):
                continue
            phones.append({"phone": token, "confidence": confidence})
        return {"transcript": " ".join(p["phone"] for p in phones), "phonemes": phones}


@lru_cache(maxsize=1)
def _load_analyzer():
    return PhonemeAnalyzer()


_load_lock = Lock()


def get_phoneme_analyzer():
    # lru_cache alone permits concurrent first loads of this large model.
    with _load_lock:
        return _load_analyzer()
