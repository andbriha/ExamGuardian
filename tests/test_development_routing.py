"""Read-only regression checks against the explicitly selected development origin."""
import os
import unittest
import urllib.error
import urllib.request
from pathlib import Path
from urllib.parse import urljoin, urlsplit

ORIGIN = os.environ.get('EXAMGUARDIAN_TEST_DEV_URL', '')


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None


@unittest.skipUnless(ORIGIN == 'http://127.0.0.1:8081', 'Requires the explicit development origin')
class DevelopmentRouting(unittest.TestCase):
    def test_downloads_serve_published_dev_artifacts(self):
        landing = Path(__file__).resolve().parents[1] / 'Landing'
        for url_name, filename in (
            ('Online_Proctoring_System_Setup.exe', 'ExamGuardian_Setup_Dev.exe'),
            ('Online_Proctoring_System_Client.exe', 'ExamGuardian_Client_Dev.exe'),
            ('Online_Proctoring_System_Client.zip', 'ExamGuardian_Client_Dev.zip'),
        ):
            with self.subTest(filename=filename):
                request = urllib.request.Request(ORIGIN + '/landing/' + url_name, method='HEAD')
                with urllib.request.urlopen(request, timeout=15) as response:
                    self.assertEqual(int(response.headers['Content-Length']), (landing / filename).stat().st_size)
                    self.assertEqual(response.headers['Content-Disposition'], f'attachment; filename="{filename}"')
                    self.assertEqual(response.headers['Cache-Control'], 'no-store')

    def test_wrong_flavor_and_missing_downloads_are_not_html(self):
        for filename in ('ExamGuardian_Setup_Prod.exe', 'ExamGuardian_Client_Prod.zip', 'missing.exe', 'missing.zip'):
            with self.subTest(filename=filename):
                with self.assertRaises(urllib.error.HTTPError) as caught:
                    urllib.request.urlopen(ORIGIN + '/landing/' + filename, timeout=15)
                self.assertEqual(caught.exception.code, 404)

    def test_root_redirect_preserves_external_port(self):
        opener = urllib.request.build_opener(NoRedirect)
        with self.assertRaises(urllib.error.HTTPError) as caught:
            opener.open(ORIGIN + '/', timeout=15)
        self.assertEqual(caught.exception.code, 302)
        destination = urljoin(ORIGIN + '/', caught.exception.headers['Location'])
        self.assertEqual(urlsplit(destination).netloc, urlsplit(ORIGIN).netloc)
        self.assertEqual(urlsplit(destination).path, '/landing/')
        with urllib.request.urlopen(destination, timeout=15) as response:
            self.assertEqual(response.status, 200)
            self.assertIn(b'Online_Proctoring_System_Setup.exe', response.read())

    def test_panel_and_landing_directory_keep_same_origin(self):
        for path in ('/login', '/landing'):
            with self.subTest(path=path):
                with urllib.request.urlopen(ORIGIN + path, timeout=15) as response:
                    self.assertEqual(response.status, 200)
                    self.assertEqual(urlsplit(response.url).netloc, urlsplit(ORIGIN).netloc)


if __name__ == '__main__':
    unittest.main(verbosity=2)
