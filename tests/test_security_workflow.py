"""Security regression contracts. Only the disposable loopback test deployment is allowed."""
import concurrent.futures
import http.cookies
import json
import os
import subprocess
import unittest
import urllib.request
from uuid import uuid4
from test_core_contract import request, URL, KEY, jwt_token

@unittest.skipUnless(URL == 'http://127.0.0.1:55111' and KEY, 'Disposable test API required')
class SecurityWorkflow(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        _, body = request('/api/Auth/login','POST',{'login':'admin','password':os.environ['EXAMGUARDIAN_TEST_PASSWORD']})
        cls.admin = body['response']['accessToken']
    def sql(self, command):
        return subprocess.check_output(['docker','exec',os.environ['EXAMGUARDIAN_TEST_DB_CONTAINER'],'psql','-U','tester','-d','examguardian_test','-Atc',command],text=True).strip()
    def account(self, teacher=False, group='Security'):
        login='sec_'+uuid4().hex; password='Security-Test-Password-2026!'
        status, student=request('/api/Students','POST',{'fullName':'Security Contract','login':login,'password':password,'group':group},self.admin)
        self.assertEqual(status,201,student)
        if teacher: self.sql(f"UPDATE \"Users\" SET \"Role\"='Examiner' WHERE \"Id\"='{student['id']}'")
        return student,login,password
    def login(self, login, password):
        req=urllib.request.Request(URL+'/api/Auth/login',json.dumps({'login':login,'password':password}).encode(),{'Content-Type':'application/json'})
        with urllib.request.urlopen(req) as response:
            body=json.load(response); cookie=http.cookies.SimpleCookie(response.headers['Set-Cookie'])
        return body['response']['accessToken'], cookie['refreshToken'].value
    def refresh(self, cookie):
        return request('/api/Auth/refresh','POST',{},extra_headers={'Cookie':'refreshToken='+cookie})
    def test_rotation_reuse_revokes_family_and_access(self):
        _,login,password=self.account(); access,refresh=self.login(login,password)
        self.assertEqual(request('/api/Auth/me',token=access)[0],200)
        self.assertEqual(self.refresh(refresh)[0],200)
        self.assertEqual(self.refresh(refresh)[0],401)
        self.assertEqual(request('/api/Auth/me',token=access)[0],401)
        self.assertNotIn(refresh,self.sql('SELECT "Hash" FROM "RefreshCredentials"'))
    def test_logout_works_without_valid_access_and_revokes_it(self):
        _,login,password=self.account(); access,refresh=self.login(login,password)
        self.assertEqual(request('/api/Auth/logout','POST',{},extra_headers={'Cookie':'refreshToken='+refresh})[0],204)
        self.assertEqual(request('/api/Auth/me',token=access)[0],401)
        self.assertEqual(self.refresh(refresh)[0],401)
    def test_parallel_refresh_consumes_once(self):
        _,login,password=self.account(); access,refresh=self.login(login,password)
        with concurrent.futures.ThreadPoolExecutor(2) as pool:
            responses=list(pool.map(lambda _: self.refresh(refresh),range(2)))
        self.assertEqual(sorted(r[0] for r in responses),[200,401])
    def test_cross_site_cookie_operation_rejected(self):
        self.assertEqual(request('/api/Auth/refresh','POST',{},extra_headers={'Origin':'https://attacker.example','Sec-Fetch-Site':'cross-site'})[0],403)
    def test_roster_requires_assignment_and_can_be_revoked(self):
        group='private-'+uuid4().hex
        student,_,_=self.account(group=group)
        teacher,login,password=self.account(teacher=True)
        other,_=self.login(login,password)
        self.assertEqual(request('/api/Students/'+student['id'],token=other)[0],404)
        self.assertEqual(request('/api/groups/credentials?group='+group,token=other)[0],403)
        path='/api/groups/'+group+'/examiners/'+teacher['id']
        self.assertEqual(request(path,'PUT',{},other)[0],403)
        self.assertEqual(request(path,'PUT',{},self.admin)[0],204)
        self.assertEqual(request('/api/Students/'+student['id'],token=other)[0],200)
        self.assertEqual(request(path,'DELETE',token=self.admin)[0],204)
        self.assertEqual(request('/api/Students/'+student['id'],token=other)[0],404)
    def test_shared_mac_cannot_mint_access_and_jwks_has_no_private_material(self):
        forged=jwt_token({'id':str(uuid4()),'role':'Examiner','iss':'ExamGuardianTest','aud':'ExamGuardianTestClient','exp':4102444800})
        self.assertEqual(request('/api/Students',token=forged)[0],401)
        status,jwks=request('/.well-known/jwks.json');self.assertEqual(status,200)
        self.assertEqual(jwks['keys'][0]['alg'],'RS256')
        self.assertTrue(set(jwks['keys'][0]).isdisjoint({'d','p','q','dp','dq','qi'}))

if __name__=='__main__': unittest.main(verbosity=2)
