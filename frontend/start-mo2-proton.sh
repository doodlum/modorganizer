#!/usr/bin/env bash
# Starts an existing MO2 installation in its own Proton prefix.
set -euo pipefail
if [[ $# != 5 ]]; then
    echo 'Usage: start-mo2-proton.sh INSTANCE PREFIX PROTON STEAM_ROOT STEAM_APP_ID' >&2
    exit 2
fi
instance_root="$(realpath -- "$1")"
prefix_root="$(realpath -m -- "$2")"
proton_command="$(realpath -- "$3")"
steam_root="$(realpath -- "$4")"
steam_app_id="$5"
runtime_command="${MO2_STEAM_RUNTIME:-}"
tool_manifest="$(dirname -- "$proton_command")/toolmanifest.vdf"
if [[ -z "$runtime_command" && -f "$tool_manifest" ]]; then
    runtime_app_id="$(awk -F '"' '/"require_tool_appid"/ { print $4; exit }' "$tool_manifest")"
    if [[ -n "$runtime_app_id" ]]; then
        [[ "$runtime_app_id" =~ ^[0-9]+$ ]] || { echo 'Invalid Proton runtime app ID' >&2; exit 2; }
        runtime_manifest="$steam_root/steamapps/appmanifest_$runtime_app_id.acf"
        [[ -f "$runtime_manifest" ]] || { echo "Install Steam runtime $runtime_app_id or set MO2_STEAM_RUNTIME to its run script" >&2; exit 2; }
        runtime_directory="$(awk -F '"' '/"installdir"/ { print $4; exit }' "$runtime_manifest")"
        [[ -n "$runtime_directory" && "$runtime_directory" != */* && "$runtime_directory" != '..' ]] || { echo 'Invalid Steam runtime directory' >&2; exit 2; }
        runtime_command="$steam_root/steamapps/common/$runtime_directory/run"
    fi
fi
if [[ -n "$runtime_command" ]]; then
    [[ -x "$runtime_command" ]] || { echo 'Steam runtime run script is unavailable' >&2; exit 2; }
    runtime_command="$(realpath -- "$runtime_command")"
fi
[[ -f "$instance_root/ModOrganizer.exe" && -f "$instance_root/ModOrganizer.ini" ]] || { echo 'Choose an initialized MO2 instance' >&2; exit 2; }
[[ "$steam_app_id" =~ ^[0-9]+$ ]] || { echo 'Steam app ID must be numeric' >&2; exit 2; }
# These characters cannot be represented safely as a Windows batch path.
case "$instance_root$prefix_root" in *'"'*|*$'\n'*|*$'\r'*) echo 'Unsupported Windows path characters' >&2; exit 2;; esac
mkdir -p -- "$prefix_root"
# Hold one launcher per prefix, including the interval before MO2 creates its
# window/bridge. The descriptor survives exec into Proton.
exec {mo2_launch_lock}>"$prefix_root/mo2-frontend-launch.lock"
flock --nonblock "$mo2_launch_lock" || { echo 'MO2 launcher is already active for this prefix' >&2; exit 0; }
command_file="$prefix_root/mo2-frontend-launch.cmd"
windows_instance="Z:${instance_root//\//\\}"
windows_instance="${windows_instance//%/%%}"
printf '@echo off\r\nset QT_QPA_PLATFORM=windows:nowmpointer\r\ncd /d "%s"\r\n"%s\\ModOrganizer.exe"\r\n' "$windows_instance" "$windows_instance" > "$command_file.tmp"
mv -- "$command_file.tmp" "$command_file"
export STEAM_COMPAT_DATA_PATH="$prefix_root"
export STEAM_COMPAT_CLIENT_INSTALL_PATH="$steam_root"
export SteamAppId="$steam_app_id"
export SteamGameId="$steam_app_id"
# Proton's declared container supplies its matching 32/64-bit multimedia and
# system libraries. Running it directly can start MO2 but stall the game menu.
if [[ -n "$runtime_command" ]]; then
    exec "$runtime_command" -- "$proton_command" run cmd /c "Z:$command_file"
fi
exec "$proton_command" run cmd /c "Z:$command_file"
