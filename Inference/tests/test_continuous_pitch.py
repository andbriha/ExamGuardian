import importlib.util
from pathlib import Path
import threading
import types
import unittest
from unittest.mock import patch
import cv2
import numpy as np


class ContinuousPitch(unittest.TestCase):
    def test_actual_adapter_uses_face_height_for_downward_observation(self):
        # Exercise the adapter without downloading/starting a MediaPipe model.
        python=types.ModuleType('mediapipe.tasks.python');python.vision=types.SimpleNamespace()
        tasks=types.ModuleType('mediapipe.tasks');tasks.python=python
        mp=types.ModuleType('mediapipe');mp.tasks=tasks
        mp.Image=lambda **kw:kw;mp.ImageFormat=types.SimpleNamespace(SRGB='rgb')
        spec=importlib.util.spec_from_file_location('isolated_liveness',Path(__file__).parents[1]/'models/liveness_analyzer.py')
        module=importlib.util.module_from_spec(spec)
        # Preload the small shared adapter dependency so module isolation does
        # not unload native numpy/OpenCV extensions.
        import models.frame_quality
        with patch.dict('sys.modules',{'mediapipe':mp,'mediapipe.tasks':tasks,'mediapipe.tasks.python':python}): spec.loader.exec_module(module)
        analyzer=module.LivenessAnalyzer.__new__(module.LivenessAnalyzer)
        analyzer._lock=threading.Lock();analyzer.yaw_threshold=.19;analyzer.pitch_threshold=.17
        image=cv2.imencode('.jpg',np.full((1000,1000,3),128,dtype=np.uint8))[1].tobytes()
        for height in (.6,.3):
            landmarks=[types.SimpleNamespace(x=.5,y=.5) for _ in range(468)]
            landmarks[10].y=.5-height/2;landmarks[152].y=.5+height/2
            landmarks[234].x=.5-height/3;landmarks[454].x=.5+height/3
            result=types.SimpleNamespace(face_landmarks=[landmarks],face_blendshapes=[[]])
            analyzer._detector=types.SimpleNamespace(detect=lambda _:result)
            for pitch in (.1,.2):
                landmarks[1].y=.5+pitch*height
                actual=analyzer.analyze_continuous(image)
                self.assertAlmostEqual(actual['pitch'],pitch)
                self.assertEqual(any(v['type']=='looking_down' for v in actual['violations']),pitch>.17)
