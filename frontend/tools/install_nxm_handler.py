#!/usr/bin/env python3
"""Install the Linux NXM handoff; retain the prior handler for restoration."""
import argparse
import os
from pathlib import Path
import shlex
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet', type=Path)
parser.add_argument('--app', type=Path)
parser.add_argument('--restore', action='store_true')
args = parser.parse_args()
data = Path(os.environ.get('XDG_DATA_HOME', Path.home() / '.local/share'))
config = Path(os.environ.get('XDG_CONFIG_HOME', Path.home() / '.config')) / 'mo2-nexus-frontend'
desktop_id = 'mo2-nexus-frontend-nxm.desktop'
previous = config / 'previous-nxm-handler.txt'
current = subprocess.check_output(['xdg-mime', 'query', 'default', 'x-scheme-handler/nxm'], text=True).strip()
if args.restore:
    if current != desktop_id:
        raise SystemExit('Another handler is already selected; left unchanged.')
    if not previous.is_file() or not previous.read_text().strip():
        raise SystemExit('No previous handler was recorded.')
    subprocess.run(['xdg-mime', 'default', previous.read_text().strip(), 'x-scheme-handler/nxm'], check=True)
    print('Restored previous NXM handler')
    raise SystemExit()
if not args.dotnet or not args.app or not args.dotnet.is_file() or not args.app.is_file():
    parser.error('Provide existing --dotnet and --app files')
config.mkdir(parents=True, exist_ok=True)
if current != desktop_id and not previous.exists():
    previous.write_text(current + '\n')
directory = data / 'mo2-nexus-frontend'
directory.mkdir(parents=True, exist_ok=True)
wrapper = directory / 'nxm-handler.sh'
command = shlex.join([str(args.dotnet.resolve()), str(args.app.resolve()), '--nxm'])
wrapper.write_text('#!/bin/sh\n'
                  '[ "$#" = 1 ] || exit 2\n'
                  f'if {command} "$1" >/dev/null 2>&1; then exit 0; fi\n'
                  'if command -v kdialog >/dev/null 2>&1; then\n'
                  '  kdialog --error "MO2 could not complete the NXM handoff. Check Downloads before retrying. Select the intended game instance in the frontend if its route is unavailable."\n'
                  'fi\nexit 1\n')
wrapper.chmod(0o700)
applications = data / 'applications'; applications.mkdir(parents=True, exist_ok=True)
# Encode the quoted argument first, then the desktop entry's string value.
# The desktop loader unescapes the string before parsing Exec arguments.
# URL stays a single %u; literal percent characters in the path are doubled.
quoted = str(wrapper).replace('\\', '\\\\').replace('"', '\\"').replace('`', '\\`').replace('$', '\\$').replace('%', '%%')
quoted = quoted.replace('\\', '\\\\').replace('\n', '\\n').replace('\r', '\\r').replace('\t', '\\t')
(applications / desktop_id).write_text('[Desktop Entry]\nType=Application\nName=Nexus Mods — MO2 downloads\n'
    f'Exec=/bin/sh "{quoted}" %u\nTerminal=false\nNoDisplay=true\nStartupNotify=false\nMimeType=x-scheme-handler/nxm;\n')
subprocess.run(['xdg-mime', 'default', desktop_id, 'x-scheme-handler/nxm'], check=True)
actual = subprocess.check_output(['xdg-mime', 'query', 'default', 'x-scheme-handler/nxm'], text=True).strip()
if actual != desktop_id:
    raise SystemExit('The desktop did not select the new NXM handler')
print('Installed MO2 frontend NXM handler; previous handler retained for --restore')
