#!/usr/bin/env python3
"""Physically split and rejoin a tab in an isolated frontend layout."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time

from check_mod_pointer_drag import ROOT, BRIDGE, xdo, move_pointer
from data_launch_probe import call
from save_delete_probe import profile_state


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=ROOT / 'frontend/MockHost/bin/Release/net9.0/MockHost.dll')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    app, output = args.app.resolve(), args.output.resolve()
    if not app.is_file() or output.exists():
        parser.error('Require an existing frontend DLL and a new output directory')
    before = call(BRIDGE, 'snapshot')
    if not before['profile']['path'].replace('\\', '/').endswith('/profiles/Frontend Test'):
        parser.error('Select the isolated FNV Frontend Test profile')
    output.mkdir(parents=True)
    data = json.loads((ROOT / 'frontend/artifacts/verify-paired-layout.json').read_text())
    workspace = data['Workspaces'][0]
    panel, second = workspace['Panels']
    panel['Tabs'] += second['Tabs']
    panel.update(Width=1, Active=True)
    workspace['Panels'] = [panel]
    layout = output / 'layout.json'
    layout.write_text(json.dumps(data, indent=2))
    (output / 'input-layout.json').write_text(json.dumps(data, indent=2))
    (output / 'app.json').write_text(json.dumps({'path': str(app),
        'sha256': hashlib.sha256(app.read_bytes()).hexdigest(),
        'assemblies': {name: hashlib.sha256((app.parent / name).read_bytes()).hexdigest()
                       for name in (app.name, 'NexusMods.App.UI.dll')}}, indent=2))
    phase_file = output / 'phase.json'
    env = {k: v for k, v in os.environ.items() if not k.startswith('MO2_')}
    env.update(MO2_BRIDGE_DIRECTORY=str(BRIDGE), MO2_FRONTEND_LAYOUT=str(layout),
               MO2_SCREENSHOT=str(output / 'frontend.png'), MO2_VERIFY_TAB_POINTER=str(phase_file))
    seen, error = set(), None
    with (output / 'frontend.log').open('w') as log:
        process = subprocess.Popen([str(ROOT / '.tools/dotnet/dotnet'), str(app)],
                                   cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
        window = None
        try:
            deadline = time.monotonic() + 100
            while process.poll() is None:
                if time.monotonic() > deadline:
                    raise TimeoutError('Tab pointer check timed out')
                try:
                    phase = json.loads(phase_file.read_text())
                except (FileNotFoundError, json.JSONDecodeError):
                    time.sleep(.05)
                    continue
                if phase['Phase'] in seen:
                    time.sleep(.05)
                    continue
                ids = xdo('search', '--onlyvisible', '--pid', process.pid, '--class', 'mo2-nexus-frontend').splitlines()
                if window is None:
                    if len(ids) != 1:
                        raise RuntimeError('Expected one initial owned frontend window')
                    window = ids[0]
                if window not in ids or int(xdo('getwindowpid', window)) != process.pid:
                    raise RuntimeError('Owned frontend window disappeared')
                xdo('windowactivate', '--sync', window)
                geometry = dict(line.split('=', 1) for line in xdo('getwindowgeometry', '--shell', window).splitlines())
                for point in (phase['Start'], phase['End']):
                    if not (int(geometry['X']) <= point['X'] < int(geometry['X']) + int(geometry['WIDTH']) and
                            int(geometry['Y']) <= point['Y'] < int(geometry['Y']) + int(geometry['HEIGHT'])):
                        raise RuntimeError('Input point is outside the owned window')
                def focus():
                    if xdo('getactivewindow') != window:
                        raise RuntimeError('Another window took focus')
                focus()
                start, end = phase['Start'], phase['End']
                move_pointer(start['X'], start['Y'])
                time.sleep(.4)
                held = False
                try:
                    focus()
                    xdo('mousedown', 1)
                    held = True
                    time.sleep(.15)
                    for step in range(1, 21):
                        focus()
                        move_pointer(round(start['X'] + (end['X'] - start['X']) * step / 20),
                                     round(start['Y'] + (end['Y'] - start['Y']) * step / 20))
                        time.sleep(.06)
                    time.sleep(.3)
                finally:
                    if held:
                        xdo('mouseup', 1)
                seen.add(phase['Phase'])
                print('Pointer input delivered:', phase['Phase'], flush=True)
        except Exception as exc:
            error = str(exc)
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill(); process.wait()
    after = call(BRIDGE, 'snapshot')
    unchanged = (before['profile']['path'] == after['profile']['path'] and profile_state(before) == profile_state(after))
    verdicts = [line for line in (output / 'frontend.log').read_text().splitlines() if line.startswith(('PASS ', 'FAIL '))]
    for line in verdicts:
        print(line)
    result = {'exitCode': process.returncode, 'nativeStateUnchanged': unchanged, 'phases': sorted(seen), 'driverError': error}
    (output / 'result.json').write_text(json.dumps(result, indent=2))
    print(result)
    return 0 if (process.returncode == 0 and not error and unchanged and seen == {'split', 'join'} and
                 sum(line.startswith('PASS tab pointer:') for line in verdicts) == 2 and
                 sum(line.startswith('PASS tab history:') for line in verdicts) == 2 and
                 not any(line.startswith('FAIL ') for line in verdicts)) else 1


if __name__ == '__main__':
    raise SystemExit(main())
