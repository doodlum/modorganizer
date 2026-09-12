#!/usr/bin/env bash
set -euo pipefail
frontend_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
reference_root="$frontend_root/../reference-fnv"
profile_root="$frontend_root/artifacts/fnv-profile"
if [[ ! -f "$reference_root/src/NexusMods.App/NexusMods.App.csproj" ]]; then
    echo "Create reference-fnv using frontend/reference/README.md first." >&2
    exit 1
fi
if [[ -x "$frontend_root/../.tools/dotnet/dotnet" ]]; then
    export DOTNET_ROOT="$frontend_root/../.tools/dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
fi
export XDG_DATA_HOME="$profile_root/data" XDG_CONFIG_HOME="$profile_root/config"
export XDG_CACHE_HOME="$profile_root/cache" XDG_STATE_HOME="$profile_root/state"
export XDG_RUNTIME_DIR="$profile_root/runtime" DOTNET_CLI_TELEMETRY_OPTOUT=1
mkdir -p "$XDG_DATA_HOME" "$XDG_CONFIG_HOME" "$XDG_CACHE_HOME" "$XDG_STATE_HOME" "$XDG_RUNTIME_DIR"
chmod 700 "$XDG_RUNTIME_DIR"
exec dotnet run --project "$reference_root/src/NexusMods.App/NexusMods.App.csproj" -- "$@"
