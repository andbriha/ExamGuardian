"""Optional real-model smoke test: python -m tests.check_phoneme_model.

Requires downloaded weights and the repository's Client/assets/pronunciation.
Synthetic samples check integration, not human/accent recognition accuracy.
"""
import itertools
from pathlib import Path
import wave
import numpy as np
from models.phoneme_analyzer import get_phoneme_analyzer
from core.syllable_challenges import score_transcript, PHRASES


def main():
    import os
    root = Path(os.getenv("PRONUNCIATION_SAMPLES", str(Path(__file__).resolve().parents[2] / "Client/assets/pronunciation")))
    samples = {}
    for syllable in ("MA", "MI", "MU"):
        with wave.open(str(root / (syllable + ".wav"))) as wav:
            assert wav.getnchannels() == 1 and wav.getsampwidth() == 2
            x = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2")
            samples[syllable] = np.interp(np.arange(0, len(x), wav.getframerate() / 16000), np.arange(len(x)), x).astype("<i2")
    analyzer = get_phoneme_analyzer()
    assert analyzer.available
    for order in itertools.permutations(samples):
        phrase = " ".join(order)
        result = analyzer.transcribe(np.concatenate([part for s in order for part in (samples[s], np.zeros(8000, dtype='<i2'))]).tobytes())
        assert score_transcript(result, phrase), (phrase, result)
        for wrong in itertools.permutations(order):
            if wrong != order:
                assert not score_transcript(result, " ".join(wrong)), (wrong, result)
        print(phrase, "OK", flush=True)
    # Repeated sounds exercise CTC boundaries and the longer live challenge.
    for phrase in PHRASES:
        order = phrase.split()
        result = analyzer.transcribe(np.concatenate([part for s in order for part in (samples[s], np.zeros(8000, dtype='<i2'))]).tobytes())
        assert score_transcript(result, phrase), (phrase, result)
        assert not score_transcript(result, ' '.join(order[:-1] + [('MI' if order[-1] != 'MI' else 'MU')])), result
        print(phrase, 'OK', flush=True)
    for pcm in (b"\0" * 96000, np.random.default_rng(7).integers(-1000, 1000, 48000, dtype="<i2").tobytes()):
        assert not score_transcript(analyzer.transcribe(pcm), "MA MI MU")
    print("Legacy orders and five-syllable sequences accepted; wrong orders, silence and noise rejected.")


if __name__ == "__main__":
    main()
