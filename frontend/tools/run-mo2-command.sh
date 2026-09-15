#!/usr/bin/env bash
# Runs inside the Steam container. Report the main command's actual exit even
# when the container subreaper continues waiting for orphaned helper processes.
completion_path="$1"
shift
"$@"
command_status=$?
if [[ -d "$(dirname -- "$completion_path")" ]]; then
    # The host observer may already have completed and removed this directory.
    { printf '%s\n' "$command_status" > "$completion_path.tmp" &&
        mv -- "$completion_path.tmp" "$completion_path"; } 2>/dev/null || true
fi
exit "$command_status"
