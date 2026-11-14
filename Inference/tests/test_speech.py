import unittest
import sys
import types
from unittest.mock import patch
from core.speech_challenges import score_transcript, new_phrase, WORDS
from models.speech_analyzer import phrase_matches


class PhraseMatching(unittest.TestCase):
    def test_words_order_and_unicode(self):
        self.assertTrue(phrase_matches("Сонце, книга вода!", "сонце книга вода"))
        for text in ("", "сонце книга", "вода книга сонце", "сонцекнига вода", "сонце школа книга вода"):
            self.assertFalse(phrase_matches(text, "сонце книга вода"))

    def test_repetition_and_intro_do_not_hide_a_complete_utterance(self):
        for text in ("сонце книга вода сонце книга вода", "зараз скажу сонце книга вода", "сонце книга вода школа"):
            self.assertTrue(phrase_matches(text, "сонце книга вода"))
        self.assertFalse(phrase_matches("сонце", ""))

    def test_confidence_belongs_to_matched_words_not_a_failed_retry(self):
        words = [{'word':word, 'conf':conf} for word,conf in [('вода',.1),('сонце',.9),('книга',.8)]]
        result = {'transcript':'вода сонце книга', 'words': words, 'confidence':.1}
        self.assertTrue(score_transcript(result, 'сонце книга'))
        words[-1]['conf'] = .4
        self.assertFalse(score_transcript(result, 'сонце книга'))

    def test_one_to_four_words_are_supported(self):
        for _ in range(100):
            phrase = new_phrase().split()
            self.assertIn(len(phrase), (1,2,3,4))
            self.assertEqual(len(phrase),len(set(phrase)))
            self.assertTrue(set(phrase).issubset(WORDS))

    def test_asr_keeps_words_before_and_after_silence(self):
        from models.speech_analyzer import SpeechAnalyzer
        class Recognizer:
            def __init__(self, *_): pass
            def SetWords(self, _): pass
            def AcceptWaveform(self, _): return True
            def Result(self): return '{"text":"сонце", "result":[{"word":"сонце","conf":0.9}]}'
            def FinalResult(self): return '{"text":"книга", "result":[{"word":"книга","conf":0.9}]}'
        analyzer = SpeechAnalyzer.__new__(SpeechAnalyzer); analyzer.model = object()
        with patch.dict(sys.modules, {'vosk':types.SimpleNamespace(KaldiRecognizer=Recognizer)}):
            result = analyzer.transcribe(b'\0'*8000)
        self.assertEqual(result['transcript'],'сонце книга')
        self.assertTrue(score_transcript(result,'сонце книга'))
