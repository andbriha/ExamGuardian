"""Conservative exposure correction for landmarks, never for spoof detection."""
import cv2
import numpy as np


def quality_reason(image):
    if image.size == 0:
        return "face_too_small"
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    if float(np.median(gray)) < 40:
        return "too_dark"
    if float(np.mean(gray > 245)) > .45:
        return "too_bright"
    if float(cv2.Laplacian(gray, cv2.CV_64F).var()) < 12:
        return "too_blurry"
    return None


def landmark_image(image):
    # Correct luminance only; avoid amplifying noise in severely dark frames.
    lab = cv2.cvtColor(image, cv2.COLOR_BGR2LAB)
    light, a, b = cv2.split(lab)
    if 25 < float(np.median(light)) < 110:
        corrected = cv2.createCLAHE(clipLimit=1.5, tileGridSize=(8, 8)).apply(light)
        light = cv2.addWeighted(light, .5, corrected, .5, 0)
        return cv2.cvtColor(cv2.merge((light, a, b)), cv2.COLOR_LAB2BGR)
    return image
