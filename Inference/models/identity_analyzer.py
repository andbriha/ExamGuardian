# models/identity_analyzer.py
from deepface import DeepFace
import numpy as np
import cv2
import logging
from functools import lru_cache

logger = logging.getLogger(__name__)

class IdentityAnalyzer:

    def __init__(self, model_name: str = "Facenet512", threshold: float = 0.40):
        self.model_name = model_name
        self.threshold  = threshold
        self._warmup()
        DeepFace.build_model(model_name="Fasnet", task="spoofing")

    def _warmup(self):
        dummy = np.zeros((160, 160, 3), dtype=np.uint8)
        try:
            DeepFace.represent(
                img_path          = dummy,
                model_name        = self.model_name,
                detector_backend  = "skip",
                enforce_detection = False,
            )
            print(f"[IdentityAnalyzer] Model {self.model_name} warmed up.")
        except Exception:
            pass

    def compare(self, reference_bytes: bytes, current_bytes: bytes, check_spoof: bool = True) -> dict:
        try:
            cur_img = self._decode(current_bytes)
            ref_rep = self._reference(reference_bytes)

            cur_rep = DeepFace.represent(
                img_path          = cur_img,
                anti_spoofing     = check_spoof,
                model_name        = self.model_name,
                detector_backend  = "opencv",
                enforce_detection = True,
            )

            if len(ref_rep) != 1 or len(cur_rep) != 1:
                return {
                    "match": False,
                    "distance": 1.0,
                    "threshold": self.threshold,
                    "reason": "no_face_detected"
                }

            area = cur_rep[0].get("facial_area", {})
            if area.get("w", 0) < 60 or area.get("h", 0) < 60:
                return {"match": False, "distance": None, "threshold": self.threshold, "reason": "face_too_small"}
            ref_embedding = ref_rep[0]["embedding"]
            cur_embedding = cur_rep[0]["embedding"]

            ref_vec = np.array(ref_embedding)
            cur_vec = np.array(cur_embedding)
            cosine  = 1 - np.dot(ref_vec, cur_vec) / (
                np.linalg.norm(ref_vec) * np.linalg.norm(cur_vec)
            )
            if not np.isfinite(cosine):
                return {"match": False, "distance": None, "reason": "identity_unavailable"}
            distance = round(float(cosine), 4)
            match    = bool(np.isfinite(cosine) and cosine < self.threshold)

            print(f"[IdentityAnalyzer] Compared: distance={distance}, threshold={self.threshold}, match={match}")

            return {
                "match":     match,
                "distance":  distance,
                "threshold": self.threshold,
                "reason":    None if match else "face_mismatch",
            }

        except ValueError as e:
            reason = "spoof_suspected" if "spoof" in str(e).lower() else "no_face_in_frame"
            logger.info("Identity frame rejected: %s (anti-spoof checkpoint=%s)", reason, check_spoof)
            return {"match": False, "distance": None, "threshold": self.threshold, "reason": reason}
        except Exception as e:
            print(f"[IdentityAnalyzer] Exception in compare: {e}")
            logger.exception("Identity inference failed")
            return {"match": False, "distance": None, "threshold": self.threshold, "reason": "identity_unavailable"}

    @lru_cache(maxsize=64)
    def _reference(self, reference_bytes):
        # Content-keyed: replacing an enrollment photo invalidates its embedding.
        return DeepFace.represent(img_path=self._decode(reference_bytes),
                                  model_name=self.model_name,
                                  detector_backend="opencv", enforce_detection=True)

    def _decode(self, img_bytes: bytes) -> np.ndarray:
        arr = np.frombuffer(img_bytes, np.uint8)
        bgr = cv2.imdecode(arr, cv2.IMREAD_COLOR)
        if bgr is None:
            raise ValueError("Failed to decode image: corrupted or unsupported format")
        # DeepFace expects BGR numpy array
        return bgr
