#!/usr/bin/env python3
"""Exercise generated desktop entries with GIO; no real NXM links or associations."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import unittest
from gi.repository import Gio


class HandlerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='mo2-nxm-check-')
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.bin = self.root / 'bin'; self.bin.mkdir()
        self.state = self.root / 'association'; self.state.write_text('previous.desktop')
        self.record = self.root / 'received.json'
        self.notice = self.root / 'notice.json'
        self.env = dict(os.environ, PATH=str(self.bin) + ':' + os.environ['PATH'],
                        NXM_TEST_STATE=str(self.state), NXM_TEST_RECORD=str(self.record), NXM_TEST_NOTICE=str(self.notice))
        self.script('xdg-mime', "import os,sys\nfrom pathlib import Path\np=Path(os.environ['NXM_TEST_STATE'])\nif sys.argv[1]=='query':print(p.read_text())\nelse:p.write_text(sys.argv[2])\n")
        self.script('kdialog', "import os,sys,json\nfrom pathlib import Path\nPath(os.environ['NXM_TEST_NOTICE']).write_text(json.dumps(sys.argv[1:]))\n")

    def script(self, name, body):
        path = self.bin / name
        path.write_text('#!/usr/bin/python3\n' + body); path.chmod(0o700)
        return path

    def install(self, suffix):
        data = self.root / suffix
        self.env.update(XDG_DATA_HOME=str(data), XDG_CONFIG_HOME=str(self.root / 'config'))
        dotnet = self.script('runtime with spaces', "import os,sys,json\nfrom pathlib import Path\nPath(os.environ['NXM_TEST_RECORD']).write_text(json.dumps(sys.argv[1:]))\nwith open(os.environ['NXM_TEST_RECORD']+'.calls','a') as calls:calls.write('1\\n')\nprint('synthetic-private-output')\nsys.exit(int(os.environ.get('NXM_TEST_EXIT','0')))\n")
        app = self.root / 'app with spaces.dll'; app.touch()
        installer = Path(__file__).with_name('install_nxm_handler.py')
        subprocess.run(['/usr/bin/python3', str(installer), '--dotnet', str(dotnet), '--app', str(app)], env=self.env, check=True, capture_output=True)
        return data, app

    def launch(self, data):
        entry = Gio.DesktopAppInfo.new_from_filename(str(data / 'applications/mo2-nexus-frontend-nxm.desktop'))
        self.assertIsNotNone(entry)
        context = Gio.AppLaunchContext()
        for name, value in self.env.items(): context.setenv(name, value)
        link = 'nxm://newvegas/mods/1/files/2?key=fixture%2Bonly%2F&expires=123&user_id=456'
        entry.launch_uris([link], context)
        deadline = time.monotonic() + 5
        while not self.record.exists() and time.monotonic() < deadline: time.sleep(.05)
        self.assertTrue(self.record.exists(), 'Desktop entry did not reach the generated wrapper')
        return json.loads(self.record.read_text()), link

    def test_desktop_launch_preserves_one_signed_argument(self):
        data, app = self.install('ordinary data')
        received, link = self.launch(data)
        self.assertEqual(received, [str(app), '--nxm', link])

    def test_desktop_launch_supports_reserved_path_characters(self):
        data, app = self.install('data \\ " $ ` % directory')
        received, link = self.launch(data)
        self.assertEqual(received, [str(app), '--nxm', link])

    def test_failure_is_sanitized_and_not_retried(self):
        data, _ = self.install('failure data')
        self.env['NXM_TEST_EXIT'] = '1'
        self.launch(data)
        deadline = time.monotonic() + 5
        while not self.notice.exists() and time.monotonic() < deadline: time.sleep(.05)
        self.assertTrue(self.notice.exists())
        notice = self.notice.read_text()
        self.assertNotIn('fixture', notice)
        self.assertNotIn('synthetic-private', notice)
        self.assertIn('Check Downloads before retrying', notice)
        self.assertEqual(Path(str(self.record) + '.calls').read_text(), '1\n')

    def test_restore_does_not_replace_another_handler(self):
        self.install('restore data')
        installer = Path(__file__).with_name('install_nxm_handler.py')
        self.state.write_text('new-user-choice.desktop')
        result = subprocess.run(['/usr/bin/python3', str(installer), '--restore'], env=self.env, capture_output=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.state.read_text(), 'new-user-choice.desktop')
        self.state.write_text('mo2-nexus-frontend-nxm.desktop')
        subprocess.run(['/usr/bin/python3', str(installer), '--restore'], env=self.env, check=True, capture_output=True)
        self.assertEqual(self.state.read_text(), 'previous.desktop')


if __name__ == '__main__': unittest.main()
