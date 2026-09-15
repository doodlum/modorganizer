#!/usr/bin/env python3
"""Attribute the cold switch's largest pointer gap to opt-in template timings.

Build is nested within realization. Inclusive totals must not be added together.
This instrumented run is diagnostic evidence, not a normal-use speed benchmark.
"""
import argparse
from collections import defaultdict
import json
from pathlib import Path


def analyze(report):
    phases = report['SlowPhases']
    switch = next(row for row in phases if row['Phase'].startswith('Switch step:'))
    pointers = report['NativePointerSamples']
    intervals = [(a['UtcTicks'], b['UtcTicks']) for a, b in zip(pointers, pointers[1:])
                 if switch['StartUtcTicks'] <= b['UtcTicks'] < switch['StartUtcTicks'] + 50_000_000]
    if not intervals:
        raise ValueError('No native pointer samples during the cold switch')
    start, end = max(intervals, key=lambda pair: pair[1] - pair[0])
    result = {'instrumented': True, 'gapMs': (end - start) / 10_000,
              'gapStartUtcTicks': start, 'gapEndUtcTicks': end, 'groups': {}}
    for prefix in ('Template build: ', 'Template realization: '):
        rows = [row for row in phases if row['Phase'].startswith(prefix)
                and row['StartUtcTicks'] < end and row['EndUtcTicks'] > start]
        groups = defaultdict(list)
        clipped = []
        for row in rows:
            left, right = max(start, row['StartUtcTicks']), min(end, row['EndUtcTicks'])
            clipped.append((left, right))
            groups[row['Phase'][len(prefix):]].append((right - left) / 10_000)
        union, previous = 0, start
        for left, right in sorted(clipped):
            union += max(0, right - max(left, previous))
            previous = max(previous, right)
        result['groups'][prefix.strip(': ')] = {
            'count': len(rows), 'unionMs': union / 10_000,
            'types': [{'type': name, 'count': len(values), 'inclusiveMs': sum(values), 'maxMs': max(values)}
                      for name, values in sorted(groups.items(), key=lambda pair: -sum(pair[1]))]}
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('report', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    result = analyze(json.loads(args.report.read_text()))
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({key: value for key, value in result.items() if key != 'groups'}))
    for name, group in result['groups'].items():
        print(name, 'count:', group['count'], 'union ms:', group['unionMs'])
        for row in group['types'][:5]:
            print(json.dumps(row))
