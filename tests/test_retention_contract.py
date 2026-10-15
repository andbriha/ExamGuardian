"""Retention deletes media while retaining the academic and review audit trail."""
import json
import os
import subprocess
import unittest
import test_core_contract as c

API=os.environ.get('EXAMGUARDIAN_TEST_API_CONTAINER','')
DB=os.environ.get('EXAMGUARDIAN_TEST_DB_CONTAINER','')

@unittest.skipUnless(c.URL=='http://127.0.0.1:55111' and API.startswith('examguardian-') and DB.startswith('examguardian-'),'Requires explicitly disposable containers')
class RetentionContract(unittest.TestCase):
    def test_reviewed_media_expires_without_erasing_result(self):
        c.CoreContracts.setUpClass(); f=c.CoreContracts(); f.setUp()
        _,session=f.start(); sid=session['id']; token=f.students[0][1]
        self.assertEqual(c.upload_video(sid,1,token)[0],200)
        answers={str(f.exam['questions'][0]['id']):0}
        self.assertEqual(c.request(f"/api/Exams/{f.exam['id']}/submit",'POST',{'sessionId':sid,'answers':answers},token)[0],200)
        self.assertEqual(c.request(f'/api/ExamSessions/{sid}/review','POST',{'verdict':'Inconclusive','notes':'Retention contract fixture'},f.teacher)[0],200)
        sql=f"""UPDATE "Sessions" SET "EndTime"=NOW()-INTERVAL '9 days', "IntegrityReviewedAt"=NOW()-INTERVAL '9 days' WHERE "Id"={sid};"""
        subprocess.run(['docker','exec',DB,'psql','-U','tester','-d','examguardian_test','-v','ON_ERROR_STOP=1','-c',sql],check=True,capture_output=True)
        def maintenance(mode):
            result=subprocess.run(['docker','exec','-e','Retention__Days=7',API,'dotnet','Api.dll','--retention-'+mode],capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stdout+result.stderr)
            return result.stdout
        preview=maintenance('preview')
        plan=next(json.loads(line) for line in preview.splitlines() if line.startswith('{'))
        self.assertIn(sid,plan['sessionIds'])
        media=f'/app/ExamVideos/{sid}/chunk_000001.mp4'
        self.assertEqual(subprocess.run(['docker','exec',API,'test','-f',media]).returncode,0)
        maintenance('apply')
        self.assertEqual(subprocess.run(['docker','exec',API,'test','-f',media]).returncode,1)
        self.assertEqual(subprocess.run(['docker','exec',API,'test','-f',media+'.json']).returncode,0)
        _,saved=c.request(f'/api/ExamSessions/{sid}',token=f.teacher)
        self.assertEqual(saved['evidenceStatus'],'Expired'); self.assertEqual(saved['score'],100)
        self.assertTrue(saved['scoreCalculated']); self.assertEqual(saved['integrityVerdict'],'Inconclusive')
        self.assertEqual(len(json.loads(saved['integrityHistoryJson'])),1)
        sql=f"""SELECT "SavedAnswersJson" FROM "Sessions" WHERE "Id"={sid};"""
        output=subprocess.run(['docker','exec',DB,'psql','-U','tester','-d','examguardian_test','-At','-c',sql],check=True,capture_output=True,text=True).stdout
        self.assertEqual(json.loads(output.strip()),answers)

if __name__=='__main__': unittest.main(verbosity=2)
