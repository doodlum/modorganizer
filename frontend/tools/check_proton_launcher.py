#!/usr/bin/env python3
"""Check runtime selection and argv boundaries without starting Wine."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


class LauncherTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='mo2 launcher ')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.instance = self.root / 'instance'
        self.instance.mkdir()
        for name in ('ModOrganizer.exe', 'ModOrganizer.ini'):
            (self.instance / name).touch()
        self.proton = self.root / 'proton'
        self.proton.write_text(
            '#!/bin/sh\nprintf "%s\\n" "$@" > "$STEAM_COMPAT_DATA_PATH/arguments"\n'
            'printf "%s\\n" "$PWD" "$QT_QPA_PLATFORM" "$MO2_FRONTEND_HOST" > "$STEAM_COMPAT_DATA_PATH/context"\n')
        self.proton.chmod(0o755)
        (self.root / 'toolmanifest.vdf').write_text('"require_tool_appid" "1628350"\n')
        self.steam = self.root / 'Steam'
        apps = self.steam / 'steamapps'
        apps.mkdir(parents=True)
        (apps / 'appmanifest_1628350.acf').write_text('"installdir" "Test Runtime"\n')
        self.runtime = apps / 'common/Test Runtime/run'
        self.runtime.parent.mkdir(parents=True)
        self.runtime.write_text('#!/bin/sh\n[ "$1" = -- ] || exit 8\nshift\ntouch "$STEAM_COMPAT_DATA_PATH/runtime-used"\nexec "$@"\n')
        self.runtime.chmod(0o755)
        self.prefix = self.root / 'prefix'
        self.env = dict(os.environ)
        self.env.pop('MO2_STEAM_RUNTIME', None)

    def run_launcher(self):
        script = Path(__file__).resolve().parents[1] / 'start-mo2-proton.sh'
        return subprocess.run([str(script), str(self.instance), str(self.prefix), str(self.proton), str(self.steam), '22380'], env=self.env, capture_output=True, text=True)

    def test_required_runtime_and_spaced_paths(self):
        result = self.run_launcher()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue((self.prefix / 'runtime-used').exists())
        self.assertEqual((self.prefix / 'arguments').read_text().splitlines(), ['run', str(self.instance / 'ModOrganizer.exe')])
        self.assertEqual((self.prefix / 'context').read_text().splitlines(), [str(self.instance), 'windows:nowmpointer', '1'])

    def test_missing_runtime_does_not_start_proton(self):
        self.runtime.unlink()
        self.assertEqual(self.run_launcher().returncode, 2)
        self.assertFalse((self.prefix / 'arguments').exists())

    def test_explicit_runtime_in_another_library(self):
        override = self.root / 'Other Library/run'
        override.parent.mkdir()
        self.runtime.rename(override)
        self.env['MO2_STEAM_RUNTIME'] = str(override)
        result = self.run_launcher()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue((self.prefix / 'runtime-used').exists())


if __name__ == '__main__':
    unittest.main()
