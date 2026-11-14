import importlib.util
from pathlib import Path
import types
import numpy
import cv2
import unittest
from unittest.mock import Mock, patch


class ReferenceCache(unittest.TestCase):
    def test_reenrollment_uses_new_content_without_restart(self):
        deepface = Mock()
        deepface.represent.side_effect = lambda **kw: [{'embedding': kw['img_path']}]
        spec=importlib.util.spec_from_file_location('isolated_identity',Path(__file__).parents[1]/'models/identity_analyzer.py')
        module=importlib.util.module_from_spec(spec)
        with patch.dict('sys.modules', {'deepface':types.SimpleNamespace(DeepFace=deepface)}): spec.loader.exec_module(module)
        analyzer=module.IdentityAnalyzer.__new__(module.IdentityAnalyzer)
        analyzer.model_name='test';analyzer._decode=lambda data:data
        old=analyzer._reference(b'old photo');new=analyzer._reference(b'new photo')
        self.assertNotEqual(old,new)
        self.assertEqual(analyzer._reference(b'new photo'),new)
        self.assertEqual(deepface.represent.call_count,2)
