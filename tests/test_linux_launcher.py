import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('launcher', Path(__file__).parents[1] / 'scripts/start_environment.py')
launcher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(launcher)

class LauncherTests(unittest.TestCase):
    def test_initializes_private_dev_config_and_checks_without_starting(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / '.env.dev.example').write_text((launcher.ROOT / '.env.dev.example').read_text(encoding='utf-8'), encoding='utf-8')
            with patch.object(launcher,'ROOT',root), patch.object(launcher.subprocess,'run') as run:
                launcher.main(['--check'])
                config = root / '.env.dev'
                original = config.read_bytes()
                self.assertNotIn(b'REPLACE_', original)
                self.assertEqual(run.call_count,1)
                self.assertEqual(run.call_args.args[0][-2:],['config','--quiet'])
                launcher.main(['--check'])
                self.assertEqual(config.read_bytes(),original)
                if launcher.os.name != 'nt': self.assertEqual(config.stat().st_mode & 0o777,0o600)
    def test_production_never_creates_config(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(launcher,'ROOT',Path(directory)), patch.object(launcher.subprocess,'run') as run:
            with self.assertRaises(ValueError): launcher.main(['--production','--check'])
            run.assert_not_called()
    def test_invalid_action_combination(self):
        with self.assertRaises(SystemExit): launcher.main(['--check','--stop'])
    def test_configuration_is_not_executed(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'env'
            path.write_text('VALUE=$(touch attack)\n',encoding='utf-8')
            self.assertEqual(launcher.read_settings(path)['VALUE'],'$(touch attack)')
            path.write_text('VALUE=one\nVALUE=two\n',encoding='utf-8')
            with self.assertRaises(ValueError): launcher.read_settings(path)

if __name__=='__main__': unittest.main()
