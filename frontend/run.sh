#!/usr/bin/env bash
set -euo pipefail
frontend_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
if [[ -x "$frontend_root/../.tools/dotnet/dotnet" ]]; then
    export DOTNET_ROOT="$frontend_root/../.tools/dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
    dotnet_cmd="$DOTNET_ROOT/dotnet"
else
    dotnet_cmd=dotnet
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
exec "$dotnet_cmd" run --configuration "${MO2_BUILD_CONFIGURATION:-Release}" --project "$frontend_root/MockHost/MockHost.csproj" -- "$@"
