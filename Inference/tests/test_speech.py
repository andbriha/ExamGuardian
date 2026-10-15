import unittest
from models.speech_analyzer import phrase_matches


class PhraseMatching(unittest.TestCase):
    def test_words_order_and_unicode(self):
        self.assertTrue(phrase_matches("Сонце, книга вода!", "сонце книга вода"))
        for text in ("", "сонце книга", "вода книга сонце", "сонцекнига вода", "сонце книга вода школа"):
            self.assertFalse(phrase_matches(text, "сонце книга вода"))
