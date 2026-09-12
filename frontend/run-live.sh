#!/usr/bin/env bash
set -euo pipefail
if [[ $# != 1 ]]; then
    echo 'Usage: frontend/run-live.sh /path/to/mo2/plugins/data/frontend-bridge' >&2
    exit 2
fi
export MO2_BRIDGE_DIRECTORY="$(realpath -- "$1")"
frontend_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec "$frontend_root/run.sh"
