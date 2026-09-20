#!/usr/bin/env python3
"""Drive the isolated FNV frontend's mod handles with PID-scoped X11 input."""
import argparse
import hashlib
import ctypes
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time

from data_launch_probe import call
from save_delete_probe import profile_state

ROOT = Path(__file__).resolve().parents[2]
BRIDGE = ROOT / 'frontend/artifacts/mo2-fnv-host/plugins/data/frontend-bridge'
MOVING = 'The Mod Configuration Menu'
DESTINATION = 'MCM Author Examples'


def xdo(*args):
    return subprocess.check_output(['xdotool', *map(str, args)], text=True).strip()


def move_pointer(x, y):
    # xdotool 3.20211022.1 uses XWarpPointer for absolute motion. In this
    # XI2 session that moves the visible cursor but leaves injected button events
    # at the old device position. XTest motion matches its XTest button input.
    x11 = ctypes.CDLL('libX11.so.6')
    xtst = ctypes.CDLL('libXtst.so.6')
    x11.XOpenDisplay.argtypes = [ctypes.c_char_p]
    x11.XOpenDisplay.restype = ctypes.c_void_p
    x11.XSync.argtypes = [ctypes.c_void_p, ctypes.c_int]
    x11.XCloseDisplay.argtypes = [ctypes.c_void_p]
    xtst.XTestFakeMotionEvent.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_ulong]
    display = x11.XOpenDisplay(None)
    if not display:
        raise RuntimeError('Cannot open the X11 display')
    try:
        if not xtst.XTestFakeMotionEvent(display, 0, x, y, 0):
            raise RuntimeError('XTest motion injection failed')
        x11.XSync(display, 0)
    finally:
        x11.XCloseDisplay(display)


def order(snapshot):
    return [m['name'] for m in sorted(snapshot['mods'], key=lambda m: m['priority'])
            if not m.get('overwrite', False) and m['priority'] >= 0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=ROOT / 'frontend/MockHost/bin/Release/net9.0/MockHost.dll', help='Frontend DLL to test')
    parser.add_argument('--output', type=Path, required=True, help='New evidence directory')
    parser.add_argument('--select-first', action='store_true', help='Select the source with a pointer click before dragging')
    args = parser.parse_args()
    app = args.app.resolve()
    if not app.is_file():
        parser.error('Frontend DLL does not exist')
    output = args.output.resolve()
    if output.exists():
        parser.error('Choose a new output directory')
    before = call(BRIDGE, 'snapshot')
    profile = before['profile']['path']
    if not profile.replace('\\', '/').endswith('/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test'):
        parser.error('Isolated FNV Frontend Test profile is not selected')
    by_name = {m['name']: m for m in before['mods']}
    moving, destination = by_name[MOVING], by_name[DESTINATION]
    if moving['state'] & 6 or destination['state'] & 6 or destination['priority'] != moving['priority'] + 1:
        parser.error('The two disabled MCM mods must be adjacent in their original order')
    original = order(before)
    swapped = original.copy()
    a, b = original.index(MOVING), original.index(DESTINATION)
    swapped[a], swapped[b] = swapped[b], swapped[a]
    baseline = profile_state(before)
    output.mkdir(parents=True)
    (output / 'baseline.json').write_text(json.dumps(baseline, indent=2))
    (output / 'app.json').write_text(json.dumps({'path': str(app), 'sha256': hashlib.sha256(app.read_bytes()).hexdigest()}, indent=2))
    phase_path = output / 'phase.json'
    seen = set()
    error = None
    process = None
    with tempfile.TemporaryDirectory(prefix='mo2-pointer-layout-') as temporary:
        layout = Path(temporary) / 'layout.json'
        layout.write_bytes((ROOT / 'frontend/artifacts/verify-paired-layout.json').read_bytes())
        env = {k: v for k, v in os.environ.items() if not k.startswith('MO2_')}
        env.update(MO2_BRIDGE_DIRECTORY=str(BRIDGE), MO2_FRONTEND_LAYOUT=str(layout),
                   MO2_VERIFY_MOD_POINTER_DRAG=str(phase_path), MO2_SCREENSHOT=str(output / 'frontend.png'))
        try:
            with (output / 'frontend.log').open('w') as log:
                process = subprocess.Popen([str(ROOT / '.tools/dotnet/dotnet'),
                    str(app)],
                    cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
                deadline = time.monotonic() + 140
                while process.poll() is None:
                    if time.monotonic() > deadline:
                        raise TimeoutError('Frontend pointer test timed out')
                    try:
                        phase = json.loads(phase_path.read_text())
                    except (FileNotFoundError, json.JSONDecodeError):
                        time.sleep(.05)
                        continue
                    if phase['Phase'] in seen:
                        time.sleep(.05)
                        continue
                    ids = xdo('search', '--onlyvisible', '--pid', process.pid, '--class', 'mo2-nexus-frontend').splitlines()
                    if len(ids) != 1:
                        raise RuntimeError('Could not identify exactly one owned frontend window')
                    window = ids[0]
                    xdo('windowactivate', '--sync', window)
                    geometry = dict(line.split('=', 1) for line in xdo('getwindowgeometry', '--shell', window).splitlines())
                    for point in (phase['Start'], phase['End']):
                        if not (int(geometry['X']) <= point['X'] < int(geometry['X']) + int(geometry['WIDTH']) and
                                int(geometry['Y']) <= point['Y'] < int(geometry['Y']) + int(geometry['HEIGHT'])):
                            raise RuntimeError('Drag point falls outside the owned window')
                    def focused():
                        if xdo('getactivewindow') != window:
                            raise RuntimeError('Another window took focus; stopping pointer input')
                    focused()
                    start, end = phase['Start'], phase['End']
                    move_pointer(start['X'], start['Y'])
                    location = dict(line.split('=', 1) for line in xdo('getmouselocation', '--shell').splitlines())
                    print('Requested pointer: ' + str(start) + '; observed X11 pointer: ' + str(location), flush=True)
                    if (int(location['X']), int(location['Y'])) != (start['X'], start['Y']):
                        raise RuntimeError('X11 pointer did not reach the requested grip')
                    time.sleep(.6)
                    if args.select_first:
                        xdo('click', 1)
                        time.sleep(.8)
                        focused()
                    held = False
                    try:
                        xdo('mousedown', 1)
                        held = True
                        time.sleep(.15)
                        for step in range(1, 13):
                            focused()
                            x = round(start['X'] + (end['X'] - start['X']) * step / 12)
                            y = round(start['Y'] + (end['Y'] - start['Y']) * step / 12)
                            move_pointer(x, y)
                            time.sleep(.055)
                        time.sleep(.2)
                    finally:
                        if held:
                            xdo('mouseup', 1)
                    seen.add(phase['Phase'])
                    (output / 'input-phases.json').write_text(json.dumps(sorted(seen)))
                    print('Pointer input delivered: ' + phase['Phase'], flush=True)
        except Exception as exc:
            error = str(exc)
        finally:
            if process is not None and process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
            # Recover only the exact swap owned by this check; never overwrite an
            # unrelated edit or switch the user's current native profile.
            current = call(BRIDGE, 'snapshot')
            if current['profile']['path'] == profile and order(current) == swapped:
                call(BRIDGE, 'setModPriority', profilePath=profile, name=MOVING, priority=moving['priority'])
            current = call(BRIDGE, 'snapshot')
            restored = current['profile']['path'] == profile and profile_state(current) == baseline
            (output / 'restoration.json').write_text(json.dumps({'nativeStateRestored': restored, 'driverError': error}, indent=2))
    lines = (output / 'frontend.log').read_text().splitlines()
    verdicts = [line for line in lines if line.startswith(('PASS ', 'FAIL '))]
    for line in verdicts:
        print(line)
    passed = (error is None and process is not None and process.returncode == 0 and restored and seen == {'up', 'down'}
              and sum(line.startswith('PASS mod pointer drag:') for line in verdicts) == 2
              and any(line.startswith('PASS mod pointer restoration:') for line in verdicts)
              and not any(line.startswith('FAIL ') for line in verdicts))
    print('Native state restored: ' + str(restored))
    if error:
        print('Driver error: ' + error)
    return 0 if passed else 1


if __name__ == '__main__':
    raise SystemExit(main())
