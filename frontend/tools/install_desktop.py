#!/usr/bin/env python3
"""Install this checkout's launcher and taskbar identity for the current user."""
from pathlib import Path
import argparse
import os
import shutil
import subprocess

frontend = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--configuration', choices=['Release', 'Debug'], default='Release')
parser.add_argument('--app', type=Path, help='Published MockHost.dll to launch instead of the development build')
args = parser.parse_args()
app = (args.app.expanduser().resolve() if args.app else
       frontend / f'MockHost/bin/{args.configuration}/net9.0/MockHost.dll')
dotnet = frontend.parent / '.tools/dotnet/dotnet'
if not app.is_file() or not dotnet.is_file():
    parser.error(f'App DLL and repository-local SDK must exist: {app}, {dotnet}')
if not app.with_suffix('.runtimeconfig.json').is_file() or not app.with_suffix('.deps.json').is_file():
    parser.error('The app must have its runtimeconfig.json and deps.json alongside the DLL')
root = Path(os.environ.get('XDG_DATA_HOME', Path.home() / '.local/share'))
apps = root / 'applications'; apps.mkdir(parents=True, exist_ok=True)
icons = root / 'icons/hicolor/256x256/apps'; icons.mkdir(parents=True, exist_ok=True)
shutil.copy2(frontend / 'MockHost/Assets/AppIcon.png', icons / 'mo2-nexus-frontend.png')
launch = root / 'mo2-nexus-frontend/launch.sh'; launch.parent.mkdir(parents=True, exist_ok=True)
import shlex
launch.write_text('#!/bin/sh\nexec ' + shlex.quote(str(dotnet)) + ' ' + shlex.quote(str(app)) + ' "$@"\n')
launch.chmod(0o700)
# Desktop Exec quoting follows the desktop-entry specification.
quoted = '"' + str(launch).replace('\\', '\\\\').replace('"', '\\"').replace('`', '\\`').replace('$', '\\$') + '"'
(apps / 'mo2-nexus-frontend.desktop').write_text('[Desktop Entry]\nType=Application\nName=Nexus Mods — MO2\nExec=/bin/sh ' + quoted + '\nIcon=mo2-nexus-frontend\nStartupWMClass=mo2-nexus-frontend\nTerminal=false\nCategories=Game;Utility;\nStartupNotify=true\n')
if shutil.which('update-desktop-database'): subprocess.run(['update-desktop-database', str(apps)], check=True)
print('Installed frontend launcher and taskbar icon')
