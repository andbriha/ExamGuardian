# models/liveness_analyzer.py
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
    VALID_TASKS = {"turn_left", "turn_right", "nod", "presence_check"}

    MIN_EAR_DISTANCE = 0.10

    def __init__(self, yaw_threshold: float = 0.03):
        self.threshold = yaw_threshold
        self._ensure_model()

        base_options = python.BaseOptions(model_asset_path=self.MODEL_PATH)
        options = vision.FaceLandmarkerOptions(
            base_options=base_options,
            output_face_blendshapes=False,
            output_facial_transformation_matrixes=False,
            num_faces=1,
            running_mode=vision.RunningMode.IMAGE,
        )
        self._detector = vision.FaceLandmarker.create_from_options(options)

    def _ensure_model(self):
        os.makedirs("models", exist_ok=True)
        if not os.path.exists(self.MODEL_PATH):
            logger.info("[LivenessAnalyzer] Downloading face_landmarker.task...")
            urllib.request.urlretrieve(self.MODEL_URL, self.MODEL_PATH)
            logger.info("[LivenessAnalyzer] Model downloaded.")

    def analyze(self, image_bytes: bytes, task: str) -> dict:
        if task not in self.VALID_TASKS:
            return {"passed": False, "reason": f"unknown_task: {task}"}

        img_np = cv2.imdecode(np.frombuffer(image_bytes, np.uint8), cv2.IMREAD_COLOR)
        if img_np is None:
            return {"passed": False, "reason": "invalid_image"}

        img_np = self._normalize_brightness(img_np)
        img_rgb  = cv2.cvtColor(img_np, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=img_rgb)
        result   = self._detector.detect(mp_image)

        if not result.face_landmarks:
            logger.debug("[Liveness] task=%s → no_face", task)
            return {"passed": False, "reason": "no_face"}

        if task == "presence_check":
            return {"passed": True, "reason": None}

        lm = result.face_landmarks[0]
        ear_distance = abs(lm[234].x - lm[454].x)
        if ear_distance < self.MIN_EAR_DISTANCE:
            return {"passed": False, "reason": "face_too_small"}

        nose_x   = lm[1].x
        center_x = (lm[234].x + lm[454].x) / 2
        yaw      = (nose_x - center_x) / ear_distance

        nose_y     = lm[1].y
        center_y   = (lm[152].y + lm[10].y) / 2
        pitch      = nose_y - center_y

        if task == "turn_left"  and yaw < -self.threshold:
            return {"passed": True}
        if task == "turn_right" and yaw >  self.threshold:
            return {"passed": True}
        if task == "nod"        and pitch > self.threshold:
            return {"passed": True}

        return {
            "passed": False,
            "reason": "wrong_pose",
            "debug":  {"yaw": round(yaw, 4), "pitch": round(pitch, 4)},
        }

    def analyze_continuous(self, image_bytes: bytes) -> dict:
        """
        Аналіз кадру під час реального іспиту:
        - Детекція відсутності обличчя (face_missing)
        - Детекція відведення погляду / підказок збоку (looking_away_left, looking_away_right)
        - Детекція погляду вниз на шпаргалку/телефон (looking_down)
        - Детекція навушників / мікронавушників у ділянці вух (micro_earpiece)
        """
        img_np = cv2.imdecode(np.frombuffer(image_bytes, np.uint8), cv2.IMREAD_COLOR)
        if img_np is None:
            return {"has_face": False, "violations": [{"type": "face_missing", "confidence": 1.0}]}

        h, w = img_np.shape[:2]
        img_rgb  = cv2.cvtColor(img_np, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=img_rgb)
        result   = self._detector.detect(mp_image)

        if not result.face_landmarks:
            return {
                "has_face": False,
                "violations": [{
                    "type": "face_missing",
                    "confidence": 1.0,
                    "metadata": {"desc": "Обличчя студента відсутнє перед камерою"}
                }]
            }

        lm = result.face_landmarks[0]
        ear_distance = abs(lm[234].x - lm[454].x)
        if ear_distance < 0.05:
            ear_distance = 0.05

        nose_x   = lm[1].x
        center_x = (lm[234].x + lm[454].x) / 2.0
        yaw      = (nose_x - center_x) / ear_distance

        nose_y   = lm[1].y
        center_y = (lm[152].y + lm[10].y) / 2.0
        pitch    = nose_y - center_y

        violations = []

        # 1. Поворот голови ліворуч (підказки зліва / другий монітор / розмова збоку)
        if yaw < -0.19:
            conf = min(1.0, round(0.70 + abs(yaw) * 0.8, 2))
            violations.append({
                "type": "looking_away_left",
                "confidence": conf,
                "metadata": {
                    "direction": "left",
                    "yaw": round(float(yaw), 3),
                    "pitch": round(float(pitch), 3),
                    "desc": f"Погляд та поворот голови зміщено ліворуч (yaw: {round(float(yaw), 2)})"
                }
            })
        # 2. Поворот голови праворуч (підказки справа / другий монітор)
        elif yaw > 0.19:
            conf = min(1.0, round(0.70 + abs(yaw) * 0.8, 2))
            violations.append({
                "type": "looking_away_right",
                "confidence": conf,
                "metadata": {
                    "direction": "right",
                    "yaw": round(float(yaw), 3),
                    "pitch": round(float(pitch), 3),
                    "desc": f"Погляд та поворот голови зміщено праворуч (yaw: {round(float(yaw), 2)})"
                }
            })

        # 3. Погляд вниз (шпаргалка на столі або телефон на колінах)
        if pitch > 0.17:
            conf = min(1.0, round(0.70 + pitch * 1.2, 2))
            violations.append({
                "type": "looking_down",
                "confidence": conf,
                "metadata": {
                    "direction": "down",
                    "pitch": round(float(pitch), 3),
                    "desc": f"Студент дивиться вниз на стіл/коліна (pitch: {round(float(pitch), 2)})"
                }
            })

        # 4. Аналіз ділянки вух на наявність мікронавушників або гарнітури
        ear_violation = self._detect_earpiece_in_ears(img_np, lm, w, h)
        if ear_violation:
            violations.append(ear_violation)

        return {
            "has_face": True,
            "yaw": round(float(yaw), 3),
            "pitch": round(float(pitch), 3),
            "violations": violations
        }

    def _detect_earpiece_in_ears(self, img_bgr: np.ndarray, lm, w: int, h: int) -> dict | None:
        """
        Аналізує кроп лівого та правого вуха на наявність сторонніх предметів (навушники, TWS, мікронавушник).
        """
        left_ear_idx = 234
        right_ear_idx = 454

        for side, ear_idx in [("left", left_ear_idx), ("right", right_ear_idx)]:
            cx = int(lm[ear_idx].x * w)
            cy = int(lm[ear_idx].y * h)
            box_r = int(w * 0.045)  # радіус навколо вуха

            x1 = max(0, cx - box_r)
            y1 = max(0, cy - box_r)
            x2 = min(w, cx + box_r)
            y2 = min(h, cy + box_r)

            if x2 <= x1 or y2 <= y1:
                continue

            ear_crop = img_bgr[y1:y2, x1:x2]
            if ear_crop.size == 0:
                continue

            # Аналіз контрасту у просторі HSV та градаціях сірого
            gray = cv2.cvtColor(ear_crop, cv2.COLOR_BGR2GRAY)
            hsv = cv2.cvtColor(ear_crop, cv2.COLOR_BGR2HSV)

            # Перевірка на темні/білі сторонні предмети у вушній раковині
            # Білі AirPod або темні Bluetooth мікронавушники
            white_mask = cv2.inRange(hsv, np.array([0, 0, 200]), np.array([180, 50, 255]))
            dark_mask = cv2.inRange(gray, 0, 35)

            white_ratio = np.sum(white_mask > 0) / (white_mask.size + 1e-5)
            dark_ratio = np.sum(dark_mask > 0) / (dark_mask.size + 1e-5)

            if white_ratio > 0.08:
                return {
                    "type": "micro_earpiece",
                    "confidence": round(min(0.96, 0.75 + white_ratio * 1.5), 2),
                    "metadata": {
                        "side": side,
                        "earpiece_type": "white_earbud",
                        "desc": f"Виявлено сторонній предмет (навушник) у {side} вусі"
                    }
                }
            elif dark_ratio > 0.12:
                return {
                    "type": "micro_earpiece",
                    "confidence": round(min(0.94, 0.72 + dark_ratio * 1.2), 2),
                    "metadata": {
                        "side": side,
                        "earpiece_type": "dark_earpiece",
                        "desc": f"Виявлено сторонній предмет (мікронавушник) у {side} вусі"
                    }
                }

        return None

    @staticmethod
    def _normalize_brightness(img_bgr: np.ndarray) -> np.ndarray:
        lab   = cv2.cvtColor(img_bgr, cv2.COLOR_BGR2LAB)
        l, a, b = cv2.split(lab)
        clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
        l     = clahe.apply(l)
        return cv2.cvtColor(cv2.merge([l, a, b]), cv2.COLOR_LAB2BGR)

    def __enter__(self):
        return self

    def __exit__(self, *_):
        if hasattr(self, "_detector"):
            self._detector.close()