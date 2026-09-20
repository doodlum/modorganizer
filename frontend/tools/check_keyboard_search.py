#!/usr/bin/env python3
"""Exercise both installed-list searches through PID-scoped desktop input."""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time

from check_mod_pointer_drag import ROOT, BRIDGE, xdo, move_pointer
from data_launch_probe import call
from save_delete_probe import profile_state


def require_neutral_modifiers():
    # Core XKB state can retain Ctrl even when XQueryKeymap reports it released.
    # Refuse input in that state instead of turning search text into shortcuts.
    x11 = ctypes.CDLL('libX11.so.6')
    x11.XOpenDisplay.restype = ctypes.c_void_p
    x11.XkbGetState.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.c_void_p]
    x11.XCloseDisplay.argtypes = [ctypes.c_void_p]
    display = x11.XOpenDisplay(None)
    if not display:
        raise RuntimeError('Cannot inspect desktop keyboard state')
    try:
        state = (ctypes.c_ubyte * 32)()
        if x11.XkbGetState(display, 0x100, state) != 0:
            raise RuntimeError('Cannot inspect XKB modifiers')
        # XkbStateRec.mods follows two byte and two ushort group fields.
        if state[6] & (1 | 4 | 8 | 64 | 128):
            raise RuntimeError('Desktop has active typing modifiers; input was not sent')
    finally:
        x11.XCloseDisplay(display)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=ROOT / 'frontend/MockHost/bin/Release/net9.0/NexusModsApp.dll')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    app, output = args.app.resolve(), args.output.resolve()
    if not app.is_file() or output.exists():
        parser.error('Require an existing DLL and a fresh evidence directory')
    before = call(BRIDGE, 'snapshot')
    if not before['profile']['path'].replace('\\', '/').endswith('/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test'):
        parser.error('Isolated FNV Frontend Test profile is not selected')
    output.mkdir(parents=True)
    (output / 'baseline.json').write_text(json.dumps(dict(profilePath=before['profile']['path'], state=profile_state(before)), indent=2))
    layout = output / 'layout.json'
    layout.write_bytes((ROOT / 'frontend/artifacts/verify-paired-layout.json').read_bytes())
    (output / 'app.json').write_text(json.dumps({'path': str(app), 'sha256': hashlib.sha256(app.read_bytes()).hexdigest()}, indent=2))
    phase_file = output / 'phase.json'
    env = {k: v for k, v in os.environ.items() if not k.startswith('MO2_')}
    env.update(MO2_BRIDGE_DIRECTORY=str(BRIDGE), MO2_FRONTEND_LAYOUT=str(layout),
               MO2_SCREENSHOT=str(output / 'frontend.png'), MO2_VERIFY_KEYBOARD_SEARCH=str(phase_file))
    seen, error = set(), None
    inputs = []
    with (output / 'frontend.log').open('w') as log:
        process = subprocess.Popen([str(ROOT / '.tools/dotnet/dotnet'), str(app)], cwd=ROOT,
                                   env=env, stdout=log, stderr=subprocess.STDOUT)
        window = None
        try:
            deadline = time.monotonic() + 120
            while process.poll() is None:
                if time.monotonic() > deadline:
                    raise TimeoutError('Desktop keyboard check timed out')
                try:
                    phase = json.loads(phase_file.read_text())
                except (FileNotFoundError, json.JSONDecodeError):
                    time.sleep(.05); continue
                if phase['Phase'] in seen:
                    time.sleep(.05); continue
                ids = xdo('search', '--onlyvisible', '--pid', process.pid, '--class', 'mo2-nexus-frontend').splitlines()
                if window is None:
                    if len(ids) != 1:
                        raise RuntimeError('Expected one initial owned window')
                    window = ids[0]
                if window not in ids or int(xdo('getwindowpid', window)) != process.pid:
                    raise RuntimeError('Owned window is unavailable')
                xdo('windowactivate', '--sync', window)
                def focus():
                    if xdo('getactivewindow') != window:
                        raise RuntimeError('Another window took focus')
                focus()
                require_neutral_modifiers()
                inputs.append(dict(phase, window=window, pid=process.pid))
                (output / 'inputs.json').write_text(json.dumps(inputs, indent=2))
                if phase['Action'] == 'click':
                    geometry = dict(line.split('=', 1) for line in xdo('getwindowgeometry', '--shell', window).splitlines())
                    if not (int(geometry['X']) <= phase['X'] < int(geometry['X']) + int(geometry['WIDTH']) and
                            int(geometry['Y']) <= phase['Y'] < int(geometry['Y']) + int(geometry['HEIGHT'])):
                        raise RuntimeError('Search button is outside the owned window')
                    move_pointer(phase['X'], phase['Y'])
                    time.sleep(.2); focus(); xdo('click', 1)
                elif phase['Action'] == 'type':
                    focus(); xdo('type', '--delay', 35, 'mo2keyboardnomatch')
                elif phase['Action'] in ('hide', 'reopen'):
                    xdo('key', 'ctrl+f')
                elif phase['Action'] == 'clear':
                    xdo('key', 'Escape')
                else:
                    raise RuntimeError('Unknown input phase')
                seen.add(phase['Phase'])
                Path(str(phase_file) + '.done').write_text(phase['Phase'])
        except Exception as exc:
            error = str(exc)
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill(); process.wait()
    unchanged = False
    try:
        after = call(BRIDGE, 'snapshot')
        unchanged = before['profile']['path'] == after['profile']['path'] and profile_state(before) == profile_state(after)
    except Exception as exc:
        error = error or ('Post-check snapshot failed: ' + type(exc).__name__)
    verdicts = [line for line in (output / 'frontend.log').read_text().splitlines() if line.startswith(('PASS ', 'FAIL '))]
    result = dict(exitCode=process.returncode, phases=sorted(seen), nativeStateUnchanged=unchanged, driverError=error)
    (output / 'result.json').write_text(json.dumps(result, indent=2))
    print('\n'.join(verdicts)); print(result)
    return 0 if (not error and process.returncode == 0 and unchanged and len(seen) == 10 and
                 sum(line.startswith('PASS desktop keyboard search:') for line in verdicts) == 2 and
                 not any(line.startswith('FAIL ') for line in verdicts)) else 1


if __name__ == '__main__':
    raise SystemExit(main())
