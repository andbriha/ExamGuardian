"""Run against the disposable API created by run-core-contracts.ps1 only."""
import io
import json
import os
import re
import subprocess
import unittest
from urllib.parse import urlencode
from uuid import uuid4
from pypdf import PdfReader
from test_core_contract import CoreContracts, request, jwt_token, URL, KEY


@unittest.skipUnless(URL == 'http://127.0.0.1:55111' and KEY, 'Disposable API required')
class GroupWorkflow(CoreContracts):
    # Reuse authentication/session helpers but do not repeat inherited test methods.
    def test_roster_preview_commit_retry_and_repeatable_password_pdf(self):
        group = 'ROSTER-' + uuid4().hex
        body = {'group': group, 'text': 'full_name,username\nAlpha Student,\nBeta Student,'}
        status, preview = request('/api/students/import/preview', 'POST', body, self.teacher)
        self.assertEqual(status, 200, preview)
        self.assertTrue(preview['token'])
        self.assertEqual([r['action'] for r in preview['rows']], ['create','create'])
        self.assertEqual(request('/api/students/import/commit','POST',{'token':preview['token']+'bad'},self.teacher)[0],400)
        status, result = request('/api/students/import/commit','POST',{'token':preview['token']},self.teacher)
        self.assertEqual(status,200,result); self.assertEqual(result['created'],2)
        status, again=request('/api/students/import/commit','POST',{'token':preview['token']},self.teacher)
        self.assertEqual(status,200,again);self.assertEqual(again['created'],0)
        path='/api/groups/credentials?'+urlencode({'group':group,'format':'pdf'})
        status,pdf=request(path,token=self.teacher);self.assertEqual(status,200)
        text='\n'.join(p.extract_text() for p in PdfReader(io.BytesIO(pdf)).pages)
        passwords=re.findall(r'\b[A-F0-9]{16}\b',text);self.assertEqual(len(passwords),2,text)
        for row,password in zip(preview['rows'], passwords):
            self.assertEqual(request('/api/Auth/login','POST',{'login':row['login'],'password':password})[0],200)
        _,pdf_again=request(path,token=self.teacher)
        self.assertEqual(re.findall(r'\b[A-F0-9]{16}\b','\n'.join(p.extract_text() for p in PdfReader(io.BytesIO(pdf_again)).pages)),passwords)
        _,repeat=request('/api/students/import/preview','POST',body,self.teacher)
        self.assertTrue(all(r['action']=='skip' for r in repeat['rows']))
        self.assertEqual(request(path,token=self.students[0][1])[0],403)
        self.assertEqual(request('/api/students/import/preview','POST',body,self.students[0][1])[0],403)

    def test_roster_errors_and_conflicts_do_not_offer_commit(self):
        group='ROSTER-'+uuid4().hex
        for text in ['Person One\nPerson One','full_name,username\nPerson One,same\nPerson Two,same','full_name,username\nPerson One,=formula']:
            status,p=request('/api/students/import/preview','POST',{'group':group,'text':text},self.teacher)
            self.assertEqual(status,200,p);self.assertIsNone(p['token']);self.assertTrue(any(r['error'] for r in p['rows']))
        existing=self.students[0][2]
        _,p=request('/api/students/import/preview','POST',{'group':group,'text':f'Other Name,{existing}'},self.teacher)
        self.assertIsNone(p['token'])

    def test_group_pdf_uses_frozen_categories_and_published_marks_only(self):
        group='RESULTS-'+uuid4().hex
        self.assertEqual(request('/api/groups','POST',{'name':group},self.teacher)[0],201)
        for sid,*_ in self.students:
            self.assertEqual(request('/api/groups/students/'+sid,'PUT',{'name':group},self.teacher)[0],204)
        status,exam=request('/api/Exams','POST',{'title':'Group PDF contract','durationMinutes':60,'allowedGroup':group,'maxAttempts':2,
            'questions':[{'text':'PRIVATE QUESTION ONE','points':2,'correctIndex':0,'reportCategory':'Аудіювання','options':['Secret correct','Secret wrong']},
                         {'text':'PRIVATE QUESTION TWO','points':3,'correctIndex':0,'reportCategory':'Відповідність','options':['Secret correct','Secret wrong']}]},self.teacher)
        self.assertEqual(status,201,exam);self.exam=exam
        status,session=self.start();self.assertEqual(status,200,session);qid=[q['id'] for q in exam['questions']]
        self.assertEqual(request(f"/api/Exams/{exam['id']}/submit",'POST',{'sessionId':session['id'],'answers':{qid[0]:0,qid[1]:1}},self.students[0][1])[0],200)
        path='/api/group-results?'+urlencode({'examId':exam['id'],'group':group})
        status,report=request(path,token=self.teacher);self.assertEqual(status,200,report)
        self.assertEqual(len(report['students']),2);self.assertTrue(all(s['grade'] is None for s in report['students']))
        def sql(query):
            subprocess.run(['docker','exec',os.environ['EXAMGUARDIAN_TEST_DB_CONTAINER'],'psql','-U','tester','-d','examguardian_test','-c',query],check=True,capture_output=True)
        sql(f'''UPDATE "Sessions" SET "IntegrityVerdict"='Accepted' WHERE "Id"={session['id']}; UPDATE "Questions" SET "Points"=99,"ReportCategory"='Changed later' WHERE "ExamId"={exam['id']};''')
        _,report=request(path,token=self.teacher)
        graded=next(s for s in report['students'] if s['grade'] is not None)
        self.assertEqual(graded['grade'],40)
        categories={c['category']:(c['earned'],c['maximum']) for c in graded['categories']}
        self.assertEqual(categories,{'Аудіювання':(2,2),'Відповідність':(0,3)})
        status,pdf=request(path+'&format=pdf',token=self.teacher);self.assertEqual(status,200)
        text='\n'.join(p.extract_text() for p in PdfReader(io.BytesIO(pdf)).pages)
        self.assertIn('Аудіювання',text);self.assertNotIn('PRIVATE QUESTION',text);self.assertNotIn('Secret correct',text)
        self.assertNotIn(self.students[0][2],text);self.assertNotIn(self.students[0][3],text)
        self.assertEqual(request(path,token=self.students[0][1])[0],403)
        other_teacher=jwt_token({'id':str(uuid4()),'role':'Examiner','iss':'ExamGuardianTest','aud':'ExamGuardianTestClient','exp':4102444800})
        self.assertEqual(request(path,token=other_teacher)[0],401)
        self.start()  # latest unfinished attempt must hide the earlier accepted grade
        _,report=request(path,token=self.teacher)
        self.assertTrue(all(s['grade'] is None for s in report['students']))


if __name__ == '__main__':
    suite=unittest.TestSuite(GroupWorkflow(name) for name in GroupWorkflow.__dict__ if name.startswith('test_'))
    raise SystemExit(not unittest.TextTestRunner(verbosity=2).run(suite).wasSuccessful())
