#!/usr/bin/env python3
"""Drive actual pointer-only save hovering on a 1280x800 Linux desktop.

Close the interactive frontend first. This driver owns its test frontend and
never sends clicks or keyboard events, nor starts/stops the native MO2 host.
"""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import time

ROOT = Path(__file__).resolve().parents[2]


def command(*arguments, optional=False):
    result = subprocess.run(arguments, text=True, capture_output=True, timeout=15)
    if result.returncode and not optional:
        raise RuntimeError(f'{arguments[0]} failed: {result.stderr.strip()}')
    return result.stdout.strip()


def running_processes():
    for path in Path('/proc').iterdir():
        if not path.name.isdigit():
            continue
        try:
            yield int(path.name), (path / 'cmdline').read_bytes().decode(errors='replace').split('\0')
        except OSError:
            continue


def frontend_running():
    return any(any(arg.endswith('MockHost.dll') or 'MockHost.csproj' in arg for arg in args)
               for _, args in running_processes())


def native_pid(instance):
    expected = ('Z:' + str(instance / 'ModOrganizer.exe')).replace('/', '\\').casefold()
    pids = [pid for pid, args in running_processes() if args and args[0].casefold() == expected]
    if len(pids) != 1:
        raise RuntimeError('Expected exactly one running native host for this instance')
    return pids[0]


def previews(pid):
    return command('xdotool', 'search', '--all', '--onlyvisible', '--pid', str(pid),
                   '--name', '^Save preview$', optional=True).split()


def write_json(path, data):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(data))
    temporary.replace(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--instance', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True, help='New directory for log and screenshots')
    args = parser.parse_args()
    if frontend_running():
        raise RuntimeError('An interactive frontend is running. Leave it undisturbed; close it before this pointer test.')
    instance = args.instance.resolve()
    host = native_pid(instance)
    if previews(host):
        raise RuntimeError('A native preview is already visible; this test must start with none')
    screen = re.search(r'current\s+(\d+)\s+x\s+(\d+)', command('xrandr', '--current'))
    if not screen or tuple(map(int, screen.groups())) != (1280, 800):
        raise RuntimeError('This pointer mapping is verified only for a 1280x800 desktop')
    binary = ROOT / 'frontend/MockHost/bin/Release/net9.0/MockHost.dll'
    if not binary.is_file():
        raise RuntimeError('Build Release first')
    args.output.mkdir(parents=True, exist_ok=False)
    output = args.output.resolve()
    from evdev import UInput, AbsInfo, ecodes as e
    with tempfile.TemporaryDirectory(prefix='mo2-save-hover-') as temporary:
        temporary = Path(temporary)
        phase = temporary / 'phase.json'
        layout = temporary / 'layout.json'
        layout.write_bytes((ROOT / 'frontend/artifacts/verify-paired-layout.json').read_bytes())
        env = {key: value for key, value in os.environ.items() if not key.startswith('MO2_')}
        env.update(MO2_BRIDGE_DIRECTORY=str(instance / 'plugins/data/frontend-bridge'),
                   MO2_FRONTEND_LAYOUT=str(layout), MO2_VERIFY_SAVE_HOVER=str(phase))
        log_path = output / 'frontend.log'
        observed = []
        # BTN_LEFT only declares a pointer device. No key/button events are emitted.
        capabilities = {e.EV_KEY: [e.BTN_LEFT], e.EV_ABS: [
            (e.ABS_X, AbsInfo(0, 0, 1279, 0, 0, 0)),
            (e.ABS_Y, AbsInfo(0, 0, 799, 0, 0, 0))]}
        with UInput(capabilities, name='MO2 save hover test pointer', input_props=[e.INPUT_PROP_POINTER]) as pointer, log_path.open('w') as log:
            process = subprocess.Popen([str(ROOT / '.tools/dotnet/dotnet'), str(binary)],
                                       cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
            try:
                deadline = time.monotonic() + 180
                seen = set()
                previous = None
                while process.poll() is None and time.monotonic() < deadline:
                    verdicts = [line for line in log_path.read_text().splitlines()
                                if line.startswith(('PASS save hover UI:', 'FAIL save hover UI:'))]
                    if verdicts:
                        print(verdicts[-1])
                        if not verdicts[-1].startswith('PASS'):
                            return 1
                        write_json(output / 'observed.json', observed)
                        return 0
                    if not phase.exists():
                        time.sleep(.05)
                        continue
                    data = json.loads(phase.read_text())
                    stage = data['stage']
                    if stage in seen:
                        time.sleep(.05)
                        continue
                    seen.add(stage)
                    try:
                        if data['pid'] != process.pid:
                            raise RuntimeError('Phase belongs to a different frontend')
                        if data['x'] is not None:
                            if not 0 <= data['x'] < 1280 or not 0 <= data['y'] < 800:
                                raise RuntimeError('Pointer destination lies outside this desktop')
                            command('xdotool', 'windowactivate', str(data['window']))
                            if command('xdotool', 'getactivewindow') != str(data['window']):
                                raise RuntimeError('Test frontend did not become active')
                            for x in (max(0, data['x'] - 1), data['x']):
                                pointer.write(e.EV_ABS, e.ABS_X, x)
                                pointer.write(e.EV_ABS, e.ABS_Y, data['y'])
                                pointer.syn()
                                time.sleep(.05)
                        started = time.monotonic()
                        limit = started + (5 if data['visible'] else 1)
                        found = []
                        while time.monotonic() < limit:
                            found = previews(host)
                            if data['visible']:
                                if len(found) == 1 and (stage != 'second' or found[0] != previous):
                                    break
                            elif not found:
                                break
                            time.sleep(.05)
                        else:
                            raise RuntimeError('Native preview visibility/row change did not match ' + stage)
                        latency = time.monotonic() - started
                        if data['visible']:
                            previous = found[0]
                            # Longer than the native lease: staying visible proves renewal.
                            time.sleep(1.7)
                            if previews(host) != found:
                                raise RuntimeError('Hover preview did not remain renewed during ' + stage + '; initial=' + str(found) + '; final=' + str(previews(host)))
                            if command('xdotool', 'getactivewindow') != str(data['window']):
                                raise RuntimeError('Hover preview stole focus')
                            command('spectacle', '-b', '-n', '-f', '-o', str(output / (stage + '.png')))
                        observed.append(dict(stage=stage, visible=data['visible'], latencySeconds=latency))
                        write_json(output / 'observed.json', observed)
                        write_json(Path(str(phase) + '.reply'), dict(stage=stage, ok=True))
                    except Exception as error:
                        write_json(output / 'failure.json', dict(stage=stage, error=str(error), observed=observed))
                        write_json(Path(str(phase) + '.reply'), dict(stage=stage, ok=False, error=str(error)))
                        raise
                raise RuntimeError('No terminal hover verdict; see ' + str(log_path))
            finally:
                if process.poll() is None:
                    process.terminate()
                    try:
                        process.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()
                # Host lease expires if frontend termination bypassed normal disposal.
                until = time.monotonic() + 3
                while previews(host) and time.monotonic() < until:
                    time.sleep(.1)
                if previews(host):
                    raise RuntimeError('Native preview outlived its stopped frontend lease')


if __name__ == '__main__':
    raise SystemExit(main())
