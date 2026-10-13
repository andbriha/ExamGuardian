# models/identity_analyzer.py
from deepface import DeepFace
import numpy as np
import cv2

class IdentityAnalyzer:

    def __init__(self, model_name: str = "Facenet512", threshold: float = 0.65):
        self.model_name = model_name
        self.threshold  = threshold
        self._warmup()

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

    def compare(self, reference_bytes: bytes, current_bytes: bytes) -> dict:
        ref_img = self._decode(reference_bytes)
        cur_img = self._decode(current_bytes)

        try:
            ref_rep = DeepFace.represent(
                img_path          = ref_img,
                model_name        = self.model_name,
                detector_backend  = "opencv",
                enforce_detection = False,
            )

            cur_rep = DeepFace.represent(
                img_path          = cur_img,
                model_name        = self.model_name,
                detector_backend  = "opencv",
                enforce_detection = False,
            )

            if not ref_rep or not cur_rep:
                return {
                    "match": False,
                    "distance": 1.0,
                    "threshold": self.threshold,
                    "reason": "no_face_detected"
                }

            ref_embedding = ref_rep[0]["embedding"]
            cur_embedding = cur_rep[0]["embedding"]

            ref_vec = np.array(ref_embedding)
            cur_vec = np.array(cur_embedding)
            cosine  = 1 - np.dot(ref_vec, cur_vec) / (
                np.linalg.norm(ref_vec) * np.linalg.norm(cur_vec)
            )
            distance = round(float(cosine), 4)
            match    = distance < self.threshold

            print(f"[IdentityAnalyzer] Compared: distance={distance}, threshold={self.threshold}, match={match}")

            return {
                "match":     match,
                "distance":  distance,
                "threshold": self.threshold,
                "reason":    None if match else "face_mismatch",
            }

        except ValueError as e:
            reason = "no_face_in_reference" if "img1" in str(e) else "no_face_in_frame"
            return {"match": False, "distance": None, "threshold": self.threshold, "reason": reason}
        except Exception as e:
            print(f"[IdentityAnalyzer] Exception in compare: {e}")
            return {"match": False, "distance": None, "threshold": self.threshold, "reason": str(e)}

    def _decode(self, img_bytes: bytes) -> np.ndarray:
        arr = np.frombuffer(img_bytes, np.uint8)
        bgr = cv2.imdecode(arr, cv2.IMREAD_COLOR)
        if bgr is None:
            raise ValueError("Failed to decode image: corrupted or unsupported format")
        # DeepFace expects BGR numpy array
        return bgr