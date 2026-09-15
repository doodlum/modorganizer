#!/usr/bin/env bash
# Starts an existing MO2 installation in its own Proton prefix.
set -euo pipefail
launcher_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
command -v python3 >/dev/null || { echo 'Python 3 is required to supervise the MO2 host' >&2; exit 2; }
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
mkdir -p -- "$prefix_root"
# Hold one launcher per prefix, including the interval before MO2 creates its
# window/bridge. Keep this shell alive until the observed native host exits:
# both the container and Proton's Steam stub can outlive MO2.
exec {mo2_launch_lock}>"$prefix_root/mo2-frontend-launch.lock"
flock --nonblock "$mo2_launch_lock" || { echo 'MO2 launcher is already active for this prefix' >&2; exit 0; }
export STEAM_COMPAT_DATA_PATH="$prefix_root"
export STEAM_COMPAT_CLIENT_INSTALL_PATH="$steam_root"
export SteamAppId="$steam_app_id"
export SteamGameId="$steam_app_id"
# Proton's declared container supplies its matching 32/64-bit multimedia and
# system libraries. Running it directly can start MO2 but stall the game menu.
cd -- "$instance_root"
# Set Qt options inside Windows too: an existing wineserver can retain the
# prefix's old environment and otherwise crash in GetPointerFrameTouchInfo.
cp -- "$launcher_directory/tools/mo2-frontend-launch.cmd" "$instance_root/.mo2-frontend-launch.cmd"
cp -- "$launcher_directory/tools/mo2-frontend-launch.vbs" "$instance_root/.mo2-frontend-launch.vbs"
mo2_command=(env QT_QPA_PLATFORM=windows:nowmpointer MO2_FRONTEND_HOST=1 "$proton_command" run wscript.exe //B //Nologo .mo2-frontend-launch.vbs)
completion_directory="$(mktemp -d -- "$prefix_root/.mo2-launch.XXXXXXXX")"
trap 'rm -rf -- "$completion_directory"' EXIT
main_command=(bash "$launcher_directory/tools/run-mo2-command.sh" "$completion_directory/result" "${mo2_command[@]}")
if [[ -n "$runtime_command" ]]; then
    container_command=("$runtime_command" -- "${main_command[@]}")
else
    container_command=("${main_command[@]}")
fi
# Only this supervising shell holds the lock. Child processes may legitimately
# survive MO2 (for example a launched game); never kill them to unlock a prefix.
(
    exec {mo2_launch_lock}>&-
    "${container_command[@]}"
) &
container_pid=$!
if ! python3 "$launcher_directory/tools/wait-mo2-host.py" "$instance_root" "$prefix_root" "$container_pid" "$completion_directory/result"; then
    # Inspection failure is not evidence that the host has stopped.
    wait "$container_pid"
    exit $?
fi
if [[ -f "$completion_directory/result" ]]; then
    read -r command_status < "$completion_directory/result"
    [[ "$command_status" =~ ^[0-9]+$ && "$command_status" -le 255 ]] || exit 1
    exit "$command_status"
fi
if kill -0 "$container_pid" 2>/dev/null; then
    # The native process was observed and has exited; its Steam stub/container
    # may remain with a game or tool. Release the launcher without killing it.
    exit 0
fi
# Startup/container failure before the command could report completion.
wait "$container_pid"
