#!/usr/bin/env python3
"""Create and verify disposable copies for the native bulk-save deletion check.

Only newly named copies may be cleaned up. Original saves and profile state must
remain unchanged. This helper never accepts a native deletion confirmation.
"""
import argparse
import hashlib
import json
from pathlib import Path
import uuid

from data_launch_probe import call


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def local_saves(instance, native):
    executable = ('Z:' + str(instance / 'ModOrganizer.exe')).replace('/', '\\').casefold()
    prefixes = []
    for process in Path('/proc').iterdir():
        if not process.name.isdigit():
            continue
        try:
            if (process / 'cmdline').read_bytes().decode(errors='replace').split('\0')[0].casefold() != executable:
                continue
            prefixes.extend(entry.split('=', 1)[1] for entry in
                            (process / 'environ').read_bytes().decode().split('\0')
                            if entry.startswith('WINEPREFIX='))
        except OSError:
            continue
    if len(prefixes) != 1:
        raise ValueError('Expected one running host with its Wine prefix')
    native = native.replace('\\', '/')
    if len(native) < 3 or native[1:3] != ':/':
        raise ValueError('Unexpected native save directory')
    return (Path(prefixes[0]) / 'dosdevices' / (native[0].lower() + ':') / native[3:]).resolve()


def profile_state(snapshot):
    return {key: [{field: row.get(field) for field in ('name', 'state', 'priority')}
                  for row in snapshot[key]] for key in ('mods', 'plugins')}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=['prepare', 'check', 'cleanup'])
    parser.add_argument('--instance', type=Path, required=True)
    parser.add_argument('--spec', type=Path, required=True)
    args = parser.parse_args()
    instance = args.instance.resolve()
    endpoint = instance / 'plugins/data/frontend-bridge'
    snapshot = call(endpoint, 'snapshot')
    profile = snapshot['profile']['path']
    native = call(endpoint, 'readSaves', profilePath=profile)
    root = local_saves(instance, native['directory'])
    if not root.is_dir():
        raise ValueError('Save directory unavailable')

    if args.operation == 'prepare':
        if args.spec.exists() or not native['saves']:
            raise ValueError('Choose a new specification and a host with a real save')
        filename = native['saves'][0]['file']
        if Path(filename).name != filename:
            raise ValueError('This probe requires a top-level native save')
        source = root / filename
        companions = [path for path in root.iterdir() if path.is_file() and path.stem == source.stem]
        if source not in companions or len(companions) < 2 or any(path.is_symlink() for path in companions):
            raise ValueError('Expected a real save and at least one regular companion file')
        originals = {str(path.relative_to(root)): digest(path) for path in root.rglob('*') if path.is_file()}
        prefix = 'mo2-save-delete-probe-' + uuid.uuid4().hex
        selected, files, copies = [], {}, []
        for index in (1, 2):
            stem = prefix + '-' + str(index)
            selected.append(stem + source.suffix)
            for original in companions:
                name = stem + original.suffix
                if (root / name).exists():
                    raise ValueError('Probe destination already exists')
                files[name] = digest(original)
                copies.append((original, root / name))
        spec = dict(profile=profile, root=str(root), prefix=prefix, selected=selected, files=files,
                    originals=originals, originalSaves=[row['file'] for row in native['saves']],
                    state=profile_state(snapshot))
        # Record recovery ownership before creating files, including a partial copy.
        with args.spec.open('x') as output:
            json.dump(spec, output, indent=2)
        for original, destination in copies:
            with destination.open('xb') as output:
                output.write(original.read_bytes())
        refreshed = call(endpoint, 'readSaves', profilePath=profile)
        if not set(selected).issubset({row['file'] for row in refreshed['saves']}):
            raise RuntimeError('MO2 did not parse both disposable save copies; inspect before retrying')
        print(f'PASS save probe prepare: two native saves and {len(files) - 2} companion copies created')
        return

    spec = json.loads(args.spec.read_text())
    if spec['profile'] != profile or Path(spec['root']) != root or not spec['prefix'].startswith('mo2-save-delete-probe-'):
        raise ValueError('Probe belongs to a different host/profile/save directory')
    for name, expected in spec['files'].items():
        if Path(name).name != name or not name.startswith(spec['prefix'] + '-'):
            raise ValueError('Invalid probe filename')
        path = root / name
        if path.is_symlink() or (path.exists() and digest(path) != expected):
            raise ValueError('Probe bytes changed; inspect before cleanup')
    if args.operation == 'cleanup':
        for name in spec['files']:
            (root / name).unlink(missing_ok=True)
    after = {str(path.relative_to(root)): digest(path) for path in root.rglob('*') if path.is_file()}
    if after != spec['originals']:
        raise RuntimeError('Save-directory contents differ from the original baseline')
    restored = call(endpoint, 'readSaves', profilePath=profile)
    if sorted(row['file'] for row in restored['saves']) != sorted(spec['originalSaves']):
        raise RuntimeError('Native save list differs from the original baseline')
    if profile_state(call(endpoint, 'snapshot')) != spec['state']:
        raise RuntimeError('Native mod/plugin state differs from the baseline')
    print(f'PASS save probe {args.operation}: original bytes, native save list, mod/plugin order and states unchanged; copies absent')


if __name__ == '__main__':
    main()
