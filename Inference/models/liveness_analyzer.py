# models/liveness_analyzer.py
from core.pose_coordinates import anatomical_yaw, relative_pitch
from models.frame_quality import landmark_image, quality_reason
import threading
import cv2
import logging
import numpy as np
import mediapipe as mp
import urllib.request
import os
from mediapipe.tasks import python
from mediapipe.tasks.python import vision

logger = logging.getLogger(__name__)


class LivenessAnalyzer:

    MODEL_PATH  = "models/face_landmarker.task"
    MODEL_URL   = "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task"
    def __init__(self, yaw_threshold: float = 0.19, pitch_threshold: float = 0.17):
        self.yaw_threshold = yaw_threshold
        self.pitch_threshold = pitch_threshold
        self._ensure_model()

        base_options = python.BaseOptions(model_asset_path=self.MODEL_PATH)
        options = vision.FaceLandmarkerOptions(
            base_options=base_options,
            output_face_blendshapes=True,
            output_facial_transformation_matrixes=False,
            num_faces=2,
            running_mode=vision.RunningMode.IMAGE,
        )
        self._detector = vision.FaceLandmarker.create_from_options(options)
        self._lock = threading.Lock()

    def _ensure_model(self):
        os.makedirs("models", exist_ok=True)
        if not os.path.exists(self.MODEL_PATH):
            logger.info("[LivenessAnalyzer] Downloading face_landmarker.task...")
            urllib.request.urlretrieve(self.MODEL_URL, self.MODEL_PATH)
            logger.info("[LivenessAnalyzer] Model downloaded.")

    def analyze_continuous(self, image_bytes: bytes) -> dict:
        """
        Аналіз кадру під час реального іспиту:
        - Детекція відсутності обличчя (face_missing)
        - Позначення повороту голови (looking_away_left, looking_away_right)
        - Позначення нахилу голови (looking_down)
        """
        img_np = cv2.imdecode(np.frombuffer(image_bytes, np.uint8), cv2.IMREAD_COLOR)
        if img_np is None:
            return {"has_face": False, "violations": [{"type": "face_missing", "confidence": 1.0}]}

        h, w = img_np.shape[:2]
        img_rgb  = cv2.cvtColor(landmark_image(img_np), cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=img_rgb)
        with self._lock:
            result = self._detector.detect(mp_image)

        if not result.face_landmarks:
            return {
                "has_face": False,
                "quality_reason": quality_reason(img_np) or "no_face",
                "violations": [{
                    "type": "face_missing",
                    "confidence": 1.0,
                    "metadata": {"desc": "Обличчя не розпізнано у кадрі; потрібна перевірка запису та якості освітлення"}
                }]
            }

        if len(result.face_landmarks) != 1:
            return {"has_face": False, "quality_reason": "multiple_faces", "violations": [{"type": "multiple_faces", "confidence": 1.0}]}
        lm = result.face_landmarks[0]
        x1, x2 = max(0, int(min(p.x for p in lm)*w)), min(w, int(max(p.x for p in lm)*w))
        y1, y2 = max(0, int(min(p.y for p in lm)*h)), min(h, int(max(p.y for p in lm)*h))
        quality = "face_too_small" if x2-x1 < 100 or y2-y1 < 100 else quality_reason(img_np[y1:y2, x1:x2])
        shapes = {s.category_name: float(s.score) for s in result.face_blendshapes[0]}

        ear_distance = abs(lm[234].x - lm[454].x)
        if ear_distance < 0.05:
            ear_distance = 0.05

        nose_x   = lm[1].x
        center_x = (lm[234].x + lm[454].x) / 2.0
        # Anatomical direction: in an unmirrored camera frame, subject-left
        # projects toward image-right. Positive yaw means the subject turns right.
        yaw      = anatomical_yaw(nose_x, lm[234].x, lm[454].x)

        nose_y   = lm[1].y
        center_y = (lm[152].y + lm[10].y) / 2.0
        pitch    = nose_y - center_y

        violations = []

        # 1. Поворот голови ліворуч (підказки зліва / другий монітор / розмова збоку)
        if yaw < -self.yaw_threshold:
            conf = min(1.0, round(0.70 + abs(yaw) * 0.8, 2))
            violations.append({
                "type": "looking_away_left",
                "confidence": conf,
                "metadata": {
                    "direction": "left",
                    "yaw": round(float(yaw), 3),
                    "pitch": round(float(pitch), 3),
                    "desc": f"Поворот голови зміщено ліворуч (yaw: {round(float(yaw), 2)})"
                }
            })
        # 2. Поворот голови праворуч (підказки справа / другий монітор)
        elif yaw > self.yaw_threshold:
            conf = min(1.0, round(0.70 + abs(yaw) * 0.8, 2))
            violations.append({
                "type": "looking_away_right",
                "confidence": conf,
                "metadata": {
                    "direction": "right",
                    "yaw": round(float(yaw), 3),
                    "pitch": round(float(pitch), 3),
                    "desc": f"Поворот голови зміщено праворуч (yaw: {round(float(yaw), 2)})"
                }
            })

        # 3. Погляд вниз (шпаргалка на столі або телефон на колінах)
        if pitch > self.pitch_threshold:
            conf = min(1.0, round(0.70 + pitch * 1.2, 2))
            violations.append({
                "type": "looking_down",
                "confidence": conf,
                "metadata": {
                    "direction": "down",
                    "pitch": round(float(pitch), 3),
                    "desc": f"Позначено нахил голови вниз (pitch: {round(float(pitch), 2)})"
                }
            })

        return {
            "has_face": True,
            "quality_reason": quality,
            "gesture_pitch": round(relative_pitch(lm[1].y, lm[10].y, lm[152].y), 4),
            "smile": min(shapes.get("mouthSmileLeft", 0), shapes.get("mouthSmileRight", 0)),
            "mouth": shapes.get("jawOpen", 0),
            "eyes_closed": min(shapes.get("eyeBlinkLeft", 0), shapes.get("eyeBlinkRight", 0)),
            "yaw": round(float(yaw), 3),
            "pitch": round(float(pitch), 3),
            "violations": violations
        }

    def __enter__(self):
        return self

    def __exit__(self, *_):
        if hasattr(self, "_detector"):
            self._detector.close()
