"""Disposable production stack smoke test; build the three named images first.

Run with a Python environment containing cryptography. Uses only loopback port
55443 and fresh project volumes; does not touch development/production data.
"""
import datetime
import http.client
import json
from pathlib import Path
import secrets
import ssl
import subprocess
import tempfile
import time
from uuid import uuid4
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID

ROOT = Path(__file__).resolve().parents[1]


def main():
    project = 'examguardian-smoke-' + uuid4().hex[:10]
    with tempfile.TemporaryDirectory(prefix=project) as folder:
        folder = Path(folder)
        key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        subject = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, 'localhost')])
        now = datetime.datetime.now(datetime.timezone.utc)
        cert = (x509.CertificateBuilder().subject_name(subject).issuer_name(subject)
                .public_key(key.public_key()).serial_number(x509.random_serial_number())
                .not_valid_before(now - datetime.timedelta(minutes=1)).not_valid_after(now + datetime.timedelta(days=1))
                .add_extension(x509.SubjectAlternativeName([x509.DNSName('localhost')]), critical=False)
                .sign(key, hashes.SHA256()))
        (folder / 'fullchain.pem').write_bytes(cert.public_bytes(serialization.Encoding.PEM))
        (folder / 'privkey.pem').write_bytes(key.private_bytes(serialization.Encoding.PEM, serialization.PrivateFormat.PKCS8, serialization.NoEncryption()))
        password, api_key = secrets.token_urlsafe(32), secrets.token_urlsafe(48)
        proxy = '172.29.231.10'
        api_env = {
            'ASPNETCORE_ENVIRONMENT': 'Production', 'ForwardedHeaders__KnownProxies__0': proxy,
            'ConnectionStrings__DefaultConnection': 'Host=db;Database=smoke;Username=smoke;Password=' + password,
            'Jwt__Key': secrets.token_urlsafe(48), 'Jwt__Issuer': 'Smoke', 'Jwt__Audience': 'SmokeClient',
            'AiService__ApiKey': api_key, 'Bootstrap__ExaminerLogin': 'admin', 'Bootstrap__ExaminerPassword': password,
            'CredentialKeysPath': '/app/credential-keys', 'INFERENCE_IMAGES_PATH': '/app/InferenceImages',
        }
        hardened = {'cap_drop': ['ALL'], 'security_opt': ['no-new-privileges:true'], 'pids_limit': 256}
        config = {'services': {
            'db': {'image': 'postgres:16-alpine', 'environment': {'POSTGRES_USER': 'smoke', 'POSTGRES_DB': 'smoke', 'POSTGRES_PASSWORD': password},
                   'tmpfs': ['/var/lib/postgresql/data']},
            'api': {**hardened, 'image': 'examguardian-core-contract:local', 'environment': api_env,
                    'volumes': ['keys:/app/credential-keys', 'images:/app/InferenceImages']},
            'ai_service': {**hardened, 'image': 'examguardian-ai-hardening:local', 'environment': {
                'CORE_API_URL': 'http://api:8080/api/violations', 'AI_API_KEY': api_key,
                'JWT_JWKS_URL': 'http://api:8080/.well-known/jwks.json', 'JWT__ISSUER': 'Smoke', 'JWT__AUDIENCE': 'SmokeClient',
                'INFERENCE_IMAGES_PATH': '/app/images'},
                'volumes': ['images:/app/images', 'state:/app/state', 'models:/home/app/.deepface']},
            'nginx': {'image': 'examguardian-monitor-hardening:local', 'ports': ['127.0.0.1:55443:443'],
                      'networks': {'default': {'ipv4_address': proxy}},
                      'volumes': [f'{ROOT / "monitor/nginx.tls.conf"}:/etc/nginx/conf.d/default.conf:ro', f'{folder}:/etc/nginx/tls:ro']},
        }, 'volumes': {name: {} for name in ['keys', 'images', 'state', 'models']},
            'networks': {'default': {'ipam': {'config': [{'subnet': '172.29.231.0/24'}]}}}}
        compose_file = folder / 'compose.json'
        compose_file.write_text(json.dumps(config), encoding='utf-8')
        command = ['docker', 'compose', '-p', project, '-f', str(compose_file)]

        def compose(*args, check=True):
            return subprocess.run(command + list(args), check=check, capture_output=True, text=True)

        def ready(service, port):
            script = f"import urllib.request; print(urllib.request.urlopen('http://localhost:{port}/health/ready',timeout=3).read().decode())"
            for _ in range(120):
                result = compose('exec', '-T', service, 'python', '-c', script, check=False) if service == 'ai_service' else compose('exec', '-T', service, 'dotnet', 'Api.dll', '--healthcheck', check=False)
                if result.returncode == 0:
                    return result.stdout
                running = compose('ps', '--status', 'running', '--services').stdout.splitlines()
                if service not in running:
                    raise RuntimeError(service + ' exited before readiness')
                time.sleep(2)
            raise RuntimeError(service + ' readiness timeout')

        context = ssl.create_default_context(cafile=str(folder / 'fullchain.pem'))

        def request(path, data=None, token=None, cookie=None):
            headers = {'Origin': 'https://localhost:55443'}
            if token: headers['Authorization'] = 'Bearer ' + token
            if cookie: headers['Cookie'] = cookie
            if data is not None: headers['Content-Type'] = 'application/json'
            connection = http.client.HTTPSConnection('localhost', 55443, context=context, timeout=30)
            connection.request('POST' if data is not None else 'GET', path, json.dumps(data) if data is not None else None, headers)
            response = connection.getresponse()
            raw = response.read()
            result = response.status, dict(response.getheaders()), json.loads(raw) if raw and 'application/json' in response.getheader('Content-Type', '') else raw
            connection.close()
            return result

        try:
            compose('up', '-d', 'db')
            for _ in range(30):
                if compose('exec', '-T', 'db', 'pg_isready', '-U', 'smoke', '-d', 'smoke', check=False).returncode == 0: break
                time.sleep(1)
            compose('run', '--rm', '--no-deps', 'api', '--migrate')
            compose('up', '-d', 'api')
            ready('api', 8080)
            compose('up', '-d', 'ai_service')
            state = json.loads(ready('ai_service', 8000))
            assert state['ready'] and state['speech_recognition'] and len(state['models']) == 5, state
            for service in ['api', 'ai_service']:
                assert compose('exec', '-T', service, 'id', '-u').stdout.strip() == '1654'
            compose('up', '-d', 'nginx')
            for _ in range(30):
                try:
                    status, headers, page = request('/login')
                    break
                except OSError: time.sleep(.2)
            assert status == 200 and b'<app-root' in page
            assert headers.get('Strict-Transport-Security') == 'max-age=31536000'
            status, headers, body = request('/api/Auth/login', {'login': 'admin', 'password': password})
            assert status == 200, body
            cookie = headers['Set-Cookie']
            assert all(part in cookie.lower() for part in ['secure', 'httponly', 'samesite=strict', 'path=/api/auth'])
            admin = body['response']['accessToken']
            assert request('/api/Auth/refresh', {}, cookie=cookie.split(';')[0])[0] == 200
            status, _, student = request('/api/Students', {'fullName': 'Smoke Student', 'login': 'smoke_student', 'password': password, 'group': 'SMOKE'}, admin)
            assert status == 201, student
            status, _, exam = request('/api/Exams', {'title': 'Smoke Exam', 'durationMinutes': 60, 'questions': [{'text': 'Choose first', 'points': 1, 'correctIndex': 0, 'options': ['First', 'Second']}]}, admin)
            assert status == 201, exam
            status, _, body = request('/api/Auth/login', {'login': 'smoke_student', 'password': password})
            assert status == 200, body
            token = body['response']['accessToken']
            path = f'/verify/challenge?exam_id={exam["id"]}&client_protocol=4'
            status, _, challenge = request(path, token=token)
            assert status == 200 and challenge['speech_recognition'], challenge
            assert request('/api/Auth/logout', {}, token=token)[0] == 204
            assert request(path, token=token)[0] == 401
            assert request('/verify/capabilities')[0] == 200
            print('PASS: production migration, non-root model readiness, HTTPS monitor, secure cookies, refresh, real AI challenge and logout revocation.', flush=True)
        except Exception:
            print(compose('logs', '--tail', '70', check=False).stdout, flush=True)
            raise
        finally:
            compose('down', '--volumes', '--remove-orphans', check=False)


if __name__ == '__main__':
    main()
