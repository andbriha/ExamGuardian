from contextlib import closing
import asyncio
import importlib
import os
import sqlite3
import sys
import tempfile
import time
import types
import unittest
from unittest.mock import patch
from uuid import uuid4
import jwt
import hmac
import hashlib
from cryptography.hazmat.primitives.asymmetric import rsa
from fastapi import HTTPException
from fastapi.security import HTTPAuthorizationCredentials

KEY = "isolated-ai-contract-test-signing-key-2026"
os.environ.update(JWT__KEY=KEY, JWT__ISSUER="ContractIssuer", JWT__AUDIENCE="ContractClient")
_temporary = tempfile.TemporaryDirectory()
os.environ["VERIFICATION_STATE_PATH"] = _temporary.name + "/verification.sqlite3"
os.environ["OBSERVATION_STATE_PATH"] = _temporary.name + "/observations.sqlite3"
os.environ["INFERENCE_IMAGES_PATH"] = _temporary.name
from api.auth import get_current_user
from core.outbox import enqueue, status, should_emit

class TokenContracts(unittest.TestCase):
    def setUp(self):
        self.key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        self.enterContext(patch('api.auth.access_verification_key', return_value=self.key.public_key()))
        self.enterContext(patch('api.auth.require_login_session'))

    def token(self, key=KEY, **overrides):
        claims = {"sub": str(uuid4()), "sid": str(uuid4()), "role": "Student", "login": "test", "exp": int(time.time())+60, "iss": "ContractIssuer", "aud": "ContractClient"}
        claims.update(overrides)
        return HTTPAuthorizationCredentials(scheme="Bearer", credentials=jwt.encode(claims,self.key if key == KEY else rsa.generate_private_key(public_exponent=65537,key_size=2048),algorithm="RS256"))

    def test_shared_mac_is_never_an_access_signer(self):
        raw = jwt.encode({"sub":str(uuid4()), "sid":str(uuid4()), "role":"Student", "exp":int(time.time())+60, "iss":"ContractIssuer", "aud":"ContractClient"}, KEY, algorithm="HS256")
        with self.assertRaises(HTTPException) as caught:
            get_current_user(HTTPAuthorizationCredentials(scheme="Bearer",credentials=raw))
        self.assertEqual(caught.exception.status_code,401)

    def test_valid_student(self):
        self.assertEqual(get_current_user(self.token())["login"], "test")

    def test_signature_expiry_issuer_audience_and_role(self):
        for credentials in [self.token(key="wrong-key-with-at-least-thirty-two-bytes"), self.token(exp=1), self.token(iss="wrong"), self.token(aud="wrong"), self.token(role="Examiner")]:
            with self.subTest(credentials="redacted"):
                with self.assertRaises(HTTPException) as caught: get_current_user(credentials)
                self.assertIn(caught.exception.status_code, [401,403])
        with self.assertRaises(HTTPException): get_current_user(None)

class OutboxContracts(unittest.TestCase):
    def setUp(self):
        from pathlib import Path
        from core import outbox
        # Other suites may import outbox before this module sets its environment.
        self.enterContext(patch.object(outbox, 'STATE_PATH', Path(os.environ['OBSERVATION_STATE_PATH'])))
        from core.outbox import connect
        with connect() as db:
            db.execute("DELETE FROM outbox")
            db.execute("DELETE FROM streaks")

    def test_delivery_survives_reopening_and_is_idempotent(self):
        event = str(uuid4())
        enqueue(event, {"examSessionId": 12})
        enqueue(event, {"examSessionId": 12})
        self.assertEqual(status()["pending"], 1)
        from core import outbox
        self.assertEqual(importlib.reload(outbox).status()["pending"], 1)

    def test_short_glance_is_not_an_incident(self):
        self.assertFalse(should_emit(12,"looking_down"))
        self.assertFalse(should_emit(12,"looking_down"))
        self.assertTrue(should_emit(12,"looking_down"))
        self.assertFalse(should_emit(12,"looking_down"))
        self.assertFalse(should_emit(13,"looking_down"))

class ChallengeContracts(unittest.IsolatedAsyncioTestCase):
    @classmethod
    def setUpClass(cls):
        # These tests exercise the security/state protocol independently of model accuracy.
        deps = types.ModuleType("api.deps")
        for name in ["get_liveness_analyzer", "get_identity_analyzer", "get_audio_analyzer", "analysis_slot", "verification_slot", "audio_slot"]:
            setattr(deps,name,lambda: None)
        sys.modules["api.deps"] = deps
        cls.routes = importlib.import_module("api.routes.verify_session")
        cls.routes.settings.ai_api_key = "isolated-admission-test-api-key-2026"

    async def test_legacy_clients_and_missing_model_receive_visual_tasks(self):
        user = {"id": str(uuid4())}
        with patch.object(self.routes, "get_phoneme_analyzer", return_value=types.SimpleNamespace(available=True)) as model:
            for protocol in (2, 3):
                issued = await self.routes.get_challenge(exam_id=9, user_info=user, client_protocol=protocol)
                self.assertFalse(issued["speech_recognition"])
            model.assert_not_called()
        with patch.object(self.routes, "get_phoneme_analyzer", return_value=types.SimpleNamespace(available=False)):
            issued = await self.routes.get_challenge(exam_id=9, user_info=user, client_protocol=4)
            self.assertFalse(issued["speech_recognition"])
            self.assertEqual(issued["total_steps"], 3)

    async def test_protocol_and_microphone_contract(self):
        user = {"id": str(uuid4())}
        with patch.object(self.routes, "get_phoneme_analyzer", return_value=types.SimpleNamespace(available=False)):
            capabilities = await self.routes.verification_capabilities()
        self.assertEqual(capabilities["protocol_version"], 2)
        self.assertFalse(capabilities["speech_recognition"])
        with self.assertRaises(HTTPException) as error:
            await self.routes.get_challenge(exam_id=9, user_info=user, client_protocol=1)
        self.assertEqual(error.exception.status_code, 426)
        issued = await self.routes.get_challenge(exam_id=9, user_info=user, client_protocol=2)
        class Audio:
            async def read(self, limit): return b"\0" * 32000
        class Analyzer:
            def analyze(self, data, sample_rate):
                assert sample_rate == 16000
                return {"speech_detected": True, "speech_seconds": .8, "violation": False}
        result = await self.routes.verification_audio(issued["challenge_id"], Audio(), user, Analyzer())
        self.assertEqual(set(result), {"speech_detected", "speech_seconds"})
        for owner in ({"id": str(uuid4())},):
            with self.assertRaises(HTTPException):
                await self.routes.verification_audio(issued["challenge_id"], Audio(), owner, Analyzer())
        class BadAudio:
            async def read(self, limit): return b"\0" * 64001
        with self.assertRaises(HTTPException):
            await self.routes.verification_audio(issued["challenge_id"], BadAudio(), user, Analyzer())
        with closing(sqlite3.connect(os.environ["VERIFICATION_STATE_PATH"])) as db, db:
            db.execute("UPDATE challenges SET used=1 WHERE id=?", (issued["challenge_id"],))
        with self.assertRaises(HTTPException):
            await self.routes.verification_audio(issued["challenge_id"], Audio(), user, Analyzer())

    async def test_replay_and_bad_quality_cannot_preserve_motion(self):
        from pathlib import Path
        import json
        user = {"id": str(uuid4())}
        Path(_temporary.name, user["id"]+".jpg").write_bytes(b"reference")
        issued = await self.routes.get_challenge(exam_id=9, user_info=user)
        class Identity:
            def compare(self, reference, current, check_spoof=True): return {"match": True}
        class Pose:
            quality = None
            def analyze_continuous(self, content):
                return dict(has_face=True, quality_reason=self.quality, yaw=0., gesture_pitch=0., smile=0., mouth=0., eyes_closed=0.)
        class Frame:
            def __init__(self, content): self.content = content
            async def read(self, limit): return self.content
        pose = Pose()
        async def verify(content):
            return await self.routes.verify_identity_and_liveness(issued["task"], issued["challenge_id"], Frame(content), user, Identity(), pose, None)
        await verify(b"first")
        await verify(b"second")
        replay = await verify(b"first")
        self.assertEqual(replay["reason"], "fresh_frame_required")
        with closing(sqlite3.connect(os.environ["VERIFICATION_STATE_PATH"])) as db:
            motion = json.loads(db.execute("SELECT motion FROM challenges WHERE id=?", (issued["challenge_id"],)).fetchone()[0])
            self.assertEqual(len(motion["samples"]), 2)
        pose.quality = "too_dark"
        result = await verify(b"dark")
        self.assertEqual(result["stage"], "quality")
        self.assertNotIn("admission_token", result)
        with closing(sqlite3.connect(os.environ["VERIFICATION_STATE_PATH"])) as db:
            self.assertEqual(db.execute("SELECT motion FROM challenges WHERE id=?", (issued["challenge_id"],)).fetchone()[0], "{}")

    async def test_live_checkpoints_are_required_before_and_after_motion(self):
        import json
        from pathlib import Path
        user = {"id": str(uuid4())}
        Path(_temporary.name,user["id"]+".jpg").write_bytes(b"reference")
        with patch.object(self.routes,"new_sequence",return_value=["turn_left"]):
            issued=await self.routes.get_challenge(exam_id=9,user_info=user)
        class Pose:
            yaw=0.
            def analyze_continuous(self, content):
                return dict(has_face=True,yaw=self.yaw,gesture_pitch=0.,smile=0.,mouth=0.,eyes_closed=0.)
        pose=Pose()
        class Identity:
            reject=True
            mismatch=False
            checks=[]
            def compare(self, reference, current, check_spoof=True):
                self.checks.append(check_spoof)
                if self.mismatch or abs(pose.yaw) > .1: return {"match":False,"reason":"face_mismatch"}
                if check_spoof and (self.reject or abs(pose.yaw)>.1):
                    return {"match":False,"reason":"spoof_suspected"}
                return {"match":True}
        identity=Identity()
        class Frame:
            def __init__(self, data):self.data=data
            async def read(self, limit):return self.data
        tick=time.time()-10;counter=0
        async def verify(content=None):
            nonlocal tick,counter
            tick+=.5;counter+=1
            with patch.object(self.routes.time,"time",return_value=tick):
                return await self.routes.verify_identity_and_liveness("turn_left",issued["challenge_id"],Frame(content or str(counter).encode()),user,identity,pose,None)
        # No initial passive liveness success: no cached progress or admission.
        first=await verify()
        self.assertEqual(first["reason"],"spoof_suspected")
        self.assertNotIn("admission_token",first)
        identity.reject=False
        for _ in range(3): await verify()
        checkpoint_count = len(identity.checks)
        pose.yaw=-.15
        for _ in range(2): result=await verify()
        self.assertEqual(result["reason"],"return_neutral")
        # Neither calibration nor movement waits for repeated biometric inference.
        self.assertEqual(checkpoint_count, 2)  # rejected initial + successful initial
        self.assertEqual(len(identity.checks), checkpoint_count)
        pose.yaw=0.;identity.reject=True
        await verify();result=await verify()
        self.assertEqual(result["reason"],"confirm_live_face")
        self.assertNotIn("admission_token",result)
        identity.reject=False
        identity.mismatch=True
        self.assertFalse((await verify())["verified"])
        identity.mismatch=False
        replay=await verify(b"2")
        self.assertEqual(replay["reason"],"fresh_frame_required")
        self.assertNotIn("admission_token",replay)
        result=await verify()
        self.assertTrue(result["verified"])
        self.assertIn("admission_token",result)
        self.assertTrue(identity.checks[-1])

    async def test_speech_requires_correct_phrase_and_final_live_face(self):
        import json
        from pathlib import Path
        user = {"id": str(uuid4())}
        Path(_temporary.name, user["id"] + ".jpg").write_bytes(b"reference")
        speech = types.SimpleNamespace(available=True, transcribe=lambda data: {"transcript": "не та фраза", "confidence": .99})
        with patch.object(self.routes, "get_phoneme_analyzer", return_value=speech):
            issued = await self.routes.get_challenge(exam_id=9, user_info=user, client_protocol=4)
            with closing(sqlite3.connect(os.environ["VERIFICATION_STATE_PATH"])) as db, db:
                # Isolate the final speech step; other tests exercise gesture transitions.
                db.execute("UPDATE challenges SET task='say_phrase',sequence=?,step=0 WHERE id=?", (json.dumps(["say_phrase"]), issued["challenge_id"]))
                phrase = db.execute("SELECT phrase FROM challenges WHERE id=?", (issued["challenge_id"],)).fetchone()[0]
            class Frame:
                async def read(self, limit): return str(uuid4()).encode()
            class Audio:
                async def read(self, limit): return str(uuid4()).encode() * 800
            class VAD:
                def analyze(self, data, rate): return {"speech_detected": True, "speech_seconds": .8}
            class Identity:
                spoof = False
                def compare(self, *args, check_spoof=True):
                    return {"match": not (check_spoof and self.spoof), "reason": "spoof_suspected"}
            class Pose:
                def analyze_continuous(self, content):
                    return dict(has_face=True, yaw=0., gesture_pitch=0., smile=0., mouth=0., eyes_closed=0.)
            identity = Identity()
            async def frame():
                return await self.routes.verify_identity_and_liveness("say_phrase", issued["challenge_id"], Frame(), user, identity, Pose(), None)
            async def audio():
                return await self.routes.verification_audio(issued["challenge_id"], Audio(), user, VAD())
            self.assertFalse((await audio())["speech_ready"])
            for _ in range(3): await frame()
            self.assertFalse((await audio())["phrase_matched"])
            self.assertFalse((await frame())["verified"])
            speech.transcribe = lambda data: {"phonemes": [{"phone": p, "confidence": .4} for s in phrase.split() for p in s.lower()]}
            self.assertFalse((await audio())["phrase_matched"])
            speech.transcribe = lambda data: {"phonemes": [{"phone": p, "confidence": .99} for s in phrase.split() for p in s.lower()]}
            spoken = await audio()
            self.assertTrue(spoken["phrase_matched"])
            self.assertNotIn("admission_token", spoken)
            identity.spoof = True
            self.assertFalse((await frame())["verified"])
            identity.spoof = False
            self.assertTrue((await frame())["verified"])
            with self.assertRaises(HTTPException): await audio()

    async def test_brief_blur_preserves_completed_motion_but_face_loss_clears_it(self):
        import json
        from pathlib import Path
        user = {"id": str(uuid4())}
        Path(_temporary.name, user["id"] + ".jpg").write_bytes(b"reference")
        issued = await self.routes.get_challenge(exam_id=9, user_info=user)
        baseline = dict(yaw=0., gesture_pitch=0., smile=0., mouth=0., eyes_closed=0.)
        state = dict(live_until=time.time()+90, last_seen=time.time(), baseline=baseline, phase="return")
        with closing(sqlite3.connect(os.environ["VERIFICATION_STATE_PATH"])) as db, db:
            db.execute("UPDATE challenges SET motion=? WHERE id=?", (json.dumps(state), issued["challenge_id"]))
        class Frame:
            async def read(self, limit): return str(uuid4()).encode()
        class Pose:
            quality = "too_blurry"
            def analyze_continuous(self, content): return {**baseline, "has_face": True, "quality_reason": self.quality}
        pose = Pose()
        async def verify():
            return await self.routes.verify_identity_and_liveness(issued["task"], issued["challenge_id"], Frame(), user, None, pose, None)
        self.assertEqual((await verify())["reason"], "quality_paused")
        with closing(sqlite3.connect(os.environ["VERIFICATION_STATE_PATH"])) as db:
            saved = json.loads(db.execute("SELECT motion FROM challenges WHERE id=?", (issued["challenge_id"],)).fetchone()[0])
        self.assertEqual(saved["phase"], "return")
        pose.quality = "multiple_faces"
        self.assertFalse((await verify())["verified"])
        with closing(sqlite3.connect(os.environ["VERIFICATION_STATE_PATH"])) as db:
            self.assertEqual(db.execute("SELECT motion FROM challenges WHERE id=?", (issued["challenge_id"],)).fetchone()[0], "{}")

    async def test_expired_anchor_requires_a_new_identity_match(self):
        import json
        from pathlib import Path
        user={"id":str(uuid4())}
        Path(_temporary.name,user["id"]+".jpg").write_bytes(b"reference")
        issued=await self.routes.get_challenge(exam_id=9,user_info=user)
        class Identity:
            match=True
            def compare(self, reference, current, check_spoof=True): return {"match":self.match,"reason":None if self.match else "face_mismatch"}
        class Pose:
            def analyze_continuous(self, content):return dict(has_face=True,yaw=0.,gesture_pitch=0.,smile=0.,mouth=0.,eyes_closed=0.)
        class Frame:
            async def read(self, limit):return str(uuid4()).encode()
        identity=Identity()
        async def verify():
            return await self.routes.verify_identity_and_liveness(issued["task"],issued["challenge_id"],Frame(),user,identity,Pose(),None)
        await verify();identity.match=False
        with closing(sqlite3.connect(os.environ["VERIFICATION_STATE_PATH"])) as db, db:
            state = json.loads(db.execute("SELECT motion FROM challenges WHERE id=?", (issued["challenge_id"],)).fetchone()[0])
            state["live_until"] = time.time()-1
            db.execute("UPDATE challenges SET motion=? WHERE id=?", (json.dumps(state), issued["challenge_id"]))
        result=await verify()
        self.assertFalse(result["verified"])
        with closing(sqlite3.connect(os.environ["VERIFICATION_STATE_PATH"])) as db:
            state=json.loads(db.execute("SELECT motion FROM challenges WHERE id=?",(issued["challenge_id"],)).fetchone()[0])
        self.assertEqual(state,{})

    async def test_challenge_cannot_change_owner_task_or_age(self):
        user = {"id": str(uuid4()), "login": "test"}
        issued = await self.routes.get_challenge(exam_id=9, user_info=user)
        class Frame:
            async def read(self, limit): return b"unique-image"
        for task, owner in [("presence_check",user), (issued["task"], {"id": str(uuid4())})]:
            with self.assertRaises(HTTPException):
                await self.routes.verify_identity_and_liveness(task, issued["challenge_id"], Frame(), owner, None, None, None)
        with closing(sqlite3.connect(os.environ["VERIFICATION_STATE_PATH"])) as db, db:
            db.execute("UPDATE challenges SET expires=1 WHERE id=?", (issued["challenge_id"],))
        with self.assertRaises(HTTPException):
            await self.routes.verify_identity_and_liveness(issued["task"], issued["challenge_id"], Frame(), user, None, None, None)

    async def test_expired_checkpoint_refresh_keeps_completed_movement(self):
        import json
        from pathlib import Path
        user = {"id":str(uuid4())}
        Path(_temporary.name,user["id"]+'.jpg').write_bytes(b'reference')
        with patch.object(self.routes,'new_sequence',return_value=['turn_left']):
            issued = await self.routes.get_challenge(exam_id=9,user_info=user)
        baseline = dict(yaw=0.,gesture_pitch=0.,smile=0.,mouth=0.,eyes_closed=0.)
        tick = time.time()
        state = dict(live_until=tick-1,last_seen=tick,baseline=baseline,phase='return')
        with closing(sqlite3.connect(os.environ['VERIFICATION_STATE_PATH'])) as db, db:
            db.execute('UPDATE challenges SET motion=? WHERE id=?',(json.dumps(state),issued['challenge_id']))
        class Frame:
            async def read(self,limit): return str(uuid4()).encode()
        class Pose:
            def analyze_continuous(self,content): return dict(has_face=True,**baseline)
        class Identity:
            def compare(self,*args,check_spoof=True): return {'match':True}
        with patch.object(self.routes.time,'time',return_value=tick):
            result = await self.routes.verify_identity_and_liveness('turn_left',issued['challenge_id'],Frame(),user,Identity(),Pose(),None)
        self.assertFalse(result['verified'])
        with closing(sqlite3.connect(os.environ['VERIFICATION_STATE_PATH'])) as db:
            saved = json.loads(db.execute('SELECT motion FROM challenges WHERE id=?',(issued['challenge_id'],)).fetchone()[0])
        self.assertEqual(saved['phase'],'return'); self.assertIn('baseline',saved)
        with patch.object(self.routes.time,'time',return_value=tick+.5):
            result = await self.routes.verify_identity_and_liveness('turn_left',issued['challenge_id'],Frame(),user,Identity(),Pose(),None)
        self.assertTrue(result['verified'])

    async def test_admission_requires_transition_and_is_consumed(self):
        from pathlib import Path
        user = {"id": str(uuid4()), "login": "test"}
        Path(_temporary.name,user["id"]+".jpg").write_bytes(b"reference")
        with patch.object(self.routes, "new_sequence", return_value=["turn_left", "smile", "close_eyes"]):
            issued = await self.routes.get_challenge(exam_id=9,user_info=user)
        class Identity:
            calls = 0
            def compare(self, reference, current, check_spoof=True):
                self.calls += 1
                return {"match": True,"distance": 0.2}
        identity = Identity()
        class Pose:
            values = dict(has_face=True, yaw=0., gesture_pitch=0., smile=0., mouth=0., eyes_closed=0.)
            def analyze_continuous(self, content): return self.values.copy()
        class Frame:
            def __init__(self, content): self.content=content
            async def read(self, limit): return self.content
        pose=Pose()
        tick = time.time() - 30
        counter = 0
        task = issued["task"]
        async def verify(content=None):
            nonlocal tick, counter
            tick += .5
            counter += 1
            with patch.object(self.routes.time, "time", return_value=tick):
                return await self.routes.verify_identity_and_liveness(task,issued["challenge_id"],Frame(content or str(counter).encode()),user,identity,pose,None)
        for current_task, feature, target in [("turn_left", "yaw", -.15), ("smile", "smile", .8), ("close_eyes", "eyes_closed", .9)]:
            task = current_task
            for _ in range(3):
                self.assertFalse((await verify())["verified"])
            pose.values[feature] = target
            self.assertFalse((await verify())["verified"])
            held = await verify()
            self.assertEqual(held["reason"], "return_neutral")
            self.assertNotIn("admission_token", held)
            pose.values[feature] = 0.
            self.assertFalse((await verify())["verified"])
            result = await verify()
            if task != "close_eyes":
                self.assertFalse(result["verified"])
                self.assertIn("next_task", result)
                with self.assertRaises(HTTPException): await verify()
        self.assertTrue(result["verified"])
        self.assertEqual(identity.calls, 4)  # one initial + one final checkpoint per action
        admission=jwt.decode(result["admission_token"],hmac.new(self.routes.settings.ai_api_key.encode(),b"admission:v1",hashlib.sha256).digest(),algorithms=["HS256"],issuer="ContractIssuer",audience="ExamGuardianAdmission")
        self.assertEqual(admission["exam_id"],"9")
        self.assertEqual(admission["sub"],user["id"])
        with self.assertRaises(HTTPException): await verify(b"replayed")

if __name__ == "__main__": unittest.main(verbosity=2)
