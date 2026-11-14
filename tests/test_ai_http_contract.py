"""Real-model HTTP smoke test against the explicitly isolated Core + AI stack.

Blank camera/silent audio verify wiring and error handling, not recognition accuracy.
"""
import json
import os
import time
import unittest
import urllib.error
import urllib.request
from pathlib import Path
import test_core_contract as contracts
from test_core_contract import request

AI=os.environ.get('EXAMGUARDIAN_TEST_AI_URL','')

def call(path, token=None, capability=None, data=None, content_type='application/json'):
    headers={'Content-Type':content_type}
    if token: headers['Authorization']='Bearer '+token
    if capability: headers['X-Session-Token']=capability
    req=urllib.request.Request(AI+path,data,headers,method='POST' if data is not None else 'GET')
    try:
        with urllib.request.urlopen(req,timeout=30) as result:
            return result.status,json.loads(result.read())
    except urllib.error.HTTPError as result:
        return result.code,result.read()

def file_body(content, filename):
    boundary='real-inference-contract'
    body=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{filename}"\r\nContent-Type: application/octet-stream\r\n\r\n').encode()+content+f'\r\n--{boundary}--\r\n'.encode()
    return body,'multipart/form-data; boundary='+boundary

@unittest.skipUnless(AI=='http://127.0.0.1:55112' and os.environ.get('EXAMGUARDIAN_TEST_URL')=='http://127.0.0.1:55111','Requires isolated real-model test services')
class RealInferenceContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        contracts.CoreContracts.setUpClass()
        cls.fixture=contracts.CoreContracts(); cls.fixture.setUp()
        status,cls.session=cls.fixture.start()
        assert status==200,cls.session
        cls.token=cls.fixture.students[0][1]
        cls.capability=cls.session['attemptToken']
        cls.frame=Path(os.environ['EXAMGUARDIAN_TEST_FRAME']).read_bytes()

    def test_authentication_and_attempt_binding(self):
        data,kind=file_body(self.frame,'blank.jpg')
        path=f"/analyze/frame?session_id={self.session['id']}&relative_ts=0"
        self.assertEqual(call(path,data=data,content_type=kind)[0],401)
        self.assertEqual(call(path,self.token,data=data,content_type=kind)[0],422)
        self.assertEqual(call(path,self.token,'invalid',data,kind)[0],403)
        self.assertEqual(call(path,self.fixture.students[1][1],self.capability,data,kind)[0],403)

    def test_blank_frame_receipt_and_durable_observation(self):
        data,kind=file_body(self.frame,'blank.jpg'); sid=self.session['id']
        for _ in range(3):
            status,result=call(f'/analyze/frame?session_id={sid}&relative_ts=0',self.token,self.capability,data,kind)
            self.assertEqual(status,200,result); self.assertFalse(result['has_face'])
        _,session=request(f'/api/ExamSessions/{sid}',token=self.fixture.teacher)
        self.assertIsNotNone(session['lastAnalysisAt'])
        events=[]
        for _ in range(20):
            _,events=request(f'/api/Violations/by-session/{sid}',token=self.fixture.teacher)
            if any(v['violationType']=='face_missing' for v in events): break
            time.sleep(.2)
        self.assertTrue(any(v['violationType']=='face_missing' for v in events),events)
        evidence=next(v for v in events if v['violationType']=='face_missing')
        self.assertEqual(request(evidence['evidenceUrl'],token=self.fixture.teacher)[0],200)
        self.assertEqual(request(f'/api/ExamSessions/{sid}/preview',token=self.fixture.teacher)[0],200)

    def test_silence_receipt(self):
        data,kind=file_body(bytes(16000*2*5),'silence.pcm'); sid=self.session['id']
        status,result=call(f'/analyze/audio?session_id={sid}&relative_ts=0&sample_rate=16000',self.token,self.capability,data,kind)
        self.assertEqual(status,200,result)
        self.assertFalse(result['speech_detected'])
        _,session=request(f'/api/ExamSessions/{sid}',token=self.fixture.teacher)
        self.assertIsNotNone(session['lastAudioAt'])

if __name__=='__main__': unittest.main(verbosity=2)
