#!/usr/bin/env python3
"""Wait for an observed native MO2 process, without killing its descendants."""
import os
from itertools import chain
from pathlib import Path
import sys
import time


def native_running(instance, prefix, proc=Path('/proc'), cached=None):
    expected = str(Path(instance) / 'ModOrganizer.exe').replace('\\', '/').casefold()
    wine_prefix = os.path.realpath(Path(prefix) / 'pfx')
    # Once found, check that process first instead of repeatedly walking every
    # process on the Steam Deck. A changed/exited PID falls back to a full scan.
    for entry in chain(tuple(cached or ()), proc.iterdir()):
        if not entry.name.isdigit():
            continue
        try:
            if entry.stat().st_uid != os.getuid():
                continue
            with (entry / 'cmdline').open('rb') as stream:
                name = stream.read(16384).split(b'\0', 1)[0].decode(errors='replace')
            if name.replace('\\', '/').removeprefix('Z:').removeprefix('z:').casefold() != expected:
                continue
            if (entry / 'stat').read_text().rpartition(') ')[2].split()[0] == 'Z':
                continue
            # Only inspect the prefix field; never log process environments.
            fields = (entry / 'environ').read_bytes().split(b'\0')
            value = next((x[len(b'WINEPREFIX='):] for x in fields if x.startswith(b'WINEPREFIX=')), None)
            if value is None or os.path.realpath(os.fsdecode(value)) == wine_prefix:
                if cached is not None:
                    cached[:] = [entry]
                return True
        except PermissionError:
            # Uncertain inspection cannot authorize a duplicate native host.
            return True
        except (FileNotFoundError, ProcessLookupError):
            continue
    if cached is not None:
        cached.clear()
    return False


def wait(instance, prefix, container, result):
    observed = False
    cached = []
    while True:
        running = native_running(instance, prefix, cached=cached)
        if observed and not running:
            return
        observed = observed or running
        if not running and Path(result).exists():
            return
        try:
            os.kill(container, 0)
        except ProcessLookupError:
            if not running:
                return
        time.sleep(.2)


if __name__ == '__main__':
    wait(sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4])
