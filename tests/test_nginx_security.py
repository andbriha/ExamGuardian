"""Isolated TLS/header checks using temporary certificates, without deployment secrets."""
import datetime
import http.client
import pathlib
import ssl
import subprocess
import tempfile
import time
import unittest
from uuid import uuid4
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID

class EdgeSecurity(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.directory=tempfile.TemporaryDirectory(prefix='examguardian-tls-contract-'); root=pathlib.Path(cls.directory.name)
        cls.root=root; cls.name='examguardian-tls-contract-'+uuid4().hex[:8]
        key=rsa.generate_private_key(public_exponent=65537,key_size=2048)
        name=x509.Name([x509.NameAttribute(NameOID.COMMON_NAME,'localhost')]); now=datetime.datetime.now(datetime.timezone.utc)
        cert=(x509.CertificateBuilder().subject_name(name).issuer_name(name).public_key(key.public_key()).serial_number(x509.random_serial_number())
            .not_valid_before(now-datetime.timedelta(minutes=1)).not_valid_after(now+datetime.timedelta(days=1))
            .add_extension(x509.SubjectAlternativeName([x509.DNSName('localhost')]),critical=False).sign(key,hashes.SHA256()))
        (root/'fullchain.pem').write_bytes(cert.public_bytes(serialization.Encoding.PEM))
        (root/'privkey.pem').write_bytes(key.private_bytes(serialization.Encoding.PEM,serialization.PrivateFormat.PKCS8,serialization.NoEncryption()))
        config=pathlib.Path(__file__).resolve().parents[1]/'monitor/nginx.tls.conf'
        subprocess.run(['docker','run','-d','--name',cls.name,'-p','127.0.0.1:55443:443','-p','127.0.0.1:55080:80','--add-host','api:127.0.0.1','--add-host','ai_service:127.0.0.1','--mount',f'type=bind,source={config},target=/etc/nginx/conf.d/default.conf,readonly','--mount',f'type=bind,source={root},target=/etc/nginx/tls,readonly','nginx:alpine'],check=True,stdout=subprocess.DEVNULL)
        cls.addClassCleanup(lambda: subprocess.run(['docker','rm','--force',cls.name],stdout=subprocess.DEVNULL,check=False))
        cls.addClassCleanup(cls.directory.cleanup)
        for _ in range(30):
            try:
                connection=http.client.HTTPConnection('localhost',55080,timeout=1);connection.request('GET','/');connection.getresponse().read();connection.close();break
            except OSError: time.sleep(.2)
    def test_http_redirect(self):
        connection=http.client.HTTPConnection('localhost',55080);connection.request('GET','/monitor/1');response=connection.getresponse()
        self.assertEqual(response.status,308);self.assertTrue(response.getheader('Location').startswith('https://'));connection.close()
    def test_tls_certificate_and_headers_on_page_and_download(self):
        ctx=ssl.create_default_context(cafile=str(self.root/'fullchain.pem'))
        for path in ['/monitor/1','/landing/Online_Proctoring_System_Client.zip','/landing/ExamGuardian_Setup_Prod.exe']:
            with self.subTest(path=path):
                connection=http.client.HTTPSConnection('localhost',55443,context=ctx);connection.request('GET',path);response=connection.getresponse()
                self.assertEqual(response.getheader('Strict-Transport-Security'),'max-age=31536000')
                self.assertEqual(response.getheader('X-Content-Type-Options'),'nosniff')
                self.assertEqual(response.getheader('Referrer-Policy'),'no-referrer');response.read();connection.close()

if __name__=='__main__':unittest.main(verbosity=2)
