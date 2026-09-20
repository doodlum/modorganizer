#!/usr/bin/env python3
"""Compare installed bridge code with this checkout without reading account data."""
import argparse
import hashlib
from pathlib import Path
import sys


def differences(source, installed):
    expected = {p.relative_to(source) for p in source.rglob('*.py') if '__pycache__' not in p.parts}
    actual = {p.relative_to(installed) for p in installed.rglob('*.py') if '__pycache__' not in p.parts}
    result = []
    for name in sorted(expected | actual):
        if name not in actual:
            result.append((str(name), 'missing'))
        elif name not in expected:
            result.append((str(name), 'unexpected'))
        elif hashlib.sha256((source / name).read_bytes()).digest() != hashlib.sha256((installed / name).read_bytes()).digest():
            result.append((str(name), 'different'))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('instances', type=Path, nargs='+', help='MO2 instance directories')
    args = parser.parse_args()
    source = Path(__file__).resolve().parents[1] / 'mo2-plugin/nexus_frontend_bridge'
    failed = False
    for instance in args.instances:
        delta = differences(source, instance / 'plugins/nexus_frontend_bridge')
        print(f'{"FAIL" if delta else "PASS"} bridge code: {instance}')
        for name, reason in delta:
            print(f'  {name}: {reason}')
        failed |= bool(delta)
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
