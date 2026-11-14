"""Keep the tested model artifacts tied to the source release."""
import hashlib
import json
import os
from pathlib import Path

def verify_weights(require_all=False):
    root=Path(__file__).resolve().parents[1]
    expected=json.loads((root/'model-manifest.json').read_text(encoding='utf-8'))
    deepface=Path(os.environ.get('DEEPFACE_HOME',str(Path.home())))/'.deepface/weights'
    verified={}
    for name,digest in expected.items():
        path=deepface/name.removeprefix('deepface/') if name.startswith('deepface/') else root/name
        if not path.is_file():
            if require_all: raise RuntimeError(f'Required model artifact missing: {name}')
            continue
        with path.open('rb') as file: actual=hashlib.file_digest(file,'sha256').hexdigest()
        if actual!=digest: raise RuntimeError(f'Model artifact differs from the tested release: {name}')
        verified[name]=actual
    return verified
