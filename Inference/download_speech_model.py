"""Provision the official Apache-2.0 Ukrainian Vosk model before startup."""
from pathlib import Path
import tempfile
import urllib.request
import zipfile

NAME = "vosk-model-small-uk-v3-small"
URL = f"https://alphacephei.com/vosk/models/{NAME}.zip"


def install(root=Path("models")):
    root = root.resolve()
    if (root / NAME / "am" / "final.mdl").is_file():
        return
    root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        archive = Path(tmp) / "model.zip"
        urllib.request.urlretrieve(URL, archive)
        with zipfile.ZipFile(archive) as bundle:
            for entry in bundle.infolist():
                target = (root / entry.filename).resolve()
                if not target.is_relative_to(root / NAME):
                    raise ValueError("Unexpected speech model archive path")
            bundle.extractall(root)
    print(f"Speech model installed: {root / NAME}")


if __name__ == "__main__":
    install()
