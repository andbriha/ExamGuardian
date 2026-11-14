"""Speech challenge vocabulary and scoring, independent of the ASR backend."""
import re
import secrets

WORDS = ("сонце", "книга", "вода", "школа", "місто", "ранок", "небо", "дерево")


def new_phrase():
    # Same wire task supports one word, a pair, or a short ordered sequence.
    rng = secrets.SystemRandom()
    return " ".join(rng.sample(WORDS, rng.choice((1, 2, 3, 4))))


def tokens(text):
    return re.findall(r"[^\W\d_]+", text.casefold(), re.UNICODE)


def matching_spans(text, phrase):
    heard, expected = tokens(text), tokens(phrase)
    if not expected:
        return []
    return [(i, i + len(expected)) for i in range(len(heard) - len(expected) + 1)
            if heard[i:i + len(expected)] == expected]


def phrase_matches(text, phrase):
    # Allow a retry or introductory words, but never fuzzy, reordered or partial matches.
    return bool(matching_spans(text, phrase))


def score_transcript(result, phrase):
    spans = matching_spans(result.get("transcript", ""), phrase)
    words = result.get("words")
    if words is None:
        return bool(spans) and result.get("confidence", 0) >= .65
    # Only the words of the successful utterance determine its confidence.
    aligned = [(token, word.get("conf", 0)) for word in words for token in tokens(word.get("word", ""))]
    if [token for token, _ in aligned] != tokens(result.get("transcript", "")):
        return False
    return any(all(conf >= .65 for _, conf in aligned[start:end]) for start, end in spans)
