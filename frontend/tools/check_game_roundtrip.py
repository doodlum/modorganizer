#!/usr/bin/env python3
"""Check FNV/Skyrim frontend switching and independently verify native state."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess

from data_launch_probe import call, local
from save_delete_probe import profile_state

ROOT = Path(__file__).resolve().parents[2]


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest()


def state(endpoint):
    snapshot = call(endpoint, 'snapshot')
    profile = local(snapshot['profile']['path'])
    # Keep only hashes of selected state fields, never raw bridge/account data.
    return {
        'profile': digest(snapshot['profile']['path']),
        'rows': digest(profile_state(snapshot)),
        'pins': digest(snapshot.get('pinnedExecutables', [])),
        'executables': digest(snapshot.get('executables', [])),
        'selectedExecutable': digest(snapshot.get('selectedExecutable', '')),
        'iniHashes': {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                      for p in profile.glob('*.ini') if p.is_file()},
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=ROOT / 'frontend/MockHost/bin/Release/net9.0/MockHost.dll')
    parser.add_argument('--skyrim-bridge', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    app = args.app.resolve()
    if not app.is_file():
        parser.error('Frontend DLL does not exist')
    output = args.output.resolve()
    if output.exists():
        parser.error('Choose a new evidence directory')
    fnv = ROOT / 'frontend/artifacts/mo2-fnv-host/plugins/data/frontend-bridge'
    snapshot = call(fnv, 'snapshot')
    if not snapshot['profile']['path'].replace('\\', '/').endswith('/profiles/Frontend Test'):
        parser.error('Isolated FNV Frontend Test profile must be selected')
    endpoints = [fnv, args.skyrim_bridge.resolve()]
    before = [state(endpoint) for endpoint in endpoints]
    output.mkdir(parents=True)
    (output / 'baseline.json').write_text(json.dumps(before, indent=2))
    (output / 'app.json').write_text(json.dumps({'path': str(app),
        'sha256': hashlib.sha256(app.read_bytes()).hexdigest()}, indent=2))
    layout = output / 'layout.json'
    layout.write_bytes((ROOT / 'frontend/artifacts/verify-paired-layout.json').read_bytes())
    env = {k: v for k, v in os.environ.items() if not k.startswith('MO2_')}
    env.update(MO2_BRIDGE_DIRECTORY=str(fnv), MO2_FRONTEND_LAYOUT=str(layout),
               MO2_VERIFY_GAME_SWITCH='1', MO2_SCREENSHOT=str(output / 'frontend.png'))
    timed_out = False
    with (output / 'frontend.log').open('w') as log:
        process = subprocess.Popen([str(ROOT / '.tools/dotnet/dotnet'), str(app)],
                                   cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            process.wait(timeout=300)
        except subprocess.TimeoutExpired:
            timed_out = True
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
    after = [state(endpoint) for endpoint in endpoints]
    checks = [{key: old[key] == new[key] for key in old} for old, new in zip(before, after)]
    (output / 'state-check.json').write_text(json.dumps(checks, indent=2))
    result = (output / 'frontend.log').read_text()
    for line in result.splitlines():
        if line.startswith(('PASS ', 'FAIL ')):
            print(line)
    print('Native state unchanged:', checks)
    print('Frontend exit:', process.returncode)
    if (timed_out or process.returncode != 0 or 'FAIL ' in result or
            'PASS game switch:' not in result or result.count('PASS game launcher:') != 3 or
            result.count('PASS toolbar search: ToolsToolbarSearch') != 3 or
            not all(all(check.values()) for check in checks)):
        raise RuntimeError('Game roundtrip acceptance failed; inspect saved evidence')


if __name__ == '__main__':
    main()
