#!/usr/bin/env python3
"""Prepare/clean up a native Data activation probe; run it through the frontend verifier."""
import argparse
import hashlib
import json
from pathlib import Path
import time
import uuid


def call(endpoint, action, **arguments):
    session = json.loads((endpoint / 'endpoint.json').read_text())['session']
    ident = str(uuid.uuid4())
    request = endpoint / 'requests' / (ident + '.json')
    temporary = request.with_suffix('.tmp')
    temporary.write_text(json.dumps(dict(protocol=1, session=session, action=action, **arguments)))
    temporary.replace(request)
    response = endpoint / 'responses' / (ident + '.json')
    deadline = time.monotonic() + 30
    while not response.exists():
        if time.monotonic() > deadline:
            raise RuntimeError(f'{action} timed out; request {ident} was not retried')
        time.sleep(.05)
    data = json.loads(response.read_text())
    if data['session'] != session or data['id'] != ident:
        raise RuntimeError('Response identity mismatch')
    if not request.exists():
        response.unlink()
    if not data['ok']:
        raise RuntimeError(data['error'])
    return data['result']


def local(path):
    path = path.replace('\\', '/')
    if not path.lower().startswith('z:/'):
        raise ValueError('This Linux probe requires Overwrite on Wine Z:')
    return Path(path[2:])


def windows(path):
    return ('Z:' + str(path)).replace('/', '\\')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=['prepare', 'cleanup'])
    parser.add_argument('--bridge', type=Path, required=True)
    parser.add_argument('--spec', type=Path, required=True)
    parser.add_argument('--game-data', type=Path)
    args = parser.parse_args()
    snapshot = call(args.bridge, 'snapshot')
    profile = snapshot['profile']['path']
    overwrite = local(call(args.bridge, 'readOverwrite', profilePath=profile)['path']).resolve()
    mod = next(m['name'] for m in snapshot['mods'] if m.get('nexusId', 0) > 0)

    if args.operation == 'prepare':
        if args.spec.exists() or args.game_data is None or not args.game_data.is_dir():
            raise ValueError('Choose a new spec file and an existing game Data directory')
        folder = 'mo2-activation-probe-' + uuid.uuid4().hex + ' with spaces'
        root = overwrite / folder
        result = args.spec.resolve().with_suffix('.result.txt')
        game_probe = args.game_data.resolve() / folder / 'onlyvirtual.txt'
        if root.exists() or result.exists() or game_probe.parent.exists():
            raise ValueError('Probe destination already exists')
        root.mkdir()
        command = ('@echo off\r\n'
                   'if not exist "onlyvirtual.txt" (\r\n'
                   f'>"{windows(result)}" echo wrong-working-directory\r\nexit /b 1\r\n)\r\n'
                   f'if exist "{windows(game_probe)}" (\r\n'
                   f'>"{windows(result)}" echo hooked\r\n) else (\r\n'
                   f'>"{windows(result)}" echo plain\r\n)\r\n')
        (root / 'run.cmd').write_bytes(command.encode('utf-8'))
        (root / 'onlyvirtual.txt').write_text('Temporary VFS probe\n')
        spec = dict(folder=folder, root=str(root), result=str(result), gameProbe=str(game_probe),
                    profile=profile, hashes={p.name: digest(p) for p in root.iterdir()})
        args.spec.write_text(json.dumps(spec, indent=2))
    else:
        spec = json.loads(args.spec.read_text())
        root = Path(spec['root'])
        if (spec['profile'] != profile or not spec['folder'].startswith('mo2-activation-probe-')
                or Path(spec['folder']).name != spec['folder'] or root.parent != overwrite or root.is_symlink()
                or Path(spec['result']) != args.spec.resolve().with_suffix('.result.txt')):
            raise ValueError('Probe no longer belongs to this profile/Overwrite directory')
        if set(p.name for p in root.iterdir()) != set(spec['hashes']) or any(digest(root / name) != sha for name, sha in spec['hashes'].items()):
            raise ValueError('Probe files changed; inspect before cleanup')
        for name in spec['hashes']:
            (root / name).unlink()
        root.rmdir()
        Path(spec['result']).unlink(missing_ok=True)

    call(args.bridge, 'modMenuAction', profilePath=profile, names=[mod], path=[['All Mods', 'Refresh']])
    # Refresh starts MO2's asynchronous directory rebuild. Observe completion;
    # one successful command response does not mean Data has published it yet.
    # Never replay the mutation when a read still shows the previous tree.
    deadline = time.monotonic() + 20
    while True:
        entries = call(args.bridge, 'readDataDirectory', profilePath=profile, directory='')['entries']
        present = any(e['name'] == spec['folder'] for e in entries)
        if present == (args.operation == 'prepare'):
            break
        if time.monotonic() >= deadline:
            raise RuntimeError('Native Data did not reflect probe preparation/cleanup; inspect the existing probe before retrying')
        time.sleep(.1)
    if Path(spec['gameProbe']).exists():
        raise RuntimeError('Probe unexpectedly exists in physical game Data')
    print(f'PASS probe {args.operation}: native Data readback agrees; physical game Data untouched')


if __name__ == '__main__':
    main()
