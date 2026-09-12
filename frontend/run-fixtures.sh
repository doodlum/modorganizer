#!/usr/bin/env bash
set -euo pipefail
export MO2_FIXTURES=1
unset MO2_BRIDGE_DIRECTORY
frontend_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec "$frontend_root/run.sh" "$@"
