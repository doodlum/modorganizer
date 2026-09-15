#!/usr/bin/env python3
"""Check runtime selection and argv boundaries without starting Wine."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


class LauncherTests(unittest.TestCase):
    def test_native_observer_distinguishes_prefixes_and_exited_processes(self):
        import importlib.util
        path = Path(__file__).with_name('wait-mo2-host.py')
        spec = importlib.util.spec_from_file_location('host_observer', path)
        observer = importlib.util.module_from_spec(spec); spec.loader.exec_module(observer)
        proc = self.root/'proc'; entry = proc/'123'; entry.mkdir(parents=True)
        (entry/'cmdline').write_bytes(('Z:'+str(self.instance/'ModOrganizer.exe').replace('/','\\')).encode()+b'\0')
        (entry/'stat').write_text('123 (MO2 test process) S 1 0')
        (entry/'environ').write_bytes(b'WINEPREFIX='+os.fsencode(self.root/'another-prefix/pfx')+b'\0')
        cached=[]
        self.assertFalse(observer.native_running(self.instance,self.prefix,proc,cached))
        (entry/'environ').write_bytes(b'WINEPREFIX='+os.fsencode(self.prefix/'pfx')+b'\0')
        self.assertTrue(observer.native_running(self.instance,self.prefix,proc,cached))
        self.assertEqual(cached,[entry])
        (entry/'stat').write_text('123 (MO2 test process) Z 1 0')
        self.assertFalse(observer.native_running(self.instance,self.prefix,proc,cached))
        (entry/'stat').write_text('123 (another process) S 1 0')
        (entry/'cmdline').write_bytes(b'/bin/sleep\0')
        self.assertFalse(observer.native_running(self.instance,self.prefix,proc,cached))

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
        self.assertEqual((self.prefix / 'arguments').read_text().splitlines(), ['run', 'wscript.exe', '//B', '//Nologo', '.mo2-frontend-launch.vbs'])
        tools = Path(__file__).resolve().parent
        for suffix in ('cmd', 'vbs'):
            self.assertEqual((self.instance / ('.mo2-frontend-launch.' + suffix)).read_bytes(),
                             (tools / ('mo2-frontend-launch.' + suffix)).read_bytes())
        self.assertEqual((self.prefix / 'context').read_text().splitlines(), [str(self.instance), 'windows:nowmpointer', '1'])

    def test_missing_runtime_does_not_start_proton(self):
        self.runtime.unlink()
        self.assertEqual(self.run_launcher().returncode, 2)
        self.assertFalse((self.prefix / 'arguments').exists())

    def test_runtime_closing_inherited_descriptors_does_not_release_launcher_lock(self):
        import time
        self.runtime.write_text('#!/usr/bin/env python3\nimport os,time\nfrom pathlib import Path\nfor fd in range(3,256):\n try: os.close(fd)\n except OSError: pass\np=Path(os.environ["STEAM_COMPAT_DATA_PATH"])\n(p/"runtime-ready").touch()\nwhile not (p/"finish-runtime").exists(): time.sleep(.02)\n')
        script = Path(__file__).resolve().parents[1] / 'start-mo2-proton.sh'
        first = subprocess.Popen([str(script),str(self.instance),str(self.prefix),str(self.proton),str(self.steam),'22380'],env=self.env,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
        try:
            deadline = time.monotonic()+5
            while not (self.prefix/'runtime-ready').exists() and time.monotonic()<deadline: time.sleep(.02)
            self.assertTrue((self.prefix/'runtime-ready').exists())
            second = self.run_launcher()
            self.assertIn('already active',second.stderr)
            self.assertIsNone(first.poll())
        finally:
            (self.prefix/'finish-runtime').touch()
            first.communicate(timeout=5)

    def test_explicit_runtime_in_another_library(self):
        override = self.root / 'Other Library/run'
        override.parent.mkdir()
        self.runtime.rename(override)
        self.env['MO2_STEAM_RUNTIME'] = str(override)
        result = self.run_launcher()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue((self.prefix / 'runtime-used').exists())

    def test_main_exit_releases_lock_without_terminating_surviving_container(self):
        import time
        self.runtime.write_text(
            '#!/usr/bin/env python3\nimport os,subprocess,time\nfrom pathlib import Path\n'
            'p=Path(os.environ["STEAM_COMPAT_DATA_PATH"])\n'
            'for fd in range(3,256):\n'
            ' try: os.close(fd)\n'
            ' except OSError: pass\n'
            'import sys\nsubprocess.run(sys.argv[2:])\n'
            'marker=p/("orphan-"+str(os.getpid()));marker.touch()\n'
            'while not (p/"finish-orphans").exists(): time.sleep(.02)\n'
            'marker.unlink()\n')
        script = Path(__file__).resolve().parents[1] / 'start-mo2-proton.sh'
        launchers = []
        try:
            for status in (7, 0):
                self.proton.write_text(f'#!/bin/sh\nexit {status}\n')
                process = subprocess.Popen([str(script),str(self.instance),str(self.prefix),str(self.proton),str(self.steam),'22380'],
                                           env=self.env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
                launchers.append(process)
                self.assertEqual(process.wait(timeout=5), status)
            deadline = time.monotonic()+5
            while len(list(self.prefix.glob('orphan-*'))) < 2 and time.monotonic()<deadline: time.sleep(.02)
            survivors = list(self.prefix.glob('orphan-*'))
            self.assertEqual(len(survivors), 2, 'Second launcher did not reach the main command')
            for marker in survivors: os.kill(int(marker.name.split('-')[1]), 0)
            self.assertEqual(list(self.prefix.glob('.mo2-launch.*')), [], 'Completion directories leaked')
        finally:
            self.prefix.mkdir(exist_ok=True)
            (self.prefix/'finish-orphans').touch()
            for process in launchers:
                if process.poll() is None: process.terminate(); process.wait(timeout=5)
            deadline = time.monotonic()+5
            while list(self.prefix.glob('orphan-*')) and time.monotonic()<deadline: time.sleep(.02)

    def test_native_exit_releases_lock_even_when_proton_stub_keeps_running(self):
        import time
        self.proton.write_text(
            '#!/usr/bin/env python3\nimport os,subprocess,time\nfrom pathlib import Path\n'
            'p=Path(os.environ["STEAM_COMPAT_DATA_PATH"])\n'
            'env=dict(os.environ,WINEPREFIX=str(p/"pfx"))\n'
            'child=subprocess.Popen([str(Path.cwd()/"ModOrganizer.exe"),"1"],executable="/bin/sleep",env=env)\n'
            '(p/"native-ready").write_text(str(os.getpid()))\n'
            'child.wait();(p/"native-exited").touch()\n'
            'while not (p/"finish-stub").exists():time.sleep(.02)\n'
            '(p/"stub-exited").touch()\n')
        script = Path(__file__).resolve().parents[1] / 'start-mo2-proton.sh'
        process = subprocess.Popen([str(script),str(self.instance),str(self.prefix),str(self.proton),str(self.steam),'22380'],
                                   env=self.env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
        try:
            deadline = time.monotonic()+5
            while not (self.prefix/'native-ready').exists() and time.monotonic()<deadline:time.sleep(.02)
            self.assertTrue((self.prefix/'native-ready').exists())
            self.assertIn('already active',self.run_launcher().stderr)
            self.assertEqual(process.wait(timeout=5),0)
            self.assertTrue((self.prefix/'native-exited').exists())
            os.kill(int((self.prefix/'native-ready').read_text()),0)
            self.assertEqual(list(self.prefix.glob('.mo2-launch.*')),[])
        finally:
            self.prefix.mkdir(exist_ok=True);(self.prefix/'finish-stub').touch()
            if process.poll() is None:process.terminate();process.wait(timeout=5)
            deadline=time.monotonic()+5
            while not (self.prefix/'stub-exited').exists() and time.monotonic()<deadline:time.sleep(.02)


if __name__ == '__main__':
    unittest.main()
