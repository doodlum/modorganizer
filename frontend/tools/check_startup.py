#!/usr/bin/env python3
"""Measure a published frontend using an isolated paired Mods/Plugins layout."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess


def accepted(returncode, lines):
    return (returncode == 0
            and any(line.startswith('PASS startup:') for line in lines)
            and not any(line.startswith('FAIL ') for line in lines))


def main():
    root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, required=True)
    parser.add_argument('--bridge', type=Path, required=True)
    parser.add_argument('--layout', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True,
                        help='New directory for all run evidence; must not exist')
    parser.add_argument('--runs', type=int, default=3)
    args = parser.parse_args()
    if not 1 <= args.runs <= 10:
        parser.error('--runs must be between 1 and 10')
    for path in (args.app, args.layout, root / '.tools/dotnet/dotnet'):
        if not path.is_file():
            parser.error(f'Missing required file: {path}')
    if not args.bridge.is_dir():
        parser.error('Bridge directory does not exist')
    output = args.output.resolve()
    if output.exists():
        parser.error('Output directory already exists; choose a new evidence directory')
    output.mkdir(parents=True)
    (output / "app.json").write_text(json.dumps({"path": str(args.app.resolve()),
        "sha256": hashlib.sha256(args.app.read_bytes()).hexdigest()}, indent=2) + "\n")
    results = []
    for index in range(1, args.runs + 1):
        layout = output / f'run-{index}-layout.json'
        layout.write_bytes(args.layout.read_bytes())
        env = {k: v for k, v in os.environ.items() if not k.startswith('MO2_')}
        env.update(MO2_BRIDGE_DIRECTORY=str(args.bridge.resolve()),
                   MO2_FRONTEND_LAYOUT=str(layout), MO2_VERIFY_STARTUP='1',
                   MO2_SCREENSHOT=str(output / f'run-{index}.png'))
        log = output / f'run-{index}.log'
        timed_out = False
        with log.open('w') as stream:
            process = subprocess.Popen([str(root / '.tools/dotnet/dotnet'),
                                        str(args.app.resolve())], cwd=root, env=env,
                                       stdout=stream, stderr=subprocess.STDOUT)
            try:
                process.wait(timeout=90)
            except subprocess.TimeoutExpired:
                timed_out = True
            finally:
                if process.poll() is None:
                    process.terminate()
                    try:
                        process.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()
        lines = [line for line in log.read_text().splitlines()
                 if line.startswith(('STARTUP:', 'PASS startup:', 'FAIL '))]
        passed = not timed_out and accepted(process.returncode, lines)
        results.append(dict(run=index, exitCode=process.returncode,
                            timedOut=timed_out, passed=passed, verdicts=lines))
        (output / 'results.json').write_text(json.dumps(results, indent=2) + '\n')
        print(f'Run {index}: {"PASS" if passed else "FAIL"}', flush=True)
        for line in lines:
            print(line, flush=True)
    return 0 if all(result['passed'] for result in results) else 1


if __name__ == '__main__':
    raise SystemExit(main())
