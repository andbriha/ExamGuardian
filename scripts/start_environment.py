"""Portable Docker Compose launcher. Configuration is data, never shell code."""
import argparse
import os
from pathlib import Path
import re
import secrets
import socket
import subprocess
import sys
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parent.parent
REQUIRED = ('DB_NAME', 'DB_USER', 'DB_PASSWORD', 'JWT__KEY', 'JWT__ISSUER', 'JWT__AUDIENCE',
            'AiService__ApiKey', 'BOOTSTRAP_EXAMINER_LOGIN', 'BOOTSTRAP_EXAMINER_PASSWORD')


def read_settings(path):
    settings = {}
    for line in path.read_text(encoding='utf-8-sig').splitlines():
        match = re.fullmatch(r'([A-Za-z_][A-Za-z0-9_]*)=(.*)', line)
        if match:
            key, value = match.groups()
            if key in settings:
                raise ValueError(f'Duplicate configuration field: {key}')
            settings[key] = value.strip()
    return settings


def initialize_dev(path):
    text = (ROOT / '.env.dev.example').read_text(encoding='utf-8')
    for key in ('DB_PASSWORD', 'JWT__KEY', 'AiService__ApiKey', 'BOOTSTRAP_EXAMINER_PASSWORD'):
        text = re.sub(rf'(?m)^{key}=.*$', f'{key}={secrets.token_hex(48)}', text)
    for port in range(4321, 4332):
        with socket.socket() as listener:
            try:
                listener.bind(('127.0.0.1', port))
                break
            except OSError:
                continue
    else:
        raise ValueError('No free documentation port in 4321–4331.')
    text = text.replace('DOCS_PORT=4321', f'DOCS_PORT={port}')
    # Exclusive creation protects an existing environment even during concurrent launches.
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, 'w', encoding='utf-8', newline='\n') as output:
        output.write(text)
    print('Created .env.dev with random secrets. Examiner credentials remain in that file.')


def validate(settings, flavor):
    for key in REQUIRED:
        if not settings.get(key) or re.search('REPLACE_|CHANGEME', settings[key]):
            raise ValueError(f'Configure {key} in .env.{flavor}.')
    if len(settings['JWT__KEY']) < 32:
        raise ValueError('JWT__KEY must contain at least 32 characters.')
    if flavor == 'production':
        origin = urlparse(settings.get('PUBLIC_ORIGIN', ''))
        if origin.scheme != 'https' or not origin.hostname or origin.hostname == 'exam.example.org':
            raise ValueError('Configure the actual HTTPS PUBLIC_ORIGIN.')
        directory = Path(settings.get('TLS_DIRECTORY', ''))
        if not directory.is_absolute():
            raise ValueError('TLS_DIRECTORY must be an absolute path.')
        for filename in ('fullchain.pem', 'privkey.pem'):
            if not (directory / filename).is_file():
                raise ValueError(f'TLS certificate file missing: {filename}')


def main(argv=None):
    parser = argparse.ArgumentParser(description='Start ExamGuardian with Docker Compose (Python 3.9+, Docker Compose v2).')
    parser.add_argument('--production', '--продакшн', action='store_true')
    actions = parser.add_mutually_exclusive_group()
    for action in ('check', 'stop', 'status'):
        actions.add_argument('--' + action, action='store_true')
    args = parser.parse_args(argv)
    flavor = 'production' if args.production else 'dev'
    config = ROOT / ('.env.' + flavor)
    if not config.exists():
        if flavor == 'production' or args.stop or args.status:
            raise ValueError(f'Missing {config.name}. Configure the selected environment explicitly.')
        initialize_dev(config)
    settings = read_settings(config)
    if not (args.stop or args.status):
        validate(settings, flavor)
    compose = ['docker', 'compose', '--project-directory', str(ROOT), '--project-name', 'examguardian-' + flavor,
               '--env-file', str(config), '-f', str(ROOT / 'compose.base.yml'), '-f', str(ROOT / ('compose.' + flavor + '.yml'))]
    def run(*arguments):
        subprocess.run(compose + list(arguments), check=True, cwd=ROOT)
    run('config', '--quiet')
    print(f'Environment: {flavor} | Project: examguardian-{flavor}', flush=True)
    if args.check:
        print('Configuration valid. No containers changed.')
        return
    if args.stop:
        run('stop')
        return
    if args.status:
        run('ps')
        return
    if flavor == 'dev':
        run('up', '-d', '--build', 'db', 'docs')
        print(f"Documentation: http://127.0.0.1:{settings.get('DOCS_PORT', '4321')}/start", flush=True)
    run('up', '-d', '--build', '--wait', '--wait-timeout', '300')
    origin = settings.get('PUBLIC_ORIGIN') if flavor == 'production' else 'http://127.0.0.1:' + settings.get('APP_PORT', '8081')
    print(f'Application: {origin} | Examiner panel: {origin}/login')
    print('The student Qt client is built and launched separately on its native OS.')
    run('ps')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        print(f'Launch failed: {error}', file=sys.stderr)
        sys.exit(1)
