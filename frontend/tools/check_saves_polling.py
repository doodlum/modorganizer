#!/usr/bin/env python3
"""Verify live save additions/removals with disposable copies and no UI refresh."""
import argparse
import os
from pathlib import Path
import subprocess
import sys
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--instance', type=Path, required=True)
    parser.add_argument('--tag', default=str(time.time_ns()))
    args = parser.parse_args()
    if not args.tag or any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_' for c in args.tag):
        raise ValueError('Use a simple artifact tag')
    root = Path(__file__).resolve().parents[2]
    artifacts = root / 'frontend/artifacts'
    artifacts.mkdir(exist_ok=True)
    spec = artifacts / ('saves-polling-' + args.tag + '.json')
    layout = artifacts / ('saves-polling-' + args.tag + '-layout.json')
    log = artifacts / ('saves-polling-' + args.tag + '.log')
    if spec.exists() or log.exists() or layout.exists():
        raise ValueError('Choose a new tag')
    source = artifacts / 'verify-paired-layout.json'
    if source.exists():
        layout.write_bytes(source.read_bytes())
    env = {key: value for key, value in os.environ.items()
           if not key.startswith('MO2_VERIFY_') and key not in ('MO2_FIXTURES', 'MO2_SCREENSHOT')}
    env.update(MO2_BRIDGE_DIRECTORY=str(args.instance.resolve() / 'plugins/data/frontend-bridge'),
               MO2_FRONTEND_LAYOUT=str(layout), MO2_VERIFY_SAVES_POLLING='1', MO2_SAVES_POLL_PROBE=str(spec))

    def probe(operation):
        subprocess.run([sys.executable, str(root / 'frontend/tools/save_delete_probe.py'), operation,
                        '--instance', str(args.instance.resolve()), '--spec', str(spec)], check=True)

    prepared = cleaned = False
    with log.open('w') as output:
        process = subprocess.Popen([str(root / '.tools/dotnet/dotnet'),
                                    str(root / 'frontend/MockHost/bin/Release/net9.0/MockHost.dll')],
                                   env=env, stdout=output, stderr=subprocess.STDOUT)
        try:
            end = time.monotonic() + 180
            while process.poll() is None and time.monotonic() < end:
                text = log.read_text()
                failures = [line for line in text.splitlines() if line.startswith('FAIL Saves polling:')]
                if failures:
                    raise RuntimeError(failures[0])
                if not prepared and Path(str(spec) + '.ready').exists():
                    probe('prepare')
                    prepared = True
                    Path(str(spec) + '.prepared').write_text('prepared')
                if prepared and not cleaned and Path(str(spec) + '.added').exists():
                    probe('cleanup')
                    cleaned = True
                passed = [line for line in text.splitlines() if line.startswith('PASS native Saves polling:')]
                if passed:
                    if not cleaned:
                        raise RuntimeError('Native success was reported before disposable-file cleanup')
                    print(passed[0])
                    return
                time.sleep(.1)
            raise RuntimeError('Save polling verification ended without a native verdict; inspect its log')
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
            if spec.exists() and not cleaned:
                probe('cleanup')


if __name__ == '__main__':
    main()
