"""Real routing/policy/persistence with deterministic model outputs, no accuracy claim."""
import importlib
import json
import os
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest.mock import AsyncMock, patch
import cv2
import numpy as np
from core import outbox


class ContinuousRoutes(unittest.IsolatedAsyncioTestCase):
    @classmethod
    def setUpClass(cls):
        deps = types.ModuleType('api.deps')
        for name in ('get_identity_analyzer','analysis_slot','audio_slot','get_liveness_analyzer','get_object_detector','get_audio_analyzer'):
            setattr(deps,name,lambda: None)
        models = {}
        for module, name in [('liveness_analyzer','LivenessAnalyzer'),('object_detector','ObjectDetector')]:
            stub=types.ModuleType('models.'+module); setattr(stub,name,type(name,(),{})); models['models.'+module]=stub
        with patch.dict(sys.modules, {'api.deps':deps, **models}):
            cls.frame=importlib.import_module('api.routes.frame')
            cls.audio=importlib.import_module('api.routes.audio')

    def setUp(self):
        self.folder=tempfile.TemporaryDirectory(); self.addCleanup(self.folder.cleanup)
        self.enterContext(patch.object(outbox,'STATE_PATH',Path(self.folder.name)/'outbox.db'))
        self.enterContext(patch.dict(os.environ,{'INFERENCE_IMAGES_PATH':self.folder.name}))
        self.session={}
        for route in (self.frame,self.audio):
            self.enterContext(patch.object(route,'require_active_session',AsyncMock(side_effect=lambda *args:self.session)))
            self.enterContext(patch.object(route,'acknowledge_analysis',AsyncMock()))
        self.enterContext(patch.object(self.audio,'get_speech_analyzer',return_value=types.SimpleNamespace(available=False)))

    def rows(self):
        with outbox.connect() as db:
            return [json.loads(row[0]) for row in db.execute('SELECT payload FROM outbox').fetchall()]

    async def frame_at(self,tick,objects=(),quality=None):
        content=cv2.imencode('.jpg',np.full((120,120,3),128,np.uint8))[1].tobytes()
        file=types.SimpleNamespace(read=AsyncMock(return_value=content))
        pose={'has_face':True,'quality_reason':quality,'yaw':0,'mouth':0,'eyes_closed':0,
              'violations':[{'type':'looking_down','confidence':1}] if quality else []}
        return await self.frame.analyze_frame(None,session_token='test',user_info={},_slot=None,
            session_id=1,relative_ts=tick,file=file,
            liveness=types.SimpleNamespace(analyze_continuous=lambda data:pose),
            detector=types.SimpleNamespace(detect=lambda data:{'violations':list(objects)}),
            identity=types.SimpleNamespace(compare=lambda *args: {'match':True}))

    async def test_allowed_phone_and_bad_quality_do_not_become_behavior_alerts(self):
        self.session={'allowedObjects':['cell_phone']}
        phone={'type':'cell phone','confidence':.9}
        for tick in range(4): await self.frame_at(tick,[phone],'too_dark')
        self.assertEqual(self.rows(),[])
        for tick in range(4,11): await self.frame_at(tick,[phone],'too_dark')
        rows=self.rows()
        self.assertEqual([row['violationType'] for row in rows],['analysis_unavailable'])
        self.assertEqual(rows[0]['confidence'],0)

    async def test_phone_episode_closes_without_creating_another_card(self):
        self.enterContext(patch.object(self.frame,'should_emit',return_value=False))
        phone={'type':'cell phone','confidence':.9}
        for tick in (0,1,2): await self.frame_at(tick,[phone])
        first=self.rows()[0]
        await self.frame_at(3)
        rows=self.rows(); self.assertEqual(len(rows),1)
        self.assertEqual(rows[0]['eventId'],first['eventId'])
        self.assertEqual(json.loads(rows[0]['metadata'])['episode']['status'],'ended')
        self.assertTrue((Path(self.folder.name)/'evidence'/(first['eventId']+'.jpg')).is_file())

    async def test_speech_crosses_packets_but_allowed_speech_remains_silent(self):
        async def send(sid,tick):
            file=types.SimpleNamespace(read=AsyncMock(return_value=b'\x01\x00'*32000))
            analyzer=types.SimpleNamespace(analyze=lambda pcm,rate:{'speech_detected':True,'speech_seconds':len(pcm)/(rate*2),'violation':len(pcm)/(rate*2)>3})
            return await self.audio.analyze_audio(None,session_token='test',user_info={},_slot=None,
                session_id=sid,relative_ts=tick,sample_rate=16000,file=file,analyzer=analyzer)
        self.assertFalse((await send(1,0))['violation'])
        self.assertTrue((await send(1,2))['violation'])
        self.assertEqual(len(self.rows()),1)
        self.session={'allowSpeech':True}
        await send(2,0); self.assertFalse((await send(2,2))['violation'])
        self.assertEqual(len(self.rows()),1)
