#!/usr/bin/env python3
"""Cache public Nexus artwork, keeping the supplied API credential out of output."""
import argparse
import json
import os
import subprocess
from pathlib import Path
from urllib.request import Request, urlopen
from urllib.parse import urlsplit

parser = argparse.ArgumentParser()
parser.add_argument('game'); parser.add_argument('mod_id',type=int); parser.add_argument('--key-file',type=Path,required=True)
args = parser.parse_args()
if not args.game.isalnum() or args.mod_id <= 0: parser.error('Invalid Nexus mod identity')
try:
    key = args.key_file.read_text(encoding='utf-8-sig').strip()
    request = Request(f'https://api.nexusmods.com/v1/games/{args.game}/mods/{args.mod_id}.json',headers={'apikey':key,'User-Agent':'MO2-Nexus-Frontend/0.1'})
    with urlopen(request,timeout=25) as response: info=json.load(response)
    url = info.get('picture_url',''); parsed=urlsplit(url)
    if parsed.scheme != 'https' or not (parsed.hostname or '').endswith('.nexusmods.com'): raise ValueError('No supported public thumbnail')
    # The credential is sent only to the API, never to the image host.
    data = subprocess.run(['curl','--fail','--location','--silent','--max-time','25','--max-filesize','8388608','--user-agent','Mozilla/5.0',url],capture_output=True,check=True).stdout
    if len(data)>8*1024*1024: raise ValueError('Thumbnail too large')
    root=Path(os.environ.get('XDG_CACHE_HOME',Path.home()/'.cache'))/'mo2-nexus-frontend/thumbnails'/args.game
    root.mkdir(parents=True,exist_ok=True)
    target=root/f'{args.mod_id}.image'; temporary=target.with_suffix('.tmp'); temporary.write_bytes(data); temporary.replace(target)
    print('Cached public mod thumbnail')
except Exception:
    raise SystemExit('Could not cache the Nexus thumbnail; check the account connection and mod ID.') from None
