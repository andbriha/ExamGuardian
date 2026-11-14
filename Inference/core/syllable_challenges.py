"""A small pronunciation alphabet, independent of any natural-language lexicon."""
import secrets
from itertools import product

SYLLABLES = {"MA": ("m", "a"), "MI": ("m", "i"), "MU": ("m", "u")}
# Accept vowel length and common realizations, never missing/reordered consonants.
ALIASES = {"ɑ": "a", "ɐ": "a", "ɪ": "i", "ʊ": "u"}
PHRASES = tuple(' '.join(parts) for parts in product(SYLLABLES, repeat=5)
                if len(set(parts)) == 3 and all(a != b for a, b in zip(parts, parts[1:])))


def new_phrase():
    # 42 varied sequences, avoiding monotonous runs that confuse CTC decoding.
    # Keep the bundled pronunciation alphabet; this is not a replay-proof secret.
    return secrets.choice(PHRASES)


def normalize(phone):
    return ALIASES.get(phone.rstrip("ːˑ"), phone.rstrip("ːˑ"))


def score_transcript(result, phrase):
    syllables = phrase.split()
    if not syllables or any(s not in SYLLABLES for s in syllables):
        return False
    expected = [p for s in syllables for p in SYLLABLES[s]]
    phones = result.get("phonemes", [])
    heard = [normalize(p["phone"]) for p in phones]
    # A complete retry is allowed; partial utterances and swapped sounds are not.
    return any(heard[i:i + len(expected)] == expected
               and all(p.get("confidence", 0) >= .65 for p in phones[i:i + len(expected)])
               for i in range(len(heard) - len(expected) + 1))
