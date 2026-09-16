#!/usr/bin/env bash
# Run MO2_VERIFY_* checks against a live MO2 host, one at a time, and record what
# each of them said.
#
#   frontend/tools/verify.sh BRIDGE_DIRECTORY NAME [NAME...]
#
# Two things this exists to get right, both of which produced wrong answers when
# the checks were driven by hand:
#
#   * A check that says nothing is not a check that passed. MO2_SCREENSHOT is what
#     shuts the app down after a run, and it waits only for the checks that
#     registered a turn with Mo2CheckTurn — so a check that does not register is
#     cut off part-way and prints nothing at all. MO2_VERIFY_PANEL_CHROME and
#     MO2_VERIFY_COLUMN_TOGGLE both did that, and both read as clean runs in a
#     summary that only looked for the word FAIL. Anything with no verdict is
#     reported here as NO VERDICT, not skipped over.
#
#   * Some checks need the two lists side by side and an isolated layout file
#     under /tmp, and refuse to run without one. Those are named in PAIRED below
#     and are given a freshly built layout for every run, so one check's
#     navigation cannot decide what the next one sees.
#
# One check per run. Most of them navigate the selected panel, which tears down the
# view that was in it, so checks that read a list cannot share a process with
# checks that move between pages.
set -uo pipefail
[[ $# -ge 2 ]] || { echo 'Usage: verify.sh BRIDGE_DIRECTORY NAME [NAME...]' >&2; exit 2; }
frontend_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
bridge="$(realpath -- "$1")"; shift
out="${MO2_VERIFY_OUTPUT:-$frontend_root/artifacts/verify}"
mkdir -p "$out"

# The checks that require an isolated paired layout, and refuse to run without one.
PAIRED=" ENTRY_MENUS FILTERED_ROWS LAYOUT_PRESET SEARCH_INPUT SHARED_LISTS SORTED_ROOTS_UI PLUGIN_ROW TAB_RESTORE PROFILE_BUFFER "
# The checks that do not register a turn, so the screenshot path would shut them
# down mid-run. They are given no screenshot and are ended by the timeout instead.
UNREGISTERED=" ENTRY_MENUS FILTERED_ROWS PANEL_CHROME COLUMN_TOGGLE SHARED_LISTS PLUGIN_ROW SORTED_ROOTS_UI "

layout_source="$frontend_root/artifacts/verify-paired-layout.json"
status=0
for name in "$@"; do
    log="$out/$name.log"
    environment=("MO2_VERIFY_$name=1")
    if [[ "$PAIRED" == *" $name "* ]]; then
        [[ -f "$layout_source" ]] || { echo "$name needs $layout_source; build it with tools/paired_layout.py" >&2; status=1; continue; }
        layout="$(mktemp /tmp/mo2-frontend-layout-XXXXXX.json)"
        cp "$layout_source" "$layout"
        environment+=("MO2_FRONTEND_LAYOUT=$layout")
    fi
    # The alias check compares against the icon styles as upstream shipped them,
    # which the alias work replaced in the working tree. Recovered from upstream's
    # own history rather than kept as a copy that could drift from it.
    if [[ "$name" == ICON_ALIASES && -z "${MO2_ICON_ALIAS_REFERENCE:-}" ]]; then
        reference="$out/IconsStyles.original.axaml"
        icons=src/NexusMods.Themes.NexusFluentDark/Styles/Controls/Icons/IconsStyles.axaml
        pinned="$(git -C "$frontend_root/upstream" rev-parse HEAD~1 2>/dev/null)"
        git -C "$frontend_root/upstream" show "$pinned:$icons" >"$reference" 2>/dev/null ||
            { echo "Could not recover $icons from upstream history" >&2; status=1; continue; }
        environment+=("MO2_ICON_ALIAS_REFERENCE=$reference")
    fi
    deadline="${MO2_VERIFY_TIMEOUT:-420}"
    if [[ "$UNREGISTERED" == *" $name "* ]]; then
        # Nothing ends these runs from the inside, so they are ended from here:
        # once a verdict has been printed and the log has been quiet for a few
        # seconds, the check has said everything it is going to. Waiting out the
        # whole timeout instead cost seven minutes a check and told us nothing
        # more. Several of them print more than one line, which is why this waits
        # for quiet rather than stopping at the first PASS.
        env "${environment[@]}" timeout --signal=KILL "$deadline" \
            "$frontend_root/run-live.sh" "$bridge" >"$log" 2>&1 &
        runner=$!
        quiet=0; size=0
        while kill -0 "$runner" 2>/dev/null; do
            sleep 2
            now=$(stat -c %s "$log" 2>/dev/null || echo 0)
            if [[ "$now" == "$size" ]] && grep -qE '^(PASS|FAIL)' "$log"; then
                quiet=$((quiet + 2))
                [[ "$quiet" -ge 8 ]] && { pkill -KILL -P "$runner" 2>/dev/null; kill -KILL "$runner" 2>/dev/null; break; }
            else
                quiet=0; size="$now"
            fi
        done
        wait "$runner" 2>/dev/null
    else
        environment+=("MO2_SCREENSHOT=$out/$name.png")
        env "${environment[@]}" timeout --signal=KILL "$deadline" \
            "$frontend_root/run-live.sh" "$bridge" >"$log" 2>&1
    fi
    echo "=== $name (exit $?) ==="
    if grep -qE '^(PASS|FAIL)' "$log"; then
        grep -E '^(PASS|FAIL)' "$log"
        grep -qE '^FAIL' "$log" && status=1
    else
        echo "NO VERDICT from $name — it said neither PASS nor FAIL. Last lines:"
        tail -12 "$log"
        status=1
    fi
done
exit "$status"
