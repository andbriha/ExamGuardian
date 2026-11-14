"""Provision the pinned, offline phonetic recognizer (Apache-2.0, ~1.3 GB)."""
from pathlib import Path
from huggingface_hub import snapshot_download

if __name__ == "__main__":
    snapshot_download(
        repo_id="facebook/wav2vec2-lv-60-espeak-cv-ft",
        revision="ae45363bf3413b374fecd9dc8bc1df0e24c3b7f4",
        local_dir=Path(__file__).parent / "models" / "phoneme-model",
        allow_patterns=["config.json", "preprocessor_config.json", "vocab.json", "pytorch_model.bin"],
    )
