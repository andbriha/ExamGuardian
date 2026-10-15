"""Actual Qt HTTP admission/clock/answers/submission contract against the disposable API."""
import json
import os
import subprocess
import unittest
import test_core_contract as contracts

@unittest.skipUnless(os.environ.get('EXAMGUARDIAN_NATIVE_CLIENT') and os.environ.get('EXAMGUARDIAN_TEST_URL')=='http://127.0.0.1:55111','Requires explicit native test runner and disposable API')
class NativeWireContract(unittest.TestCase):
    def test_native_login_clock_autosave_and_submission(self):
        contracts.CoreContracts.setUpClass()
        fixture=contracts.CoreContracts(); fixture.setUp()
        student_id,token,login,password=fixture.students[0]
        env=os.environ.copy()
        env.update(EXAMGUARDIAN_URL=contracts.URL,QT_QPA_PLATFORM='offscreen',QT_QUICK_BACKEND='software',
            EXAMGUARDIAN_TEST_STUDENT_LOGIN=login,EXAMGUARDIAN_TEST_STUDENT_PASSWORD=password,
            EXAMGUARDIAN_TEST_EXAM_ID=str(fixture.exam['id']),EXAMGUARDIAN_TEST_ADMISSION=fixture.admission(),
            EXAMGUARDIAN_TEST_ANSWERS=json.dumps({str(fixture.exam['questions'][0]['id']):0}),
            EXAMGUARDIAN_TEST_DURATION='3600',EXAMGUARDIAN_TEST_SCORE='100')
        result=subprocess.run([env['EXAMGUARDIAN_NATIVE_CLIENT']],env=env,capture_output=True,timeout=45)
        self.assertEqual(result.returncode,0,result.stderr.decode('utf-8',errors='replace'))
        _,sessions=contracts.request(f'/api/ExamSessions/by-student/{student_id}',token=fixture.teacher)
        self.assertEqual(len(sessions),1); self.assertTrue(sessions[0]['isFinished']); self.assertEqual(sessions[0]['score'],100)

if __name__=='__main__': unittest.main(verbosity=2)
