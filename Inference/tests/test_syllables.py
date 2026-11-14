import unittest
from core.syllable_challenges import new_phrase, score_transcript, SYLLABLES, PHRASES
from unittest.mock import patch


def recognized(phones, confidence=.9):
    return {"phonemes": [{"phone": p, "confidence": confidence} for p in phones.split()]}


class Syllables(unittest.TestCase):
    def test_generation_has_no_language_words(self):
        for _ in range(50):
            phrase = new_phrase().split()
            self.assertEqual(len(phrase), 5)
            self.assertTrue(set(phrase) <= SYLLABLES.keys())

    def test_generation_avoids_monotonous_runs(self):
        self.assertEqual(len(set(PHRASES)),42)
        for phrase in PHRASES:
            parts=phrase.split()
            self.assertEqual(set(parts),set(SYLLABLES))
            self.assertTrue(all(a!=b for a,b in zip(parts,parts[1:])))
        with patch('core.syllable_challenges.secrets.choice',return_value=PHRASES[0]):
            self.assertEqual(new_phrase(),PHRASES[0])
        self.assertTrue(score_transcript(recognized('m a m a m i m u m a'), 'MA MA MI MU MA'))
        self.assertFalse(score_transcript(recognized('m a m i m u'), 'MA MA MI MU MA'))

    def test_vowel_length_and_complete_retry(self):
        self.assertTrue(score_transcript(recognized("m ɑː m iː m u"), "MA MI MU"))
        self.assertTrue(score_transcript(recognized("m i m a m i m u"), "MA MI MU"))

    def test_partial_reordered_noise_and_low_confidence_fail(self):
        for phones in ("", "m a m i", "m u m i m a", "a i u", "m a s m i m u", "b a m i m u"):
            self.assertFalse(score_transcript(recognized(phones), "MA MI MU"))
        self.assertFalse(score_transcript(recognized("m a m i m u", .4), "MA MI MU"))
        self.assertFalse(score_transcript(recognized("m a m i m u"), ""))
        self.assertFalse(score_transcript(recognized("m a m i m u"), "hello"))


if __name__ == "__main__":
    unittest.main()
