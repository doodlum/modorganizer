#!/usr/bin/env python3
"""Exercise the C# reader against temporary MO2-format instances."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile

with tempfile.TemporaryDirectory(prefix='mo2-reader-check-') as directory:
    root = Path(directory)
    instances = []
    for index in range(2):
        instance = root / f'instance{index}'
        profile = instance / 'custom profiles' / 'Default'
        profile.mkdir(parents=True)
        (instance / 'mods' / 'Enabled Mod').mkdir(parents=True)
        downloads = instance / 'downloads'
        downloads.mkdir()
        (downloads / 'archive.7z').write_bytes(b'123')
        (downloads / 'archive.7z.meta').write_text('[General]\n')
        (downloads / 'pending.zip.unfinished').write_bytes(b'1')
        (instance / 'ModOrganizer.ini').write_text(
            f'[General]\ngameName=Game {index}\nselected_profile=@ByteArray(Default)\n'
            '[Settings]\nprofiles_directory=%BASE_DIR%/custom profiles\n')
        (profile / 'modlist.txt').write_text('\ufeff# generated\r\n-Missing Mod\r\n+Enabled Mod\r\n*Unmanaged DLC\r\n+enabled mod\r\n')
        (profile / 'loadorder.txt').write_text('# generated\nBase.esm\nExample.esp\n')
        (profile / 'plugins.txt').write_text('# generated\n*example.ESP\nOther.esp\n')
        instances.append(instance)
    result = json.loads(subprocess.check_output([*sys.argv[1:], '--inspect-mo2', *map(str, instances)], text=True))
    assert len(result['instances']) == 2
    for instance in result['instances']:
        assert instance['SelectedProfile'] == 'Default'
        profile = instance['Profiles'][0]
        assert len(profile['ModEntries']) == 3
        assert [x['Name'] for x in profile['ModEntries']] == ['Missing Mod', 'Enabled Mod', 'Unmanaged DLC']
        assert profile['ModEntries'][0]['DirectoryExists'] is False
        assert profile['ModEntries'][1]['DirectoryExists'] is True
        assert profile['ModEntries'][2]['Unmanaged'] is True
        assert [x['AsteriskMarker'] for x in profile['Plugins']] == [None, True, False]
        assert len(instance['Downloads']) == 2
        assert any(x['HasMetadata'] and x['Bytes'] == 3 for x in instance['Downloads'])
        assert any(x['Partial'] for x in instance['Downloads'])
print('PASS: multiple instances; path override; BOM/CRLF; duplicate names; missing/unmanaged mods; plugin markers/order; downloads and metadata')
