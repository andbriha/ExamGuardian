# models/object_detector.py
from ultralytics import YOLO
import cv2
import numpy as np
import logging

logger = logging.getLogger(__name__)

# COCO класи які нас цікавлять
FORBIDDEN_CLASSES = {
    67: "cell phone",
    0:  "person",      # друга особа в кадрі
    76: "book",
    73: "laptop",
}

class ObjectDetector:

    MODEL_NAME = "yolov8n.pt"  # nano — найшвидша версія

    def __init__(self, confidence: float = 0.50):
        self.confidence = confidence
        self._model = YOLO(self.MODEL_NAME)  # завантажить автоматично
        logger.info("[ObjectDetector] YOLOv8n loaded.")

    def detect(self, image_bytes: bytes) -> dict:
        arr    = np.frombuffer(image_bytes, np.uint8)
        img    = cv2.imdecode(arr, cv2.IMREAD_COLOR)
        if img is None:
            return {"violations": [], "person_count": 0}

        results = self._model(img, verbose=False)[0]
        
        violations   = []
        person_count = 0

        for box in results.boxes:
            cls_id     = int(box.cls[0])
            conf       = float(box.conf[0])
            
            if conf < self.confidence:
                continue
            
            if cls_id == 0:  # person
                person_count += 1
                continue
            
            if cls_id in FORBIDDEN_CLASSES:
                violations.append({
                    "type":       FORBIDDEN_CLASSES[cls_id],
                    "confidence": round(conf, 4),
                })

        # якщо в кадрі більше однієї особи — підказка
        if person_count > 1:
            violations.append({
                "type":       "extra_person",
                "confidence": 1.0,
            })

        return {
            "violations":   violations,
            "person_count": person_count,
        }