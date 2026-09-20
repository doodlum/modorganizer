#!/usr/bin/env python3
"""Own one disposable, plugin-free mod for the live filter refresh check."""
import argparse
import json
from pathlib import Path
import time
import uuid

from data_launch_probe import call, local
from save_delete_probe import profile_state


def normalized(state):
    return {key: sorted(rows, key=lambda row: row['name']) for key, rows in state.items()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=['prepare', 'cleanup'])
    parser.add_argument('--spec', type=Path, required=True)
    args = parser.parse_args()
    instance = Path(__file__).resolve().parents[1] / 'artifacts/mo2-fnv-host'
    endpoint = instance / 'plugins/data/frontend-bridge'
    snapshot = call(endpoint, 'snapshot')
    profile = snapshot['profile']['path']
    mods = local(snapshot['instance']['modsPath']).resolve()
    if mods != (instance / 'mods').resolve():
        raise ValueError('Probe requires the isolated FNV host mods directory')

    def refresh(name):
        call(endpoint, 'modMenuAction', profilePath=profile, names=[name], path=[['All Mods', 'Refresh']])

    if args.operation == 'prepare':
        if args.spec.exists():
            raise ValueError('Choose a new specification; existing probes must be cleaned first')
        name = 'mo2-filter-refresh-probe-' + uuid.uuid4().hex
        marker = uuid.uuid4().hex
        row = next(mod['name'] for mod in snapshot['mods'] if mod.get('nexusId', 0) > 0)
        spec = {'name': name, 'marker': marker, 'profile': profile, 'refreshRow': row,
                'state': profile_state(snapshot)}
        # Persist ownership before creating anything, so partial setup is recoverable.
        args.spec.write_text(json.dumps(spec, indent=2) + '\n')
        folder = mods / name
        folder.mkdir()
        (folder / 'frontend-filter-probe.txt').write_text(marker)
        (folder / 'meta.ini').write_text('[General]\nmodid=0\nversion=1.0\n')
        refresh(row)
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            current = call(endpoint, 'snapshot')
            found = next((mod for mod in current['mods'] if mod['name'] == name), None)
            if found is not None:
                if found['state'] & 6:
                    raise ValueError('Disposable mod unexpectedly started active')
                print(name)
                return
            time.sleep(.1)
        raise RuntimeError('Disposable mod did not appear; ownership specification retained')

    spec = json.loads(args.spec.read_text())
    name = spec['name']
    if profile != spec['profile'] or not name.startswith('mo2-filter-refresh-probe-') or Path(name).name != name:
        raise ValueError('Profile or probe identity mismatch')
    folder = mods / name
    if folder.is_symlink() or (folder / 'frontend-filter-probe.txt').read_text() != spec['marker']:
        raise ValueError('Probe ownership marker mismatch')
    paths = list(folder.iterdir())
    if {path.name for path in paths} != {'frontend-filter-probe.txt', 'meta.ini'} or any(path.is_symlink() or not path.is_file() for path in paths):
        raise ValueError('Unexpected probe contents; nothing removed')
    found = next((mod for mod in snapshot['mods'] if mod['name'] == name), None)
    if found is not None and found['state'] & 2:
        call(endpoint, 'setModActive', profilePath=profile, name=name, enabled=False)
    for path in paths:
        path.unlink()
    folder.rmdir()
    refresh(spec['refreshRow'])
    deadline = time.monotonic() + 20
    while time.monotonic() < deadline:
        current = call(endpoint, 'snapshot')
        if normalized(profile_state(current)) == normalized(spec['state']):
            report = {'profileUnchanged': current['profile']['path'] == profile,
                      'stateUnchanged': True, 'fixtureRemoved': not folder.exists(),
                      'mods': len(current['mods']), 'plugins': len(current['plugins'])}
            args.spec.with_suffix('.restoration.json').write_text(json.dumps(report, indent=2) + '\n')
            assert report['profileUnchanged']
            print(json.dumps(report))
            return
        time.sleep(.1)
    raise RuntimeError('Original mod/plugin state did not match after cleanup')


if __name__ == '__main__':
    main()
