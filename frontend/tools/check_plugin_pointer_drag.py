#!/usr/bin/env python3
"""Physical plugin-grip check on the isolated FNV clone, with state restoration."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
from check_mod_pointer_drag import ROOT, BRIDGE, xdo, move_pointer
from data_launch_probe import call
from save_delete_probe import profile_state

CLONE = 'Frontend Clone Test'
MOD = 'MCM Author Examples'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=ROOT / 'frontend/MockHost/bin/Release/net9.0/NexusModsApp.dll', help='Frontend DLL to test')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    app = args.app.resolve()
    if not app.is_file():
        parser.error('Frontend DLL does not exist')
    output = args.output.resolve()
    if output.exists():
        parser.error('Choose a new output directory')
    before = call(BRIDGE, 'snapshot')
    original_name = before['profile']['name']
    if original_name != 'Frontend Test' or not before['profile']['path'].replace('\\', '/').endswith('/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test'):
        parser.error('The isolated FNV working profile must be selected')
    baseline = profile_state(before)
    clone_dir = ROOT / 'frontend/artifacts/mo2-fnv-host/profiles' / CLONE
    files = ['modlist.txt', 'plugins.txt', 'loadorder.txt', 'lockedorder.txt']
    saved = {name: (clone_dir / name).read_bytes() if (clone_dir / name).exists() else None for name in files}
    output.mkdir()
    backup = output / 'profile-backup'; backup.mkdir()
    for name, data in saved.items():
        if data is not None:
            (backup / name).write_bytes(data)
    (output / 'baseline.json').write_text(json.dumps(baseline, indent=2))
    (output / 'app.json').write_text(json.dumps({'path': str(app), 'sha256': hashlib.sha256(app.read_bytes()).hexdigest()}, indent=2))
    phase_path = output / 'phase.json'
    seen = set(); error = None; process = None; clone_before = None
    restored = False; clone_restored = False
    try:
        clone_before = call(BRIDGE, 'selectProfile', name=CLONE, profilePath=before['profile']['path'])
        example = next(m for m in clone_before['mods'] if m['name'] == MOD)
        if example['state'] & 6:
            raise RuntimeError('Fixture mod is already enabled; refusing to change it')
        call(BRIDGE, 'setModActive', profilePath=clone_before['profile']['path'], name=MOD, enabled=True)
        deadline = time.monotonic() + 25
        while True:
            prepared = call(BRIDGE, 'snapshot')
            movable = [p for p in prepared['plugins'] if p['name'].startswith('MCM Example') and p.get('canMove')]
            if len(movable) >= 2:
                break
            if time.monotonic() > deadline:
                raise RuntimeError('Example plugins did not become available')
            time.sleep(.2)
        with tempfile.TemporaryDirectory(prefix='mo2-plugin-pointer-') as temporary:
            layout = Path(temporary) / 'layout.json'
            layout.write_bytes((ROOT / 'frontend/artifacts/verify-paired-layout.json').read_bytes())
            env = {k: v for k, v in os.environ.items() if not k.startswith('MO2_')}
            env.update(MO2_BRIDGE_DIRECTORY=str(BRIDGE), MO2_FRONTEND_LAYOUT=str(layout),
                       MO2_VERIFY_PLUGIN_POINTER_DRAG=str(phase_path), MO2_SCREENSHOT=str(output / 'frontend.png'))
            with (output / 'frontend.log').open('w') as log:
                process = subprocess.Popen([str(ROOT / '.tools/dotnet/dotnet'),
                    str(app)],
                    cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
                owned_window = None
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
                    if owned_window is None:
                        if len(ids) != 1:
                            raise RuntimeError('Could not identify exactly one initial frontend window')
                        owned_window = ids[0]
                    if owned_window not in ids or int(xdo('getwindowpid', owned_window)) != process.pid:
                        raise RuntimeError('The original frontend window is no longer visible or owned')
                    # Multi-row drag visuals can leave a transient owned window.
                    # Never switch our input target to a newly discovered window.
                    window = owned_window
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
                    if phase.get('Action') == 'click':
                        focused()
                        control = phase.get('Control', False)
                        try:
                            if control:
                                xdo('keydown', 'ctrl')
                            focused()
                            xdo('click', 1)
                        finally:
                            if control:
                                xdo('keyup', 'ctrl')
                        seen.add(phase['Phase'])
                        Path(str(phase_path) + '.done').write_text(phase['Phase'])
                        (output / 'input-phases.json').write_text(json.dumps(sorted(seen)))
                        continue
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
                    Path(str(phase_path) + '.done').write_text(phase['Phase'])
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
                process.kill(); process.wait()
        current = call(BRIDGE, 'snapshot')
        if current['profile']['name'] != CLONE:
            raise RuntimeError(f'Fixture is not active; no files restored. Earlier error: {error}')
        if clone_before is not None:
            example = next(m for m in clone_before['mods'] if m['name'] == MOD)
            call(BRIDGE, 'setModActive', profilePath=current['profile']['path'], name=MOD, enabled=bool(example['state'] & 2))
        call(BRIDGE, 'selectProfile', name=original_name, profilePath=current['profile']['path'])
        # MO2 has flushed and released the inactive clone. Restore only the four
        # order/state files backed up for this test, then ask MO2 to reload them.
        for name, data in saved.items():
            path = clone_dir / name
            if data is None:
                path.unlink(missing_ok=True)
            else:
                path.write_bytes(data)
        try:
            restored_clone = call(BRIDGE, 'selectProfile', name=CLONE, profilePath=before['profile']['path'])
            clone_restored = clone_before is not None and profile_state(restored_clone) == profile_state(clone_before)
        finally:
            active = call(BRIDGE, 'snapshot')
            restored_original = call(BRIDGE, 'selectProfile', name=original_name, profilePath=active['profile']['path'])
        restored = (restored_original['profile']['path'] == before['profile']['path']
                    and profile_state(restored_original) == baseline)
        (output / 'restoration.json').write_text(json.dumps(dict(nativeStateRestored=restored,
            cloneStateRestored=clone_restored, driverError=error), indent=2))
    lines = (output / 'frontend.log').read_text().splitlines() if (output / 'frontend.log').exists() else []
    verdicts = [line for line in lines if line.startswith(('PASS ', 'FAIL '))]
    print('\n'.join(verdicts))
    passed = (error is None and process is not None and process.returncode == 0 and restored and clone_restored
        and seen == {'up', 'down', 'fixed', 'multi-down', 'multi-up',
                    'multi-down-select-0', 'multi-down-select-1', 'multi-up-select-0', 'multi-up-select-1'} and sum(line.startswith('PASS plugin pointer drag:') for line in verdicts) == 4
        and any(line.startswith('PASS plugin pointer restriction:') for line in verdicts)
        and any(line.startswith('PASS plugin pointer restoration:') for line in verdicts)
        and not any(line.startswith('FAIL ') for line in verdicts))
    print(f'Working profile restored: {restored}; clone restored: {clone_restored}; driver error: {error}')
    return 0 if passed else 1


if __name__ == '__main__':
    raise SystemExit(main())
