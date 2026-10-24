"""Integration contracts against a disposable ExamGuardian API/database.

Set EXAMGUARDIAN_TEST_URL and EXAMGUARDIAN_TEST_KEY explicitly. Never use a
working deployment: these tests create their own students, exams and evidence.
"""
import csv
import io
import zipfile
import datetime
import base64
import concurrent.futures
import hashlib
import hmac
import json
import os
import subprocess
from pathlib import Path
import time
import unittest
import urllib.error
import urllib.request
from uuid import uuid4

URL = os.environ.get("EXAMGUARDIAN_TEST_URL", "")
KEY = os.environ.get("EXAMGUARDIAN_TEST_KEY", "")

def jwt_token(claims, key=KEY):
    def encode(value):
        return base64.urlsafe_b64encode(json.dumps(value, separators=(",", ":")).encode()).rstrip(b"=")
    content = encode({"alg": "HS256", "typ": "JWT"}) + b"." + encode(claims)
    signature = base64.urlsafe_b64encode(hmac.new(key.encode(), content, hashlib.sha256).digest()).rstrip(b"=")
    return (content + b"." + signature).decode()

CAPABILITIES = {}

def request(path, method="GET", data=None, token=None, content_type="application/json", capabilities=True, extra_headers=None):
    body = json.dumps(data).encode() if data is not None and not isinstance(data, bytes) else data
    headers = {"Content-Type": content_type}
    if token: headers["Authorization"] = "Bearer " + token
    if capabilities and token in CAPABILITIES: headers.update(CAPABILITIES[token])
    if extra_headers: headers.update(extra_headers)
    req = urllib.request.Request(URL + path, body, headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=20) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw and response.headers.get_content_type() == "application/json" else raw
    except urllib.error.HTTPError as response:
        raw = response.read()
        try: raw = json.loads(raw)
        except ValueError: pass
        return response.code, raw

def upload_video(sid, index, token, stream="camera", content=None):
    if content is None: content=Path(os.environ["EXAMGUARDIAN_TEST_VIDEO"]).read_bytes()
    boundary="examguardian-contract"
    fields={"sessionId":sid,"chunkIndex":index,"stream":stream,"startOffsetSeconds":0,"durationSeconds":1}
    body=b"".join((f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n').encode() for key,value in fields.items())
    body+=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="clip.mp4"\r\nContent-Type: video/mp4\r\n\r\n').encode()+content+f"\r\n--{boundary}--\r\n".encode()
    return request("/api/video/upload-chunk","POST",body,token,f"multipart/form-data; boundary={boundary}")

@unittest.skipUnless(URL == "http://127.0.0.1:55111" and KEY, "Requires explicitly configured disposable test API")
class CoreContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        status, login = request("/api/Auth/login", "POST", {"login": "admin", "password": os.environ.get("EXAMGUARDIAN_TEST_PASSWORD", "password12345")})
        assert status == 200, login
        cls.teacher = login["response"]["accessToken"]

    def setUp(self):
        self.students = []
        for _ in range(2):
            login = "test_" + uuid4().hex
            password = "Integration-Test-Password-2026!"
            status, student = request("/api/Students", "POST", {"fullName": "Contract participant", "login": login, "password": password, "group": "TEST", "role": "Student"}, self.teacher)
            self.assertEqual(status, 201, student)
            status, auth = request("/api/Auth/login", "POST", {"login": login, "password": password})
            self.assertEqual(status, 200, auth)
            self.students.append((student["id"], auth["response"]["accessToken"], login, password))
        status, self.exam = request("/api/Exams", "POST", {"title": "Contract " + uuid4().hex, "durationMinutes": 60,
            "questions": [{"text": "Select the first answer", "points": 2, "correctIndex": 0, "options": ["First", "Second"]}]}, self.teacher)
        self.assertEqual(status, 201, self.exam)

    def admission(self, student=0, exam_id=None, **overrides):
        claims = {"sub": self.students[student][0], "exam_id": str(exam_id or self.exam["id"]), "purpose": "admission",
            "jti": uuid4().hex, "iss": "ExamGuardianTest", "aud": "ExamGuardianAdmission", "exp": int(time.time()) + 120}
        claims.update(overrides)
        return jwt_token(claims)

    def start(self, student=0, **overrides):
        body = {"examId": self.exam["id"], "admissionToken": self.admission(student)}
        body.update(overrides)
        status, result = request("/api/ExamSessions/start", "POST", body, self.students[student][1])
        if status == 200:
            CAPABILITIES[self.students[student][1]] = {"X-Session-Token": result["attemptToken"], "X-Evidence-Token": result["evidenceToken"]}
        return status, result

    def test_qti_preview_validates_package_and_does_not_create_exam(self):
        xml = (Path(__file__).parent / 'fixtures/qti-single-choice.xml').read_bytes()
        def upload(content, name='item.xml', token=None):
            boundary = 'qti-test'
            body = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{name}"\r\nContent-Type: application/octet-stream\r\n\r\n').encode() + content + f'\r\n--{boundary}--\r\n'.encode()
            return request('/api/exams/import-qti', 'POST', body, token or self.teacher, f'multipart/form-data; boundary={boundary}')
        _, before = request('/api/Exams', token=self.teacher)
        status, preview = upload(xml)
        self.assertEqual(status, 200, preview); self.assertTrue(preview['canImport'])
        self.assertEqual(preview['questions'][0]['correctIndex'], 1)
        self.assertEqual(preview['questions'][0]['text'], 'Which number is even?')
        self.assertEqual(preview['questions'][0]['options'], ['3', '4'])
        _, after = request('/api/Exams', token=self.teacher)
        self.assertEqual(len(before), len(after))
        self.assertEqual(upload(xml, token=self.students[0][1])[0], 403)
        for invalid in [xml.replace(b'maxChoices="1"', b'maxChoices="2"'), xml.replace(b'match_correct', b'map_response'), xml.replace(b'<value>B</value>', b'<value>X</value>'), xml.replace(b'<p>', b'<p><img src="image.png"/>')]:
            status, result = upload(invalid)
            self.assertEqual(status, 200, result); self.assertFalse(result['canImport']); self.assertTrue(result['issues'])
        self.assertEqual(upload(b'<!DOCTYPE x [<!ENTITY e SYSTEM "file:///etc/passwd">]><x>&e;</x>')[0], 400)
        manifest = b'<manifest xmlns="http://www.imsglobal.org/xsd/imscp_v1p1"><resources><resource identifier="item" type="imsqti_item_xmlv2p1" href="items/item.xml"/></resources></manifest>'
        def package(extra=False):
            stream = io.BytesIO()
            with zipfile.ZipFile(stream, 'w') as archive:
                archive.writestr('imsmanifest.xml', manifest)
                archive.writestr('items/item.xml', xml)
                if extra: archive.writestr('../outside.xml', xml)
            return stream.getvalue()
        status, preview = upload(package(), 'items.zip')
        self.assertEqual(status, 200, preview); self.assertTrue(preview['canImport'])
        self.assertEqual(upload(package(True), 'items.zip')[0], 400)
        self.assertEqual(upload(b'not a zip', 'items.zip')[0], 400)

    def test_qti_true_false_scores_through_normal_exam_flow(self):
        xml = (Path(__file__).parent / 'fixtures/qti-true-false.xml').read_bytes()
        boundary = 'qti-boolean'
        body = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="boolean.xml"\r\n\r\n').encode() + xml + f'\r\n--{boundary}--\r\n'.encode()
        status, preview = request('/api/exams/import-qti', 'POST', body, self.teacher, f'multipart/form-data; boundary={boundary}')
        self.assertEqual(status,200,preview); self.assertTrue(preview['canImport'])
        status, self.exam = request('/api/Exams','POST',{'title':'True / false contract','durationMinutes':60,'questions':preview['questions']},self.teacher)
        self.assertEqual(status,201,self.exam)
        _, session = self.start(); question = self.exam['questions'][0]['id']
        status, result = request(f"/api/Exams/{self.exam['id']}/submit",'POST',{'sessionId':session['id'],'answers':{question:0}},self.students[0][1])
        self.assertEqual(status,200,result); self.assertEqual(result['score'],100)

    def test_grade_export_latest_attempt_and_teacher_scope(self):
        _, session = self.start(); sid = session['id']; token = self.students[0][1]
        self.assertEqual(request(f"/api/Exams/{self.exam['id']}/submit", 'POST', {'sessionId':sid,'answers':{}}, token)[0], 200)
        def sql(query):
            subprocess.run(['docker','exec',os.environ['EXAMGUARDIAN_TEST_DB_CONTAINER'],'psql','-U','tester','-d','examguardian_test','-c',query], check=True, capture_output=True)
        # Isolated fixture state for export selection; the real evidence/review flow is covered separately.
        sql(f"""UPDATE "Sessions" SET "IntegrityVerdict"='Accepted', "EvidenceStatus"='Delivered' WHERE "Id"={sid}; UPDATE "Exams" SET "MaxAttempts"=2 WHERE "Id"={self.exam['id']};""")
        export = f"/api/Exams/{self.exam['id']}/grades?group=TEST"
        status, data = request(export, token=self.teacher)
        self.assertEqual(status, 200, data)
        status, catalog = request('/api/grade-exports', token=self.teacher)
        self.assertEqual(status, 200, catalog)
        exported = [exam for group in catalog if group['group'] == 'TEST' for exam in group['exams'] if exam['examId'] == self.exam['id']]
        self.assertEqual(len(exported), 1); self.assertEqual(exported[0]['eligibleCount'], 1)
        self.assertEqual(request('/api/grade-exports', token=token)[0], 403)
        self.assertEqual(request(f"/api/Exams/{self.exam['id']}/grades", token=self.teacher)[0], 400)
        self.assertEqual(request(f"/api/Exams/{self.exam['id']}/grades?group=OTHER", token=self.teacher)[0], 409)
        _, other_session = self.start(student=1)
        self.assertEqual(request(f"/api/Exams/{self.exam['id']}/submit",'POST',{'sessionId':other_session['id'],'answers':{}},self.students[1][1])[0],200)
        sql(f"""UPDATE "Sessions" SET "IntegrityVerdict"='Accepted', "EvidenceStatus"='Delivered' WHERE "Id"={other_session['id']}; UPDATE "Users" SET "Group"='OTHER' WHERE "Id"='{self.students[1][0]}';""")
        _, original_csv = request(export, token=self.teacher)
        self.assertEqual([r['username'] for r in csv.DictReader(io.StringIO(original_csv.decode()))], [self.students[0][2]])
        _, other_csv = request(f"/api/Exams/{self.exam['id']}/grades?group=OTHER", token=self.teacher)
        self.assertEqual([r['username'] for r in csv.DictReader(io.StringIO(other_csv.decode()))], [self.students[1][2]])
        sql(f"""UPDATE "Users" SET "Group"=NULL WHERE "Id"='{self.students[1][0]}';""")
        self.assertEqual(request(f"/api/Exams/{self.exam['id']}/grades?group=", token=self.teacher)[0],200)

        other_teacher = jwt_token({'id':str(uuid4()), 'role':'Examiner','iss':'ExamGuardianTest','aud':'ExamGuardianTestClient','exp':int(time.time())+120})
        self.assertEqual(request(export, token=other_teacher)[0], 404)
        sql(f"""UPDATE "Sessions" SET "EvidenceStatus"='Expired' WHERE "Id"={sid};""")
        self.assertEqual(request(export, token=self.teacher)[0], 200)

        status, latest = self.start()
        self.assertEqual(status, 200, latest); self.assertNotEqual(latest['id'], sid)
        self.assertEqual(request(export, token=self.teacher)[0], 409)
        _, catalog = request('/api/grade-exports', token=self.teacher)
        self.assertFalse(any(exam['examId'] == self.exam['id'] for group in catalog if group['group'] == 'TEST' for exam in group['exams']))

    @unittest.skipIf(os.environ.get("EXAMGUARDIAN_TEST_DEVELOPMENT") == "true", "Production artifact contract")
    def test_production_artifact_has_no_development_admission(self):
        token=self.students[0][1]
        status,caps=request('/api/system/capabilities',token=token)
        self.assertEqual(status,200,caps); self.assertFalse(caps['developmentToolsAvailable'])
        self.assertEqual(caps['allowedOverrides'],[])
        self.assertEqual(request('/api/development/admissions','POST',{'examId':self.exam['id'],'overrides':['vmGate']},token)[0],404)
        admission=jwt_token({'sub':self.students[0][0],'exam_id':str(self.exam['id']),'purpose':'development-admission','jti':uuid4().hex,'overrides':'["vmGate"]','iss':'ExamGuardianTest','aud':'ExamGuardianDevelopmentAdmission','exp':int(time.time())+120})
        self.assertEqual(self.start(admissionToken=admission)[0],400)

    @unittest.skipUnless(os.environ.get("EXAMGUARDIAN_TEST_DEVELOPMENT") == "true", "DEV artifact contract")
    def test_development_identity_bypass_is_scoped_and_not_exportable(self):
        token = self.students[0][1]
        _, caps = request('/api/system/capabilities',token=token)
        self.assertTrue(caps['developmentToolsAvailable']); self.assertEqual(caps['allowedOverrides'], ['biometricAdmission'])
        body = {'examId':self.exam['id'],'overrides':['biometricAdmission']}
        path = '/api/development/admissions'
        self.assertEqual(request(path,'POST',body)[0],401)
        self.assertEqual(request(path,'POST',body,self.teacher)[0],403)
        self.assertEqual(request(path,'POST',dict(body,overrides=['vmGate']),token)[0],400)
        status, admission = request(path,'POST',body,token)
        self.assertEqual(status,200,admission)
        self.assertEqual(self.start(student=1,admissionToken=admission['admissionToken'])[0],400)
        status, session = self.start(admissionToken=admission['admissionToken'])
        self.assertEqual(status,200,session); self.assertTrue(session['identityVerificationSkipped'])
        # Normal verification on resume must not erase the DEV marker.
        status, resumed = self.start()
        self.assertEqual(status,200,resumed); self.assertTrue(resumed['identityVerificationSkipped'])
        sid = session['id']
        self.assertEqual(request(f"/api/Exams/{self.exam['id']}/submit",'POST',{'sessionId':sid,'answers':{}},token)[0],200)
        status, error = request(f'/api/ExamSessions/{sid}/review','POST',{'verdict':'Accepted','notes':'test'},self.teacher)
        self.assertEqual(status,409,error); self.assertIn('identity',error['message'])
        self.assertEqual(request(f"/api/Exams/{self.exam['id']}/grades?group=TEST",token=self.teacher)[0],409)
        _, catalog = request('/api/grade-exports',token=self.teacher)
        self.assertTrue(any(reason['code']=='identitySkipped' for group in catalog for reason in group['exclusions']))

    def test_client_cannot_set_test_markers_on_normal_admission(self):
        status,session=self.start(isTest=True,testPolicyJson='["vmGate"]')
        self.assertEqual(status,200,session); self.assertNotIn('isTest',session);self.assertNotIn('testPolicyJson',session)

    def test_no_anonymous_materials_or_review(self):
        for path in ["/api/ExamSessions", "/api/Violations/unreviewed", "/api/video/1/chunks"]:
            self.assertEqual(request(path)[0], 401, path)
        self.assertEqual(request("/api/Violations/report", "POST", {"examSessionId": 1, "violationType": "fake"})[0], 401)
        self.assertEqual(request("/api/Violations/1/review", "PATCH", {"decision": "Confirmed"})[0], 401)

    def test_admission_binding_and_signature(self):
        self.assertEqual(self.start(admissionToken="")[0], 400)
        self.assertEqual(self.start(admissionToken=self.admission(1))[0], 400)
        self.assertEqual(self.start(admissionToken=self.admission(exp=1))[0], 400)
        token = self.admission()
        self.assertEqual(self.start(admissionToken=token[:-4] + "aaaa")[0], 400)

    def test_student_id_cannot_impersonate(self):
        status, session = self.start(studentId=self.students[1][0])
        self.assertEqual(status, 200, session)
        self.assertEqual(session["studentId"], self.students[0][0])

    def test_parallel_start_is_one_attempt(self):
        with concurrent.futures.ThreadPoolExecutor(4) as pool:
            results = list(pool.map(lambda _: self.start(), range(4)))
        self.assertTrue(all(status == 200 for status, _ in results), results)
        self.assertEqual(len({body["id"] for _, body in results}), 1)
        self.assertEqual(len({datetime.datetime.fromisoformat(body["deadlineUtc"]) for _, body in results}), 1)

    def test_foreign_attempt_and_exam_are_rejected(self):
        _, session = self.start()
        sid = session["id"]
        other = self.students[1][1]
        self.assertEqual(request(f"/api/ExamSessions/{sid}", token=other)[0], 404)
        self.assertEqual(request(f"/api/ExamSessions/{sid}/answers", "PUT", {}, other)[0], 404)
        self.assertEqual(request(f"/api/Exams/{self.exam['id']}/submit", "POST", {"sessionId": sid, "answers": {}}, other)[0], 404)
        self.assertEqual(request(f"/api/Exams/{self.exam['id']+1000}/submit", "POST", {"sessionId": sid, "answers": {}}, self.students[0][1])[0], 400)
        self.assertEqual(request(f"/api/ExamSessions/{sid}/finish?score=999", "PATCH", {}, self.students[0][1])[0], 404)

    def test_restore_answers_and_freeze_result(self):
        _, session = self.start()
        sid = session["id"]
        answer = {str(self.exam["questions"][0]["id"]): 0}
        self.assertEqual(request(f"/api/ExamSessions/{sid}/answers", "PUT", answer, self.students[0][1])[0], 200)
        _, restored = self.start()
        self.assertEqual(restored["savedAnswers"], answer)
        self.assertEqual(datetime.datetime.fromisoformat(restored["deadlineUtc"]), datetime.datetime.fromisoformat(session["deadlineUtc"]))
        status, result = request(f"/api/Exams/{self.exam['id']}/submit", "POST", {"sessionId": sid, "answers": answer}, self.students[0][1])
        self.assertEqual(status, 200, result)
        self.assertEqual(result["score"], 100)
        _, repeated = request(f"/api/Exams/{self.exam['id']}/submit", "POST", {"sessionId": sid, "answers": {}}, self.students[0][1])
        self.assertEqual(repeated["score"], 100)
        self.assertEqual(self.start()[0], 409)

    def test_attempt_capability_required_and_rotated(self):
        _, session = self.start()
        sid = session["id"]
        token = self.students[0][1]
        self.assertEqual(request(f"/api/ExamSessions/{sid}/answers", "PUT", {}, token, capabilities=False)[0], 403)
        old = session["attemptToken"]
        _, resumed = self.start()
        self.assertNotEqual(old, resumed["attemptToken"])
        self.assertEqual(request(f"/api/ExamSessions/{sid}/answers", "PUT", {}, token, extra_headers={"X-Session-Token": old})[0], 403)
        self.assertEqual(request(f"/api/ExamSessions/{sid}/answers", "PUT", {}, token)[0], 200)
        self.assertEqual(request(f"/api/video/{sid}/chunks", token=token, capabilities=False)[0], 404)

    def test_concurrent_submissions_freeze_the_first_result(self):
        _,session=self.start(); sid=session["id"]
        question=str(self.exam["questions"][0]["id"])
        def submit(choice): return request(f"/api/Exams/{self.exam['id']}/submit","POST",{"sessionId":sid,"answers":{question:choice}},self.students[0][1])
        with concurrent.futures.ThreadPoolExecutor(8) as pool:
            results=list(pool.map(submit,[0,1,0,1,0,1,0,1]))
        self.assertTrue(all(status==200 for status,_ in results),results)
        self.assertEqual(len({result["score"] for _,result in results}),1,results)
        _,stored=request(f"/api/ExamSessions/{sid}",token=self.teacher)
        self.assertEqual(stored["score"],results[0][1]["score"])

    def test_proctor_termination_does_not_claim_a_calculated_score(self):
        _,session=self.start(); sid=session["id"]
        self.assertEqual(request(f"/api/ExamSessions/{sid}/terminate","POST",{},self.teacher)[0],200)
        status,result=request(f"/api/Exams/{self.exam['id']}/submit","POST",{"sessionId":sid,"answers":{}},self.students[0][1])
        self.assertEqual(status,200,result)
        self.assertFalse(result["session"]["scoreCalculated"])
        self.assertEqual(result["session"]["status"],"TerminatedByProctor")

    def test_invalid_answer_is_not_accepted(self):
        _, session = self.start()
        self.assertEqual(request(f"/api/ExamSessions/{session['id']}/answers", "PUT", {"999999": 0}, self.students[0][1])[0], 400)

    def test_review_verdicts_and_ownership(self):
        _, session = self.start()
        event = {"examSessionId": session["id"], "violationType": "prohibited_app", "confidence": 1, "relativeTimestampSeconds": 2, "eventId": str(uuid4())}
        status, observation = request("/api/Violations/report", "POST", event, self.students[0][1])
        self.assertEqual(status, 200, observation)
        _, duplicate = request("/api/Violations/report", "POST", event, self.students[0][1])
        self.assertEqual(duplicate["id"], observation["id"])
        self.assertEqual(request("/api/Violations/report", "POST", event, self.students[1][1])[0], 403)
        path = f"/api/Violations/{observation['id']}/review"
        self.assertEqual(request(path, "PATCH", {"decision": "Confirmed"}, self.students[0][1])[0], 403)
        for verdict in ["Confirmed", "Dismissed"]:
            self.assertEqual(request(path, "PATCH", {"decision": verdict, "notes": "Test reason"}, self.teacher)[0], 204)
            _, saved = request(f"/api/Violations/{observation['id']}", token=self.teacher)
            self.assertEqual(saved["reviewDecision"], verdict)
            self.assertIsNotNone(saved["reviewedBy"])

    def test_teacher_scope_and_no_capability_disclosure(self):
        _, session=self.start()
        sid=session["id"]
        alien=jwt_token({"id":str(uuid4()),"sub":str(uuid4()),"role":"Examiner","iss":"ExamGuardianTest","aud":"ExamGuardianTestClient","exp":int(time.time())+60})
        for path in [f"/api/ExamSessions/{sid}",f"/api/Exams/{self.exam['id']}",f"/api/video/{sid}/chunks"]:
            self.assertEqual(request(path,token=alien)[0],404)
        _, visible=request(f"/api/ExamSessions/{sid}",token=self.teacher)
        self.assertNotIn("attemptToken",visible)
        self.assertNotIn("evidenceToken",visible)
        self.assertNotIn("activeClientId",visible)

    def test_absolute_exam_window_and_late_arrival(self):
        now=datetime.datetime.now(datetime.timezone.utc)
        for start,expected in [(now-datetime.timedelta(days=1),409),(now-datetime.timedelta(minutes=30),200)]:
            body={"title":"Window contract","durationMinutes":60,"scheduledStartTime":start.isoformat(),"questions":[{"text":"Q","points":1,"correctIndex":0,"options":["A","B"]}]}
            status,exam=request('/api/Exams','POST',body,self.teacher)
            self.assertEqual(status,201,exam)
            status,session=self.start(examId=exam['id'],admissionToken=self.admission(exam_id=exam['id']))
            self.assertEqual(status,expected,session)
            if status==200:
                end=datetime.datetime.fromisoformat(session['deadlineUtc'].replace('Z','+00:00'))
                self.assertLess(abs((end-(start+datetime.timedelta(minutes=60))).total_seconds()),0.01)
            else:
                _,available=request('/api/Exams',token=self.students[0][1])
                self.assertNotIn(exam['id'],[x['id'] for x in available])

    def test_schedule_local_timezone_is_independent_of_browser_timezone(self):
        body={"title":"Timezone contract","durationMinutes":60,"scheduledStartLocal":"2026-10-15T02:05","timeZoneId":"Europe/Chisinau","questions":[{"text":"Q","points":1,"correctIndex":0,"options":["A","B"]}]}
        status,exam=request('/api/Exams','POST',body,self.teacher)
        self.assertEqual(status,201,exam)
        self.assertEqual(datetime.datetime.fromisoformat(exam['scheduledStartTime'].replace('Z','+00:00')),datetime.datetime(2026,10,14,23,5,tzinfo=datetime.timezone.utc))
        for local in ['2026-03-29T03:30','2026-10-25T03:30']:
            self.assertEqual(request('/api/Exams','POST',dict(body,scheduledStartLocal=local),self.teacher)[0],400)

    def test_group_and_schedule_gate_admission(self):
        for settings in [{"allowedGroup":"OTHER"},{"scheduledStartTime":(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(days=1)).isoformat()}]:
            body={"title":"Unavailable contract","durationMinutes":1,"questions":[{"text":"Question","points":1,"correctIndex":0,"options":["A","B"]}],**settings}
            status,exam=request("/api/Exams","POST",body,self.teacher)
            self.assertEqual(status,201,exam)
            self.assertEqual(self.start(examId=exam["id"],admissionToken=self.admission(exam_id=exam["id"]))[0],409)
            _, available=request("/api/Exams",token=self.students[0][1])
            if "allowedGroup" in settings:
                self.assertNotIn(exam["id"], [e["id"] for e in available])
            else:
                upcoming = next(e for e in available if e["id"] == exam["id"])
                self.assertEqual(upcoming["questions"], [])
                self.assertTrue(upcoming["scheduledStartTime"])
                self.assertTrue(upcoming["serverTimeUtc"])
                self.assertEqual(request(f"/api/Exams/{exam['id']}", token=self.students[0][1])[0], 404)

    def test_groups_creation_assignment_and_access(self):
        name = "GROUP-" + uuid4().hex
        self.assertEqual(request('/api/groups', 'POST', {'name': '  ' + name + '  '}, self.teacher)[0], 201)
        self.assertIn(name, request('/api/groups', token=self.teacher)[1])
        self.assertEqual(request('/api/groups', 'POST', {'name': name.lower()}, self.teacher)[0], 409)
        for invalid in ['', ' ' * 3, 'x' * 101]:
            self.assertEqual(request('/api/groups', 'POST', {'name': invalid}, self.teacher)[0], 400)
        student_id, token, *_ = self.students[0]
        self.assertEqual(request('/api/groups', token=token)[0], 403)
        self.assertEqual(request('/api/groups', 'POST', {'name': name+'other'}, token)[0], 403)
        self.assertEqual(request(f'/api/groups/students/{student_id}', 'PUT', {'name': name}, token)[0], 403)
        self.assertEqual(request(f'/api/groups/students/{student_id}', 'PUT', {'name': 'missing-'+name}, self.teacher)[0], 400)
        self.assertEqual(request(f'/api/groups/students/{student_id}', 'PUT', {'name': name}, self.teacher)[0], 204)
        self.assertEqual(request(f'/api/Students/{student_id}', token=self.teacher)[1]['group'], name)
        body = {'title':'Assigned future exam','allowedGroup':name,'durationMinutes':60,
                'scheduledStartTime':(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(hours=1)).isoformat(),
                'questions':[{'text':'Q','points':1,'correctIndex':0,'options':['A','B']}]}
        status, exam = request('/api/Exams','POST',body,self.teacher)
        self.assertEqual(status,201,exam)
        self.assertIn(exam['id'], [e['id'] for e in request('/api/Exams', token=token)[1]])
        self.assertNotIn(exam['id'], [e['id'] for e in request('/api/Exams', token=self.students[1][1])[1]])
        self.assertEqual(self.start()[0],200)
        self.assertEqual(request(f'/api/groups/students/{student_id}', 'PUT', {'name':'TEST'}, self.teacher)[0],409)

    def test_integrity_is_distinct_from_academic_score(self):
        _,session=self.start(); sid=session["id"]; token=self.students[0][1]
        question=str(self.exam["questions"][0]["id"])
        self.assertEqual(request(f"/api/Exams/{self.exam['id']}/submit","POST",{"sessionId":sid,"answers":{question:0}},token)[0],200)
        review=f"/api/ExamSessions/{sid}/review"
        self.assertEqual(request(review,"POST",{"verdict":"Rejected","notes":"No confirmed observation"},self.teacher)[0],409)
        self.assertEqual(request(review,"POST",{"verdict":"Accepted","notes":"Recordings missing"},self.teacher)[0],409)
        status,reviewed=request(review,"POST",{"verdict":"Inconclusive","notes":"Evidence incomplete"},self.teacher)
        self.assertEqual(status,200,reviewed); self.assertEqual(reviewed["score"],100)
        self.assertEqual(reviewed["integrityVerdict"],"Inconclusive")
        self.assertEqual(len(json.loads(reviewed["integrityHistoryJson"])),1)
        event={"examSessionId":sid,"eventId":str(uuid4()),"violationType":"app_switched","confidence":1,"relativeTimestampSeconds":0}
        self.assertEqual(request("/api/Violations/report","POST",event,token,capabilities=False,extra_headers={"X-Evidence-Token":session["evidenceToken"]})[0],200)
        _,changed=request(f"/api/ExamSessions/{sid}",token=self.teacher)
        self.assertEqual(changed["integrityVerdict"],"UnderReview")
        self.assertEqual(changed["score"],100)

    @unittest.skipUnless(os.environ.get("EXAMGUARDIAN_TEST_DB_CONTAINER", "").startswith("examguardian-"),"Requires disposable database")
    def test_deadline_grades_previously_saved_answers(self):
        _,session=self.start(); sid=session["id"]; token=self.students[0][1]
        correct={str(self.exam["questions"][0]["id"]):0}
        self.assertEqual(request(f"/api/ExamSessions/{sid}/answers","PUT",correct,token)[0],200)
        query=f"UPDATE \"Sessions\" SET \"DeadlineUtc\"=NOW()-INTERVAL '5 seconds' WHERE \"Id\"={int(sid)}"
        subprocess.run(["docker","exec",os.environ["EXAMGUARDIAN_TEST_DB_CONTAINER"],"psql","-U","tester","-d","examguardian_test","-c",query],check=True,capture_output=True)
        self.assertEqual(request(f"/api/ExamSessions/{sid}/answers","PUT",{},token)[0],409)
        status,result=request(f"/api/Exams/{self.exam['id']}/submit","POST",{"sessionId":sid,"answers":{}},token)
        self.assertEqual(status,200,result); self.assertEqual(result["score"],100)
        self.assertEqual(result["session"]["status"],"TimedOut")

    def test_evidence_manifest_does_not_hide_missing_screen(self):
        _, session=self.start(); sid=session["id"]; token=self.students[0][1]
        self.assertEqual(upload_video(sid,1,token)[0],200)
        self.assertEqual(request(f"/api/Exams/{self.exam['id']}/submit","POST",{"sessionId":sid,"answers":{}},token)[0],200)
        path=f"/api/video/{sid}/finalize"
        status,partial=request(path,"POST",{"camera":1,"screen":1},token)
        self.assertEqual(status,200,partial); self.assertEqual(partial["status"],"Partial")
        self.assertEqual(upload_video(sid,1,token,"screen")[0],200)
        status,complete=request(path,"POST",{"camera":1,"screen":1},token)
        self.assertEqual(status,200,complete); self.assertEqual(complete["status"],"Delivered")
        status,review=request(f"/api/ExamSessions/{sid}/review","POST",{"verdict":"Accepted","notes":"Both recordings inspected"},self.teacher)
        self.assertEqual(status,200,review); self.assertEqual(review["integrityVerdict"],"Accepted")
        export = f"/api/Exams/{self.exam['id']}/grades?group=TEST"
        self.assertEqual(request(export)[0], 401)
        self.assertEqual(request(export, token=token)[0], 403)
        self.assertEqual(request(f"/api/Exams/{self.exam['id']+100000}/grades?group=TEST", token=self.teacher)[0], 404)
        status, data = request(export, token=self.teacher)
        self.assertEqual(status, 200, data)
        self.assertEqual(list(csv.DictReader(io.StringIO(data.decode('utf-8')))), [{'username': self.students[0][2], 'grade': '0'}])

        self.assertEqual(upload_video(sid,1,token)[0],200)
        _,unchanged=request(f"/api/ExamSessions/{sid}",token=self.teacher)
        self.assertEqual(unchanged["integrityVerdict"],"Accepted")
        self.assertEqual(upload_video(sid,2,token)[0],200)
        _,changed=request(f"/api/ExamSessions/{sid}",token=self.teacher)
        self.assertEqual(changed["integrityVerdict"],"UnderReview")
        self.assertEqual(changed["evidenceStatus"],"Pending")
        self.assertEqual(request(export, token=self.teacher)[0], 409)
        _,partial=request(path,"POST",{"camera":1,"screen":1},token)
        self.assertEqual(partial["status"],"Partial")

    def test_video_indices_retries_and_access(self):
        _, session = self.start()
        sid = session["id"]
        content = Path(os.environ["EXAMGUARDIAN_TEST_VIDEO"]).read_bytes()
        def upload(index, content=content):
            boundary = "testboundary"
            body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"sessionId\"\r\n\r\n{sid}\r\n"
                f"--{boundary}\r\nContent-Disposition: form-data; name=\"chunkIndex\"\r\n\r\n{index}\r\n"
                f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"test.mp4\"\r\nContent-Type: video/mp4\r\n\r\n").encode() + content + f"\r\n--{boundary}--\r\n".encode()
            return request("/api/video/upload-chunk", "POST", body, self.students[0][1], f"multipart/form-data; boundary={boundary}")
        self.assertEqual(upload(1, b"\x00\x00\x00\x18ftypmp42")[0], 400)
        self.assertEqual(upload(1)[0], 200)
        self.assertEqual(upload(1)[0], 200)
        self.assertEqual(upload(1, content + b"changed")[0], 409)
        self.assertEqual(upload(3)[0], 200)
        status, chunks = request(f"/api/video/{sid}/chunks", token=self.teacher)
        self.assertEqual(status, 200, chunks)
        self.assertEqual([c["chunkIndex"] for c in chunks], [1, 3])
        self.assertEqual(request(f"/api/video/{sid}/chunks", token=self.students[1][1])[0], 404)
        self.assertEqual(request(f"/api/video/{sid}/1")[0], 401)
        self.assertEqual(request(chunks[0]["url"])[0], 200)
        self.assertEqual(request(chunks[0]["url"].replace(f"/{sid}/1?", f"/{sid}/3?"))[0], 401)

if __name__ == "__main__":
    unittest.main(verbosity=2)
