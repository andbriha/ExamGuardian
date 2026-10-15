"""Read-only regression checks against the explicitly selected development origin."""
import os
import unittest
import urllib.error
import urllib.request
from urllib.parse import urljoin, urlsplit

ORIGIN = os.environ.get('EXAMGUARDIAN_TEST_DEV_URL', '')


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None


@unittest.skipUnless(ORIGIN == 'http://127.0.0.1:8081', 'Requires the explicit development origin')
class DevelopmentRouting(unittest.TestCase):
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
