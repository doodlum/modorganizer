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

# One sweep at a time, and the reason is this script's own cleanup: it ends every
# run with `pkill -KILL -f bin/Release/net9.0/MockHost`, which matches any host,
# not only the one it started. Two sweeps therefore kill each other's checks
# part-way. That happened — a second sweep started while one was still going, and
# WIDGET_BEHAVIOUR was killed mid-run and left a log holding build output and no
# verdict. It is reported as NO VERDICT, which is right, but "another sweep shot
# it" and "this check has stopped saying anything" read identically afterwards,
# and the checks that ran alongside it cannot be told apart from clean runs either
# — two frontends were on the same live host at once.
#
# So a second sweep does not start. It is refused rather than queued: a wait that
# takes half an hour to begin looks exactly like a hang.
#
# The holder is written down rather than held on a descriptor. `flock` on an fd was
# the first attempt and it does hold the lock — it holds it far too well: the fd is
# inherited by everything the sweep starts, `dotnet run` leaves MSBuild node-reuse
# daemons behind on purpose, and those kept the lock after the sweep had finished.
# Every later sweep was then refused by a build server. A pid that is checked for
# life cannot outlive the thing it stands for, and a sweep killed part-way leaves
# nothing behind that has to be cleaned up by hand.
lockfile="${TMPDIR:-/tmp}/mo2-verify.lock"
if [[ -s "$lockfile" ]]; then
    other="$(<"$lockfile")"
    if [[ "$other" =~ ^[0-9]+$ ]] && kill -0 "$other" 2>/dev/null &&
       tr '\0' ' ' <"/proc/$other/cmdline" 2>/dev/null | grep -q verify.sh; then
        echo "verify.sh is already running as pid $other. Two sweeps at once cannot be trusted: this script kills any MockHost after each run, including the other sweep's, which leaves a check truncated into a log that reads as NO VERDICT." >&2
        exit 3
    fi
fi
echo $$ >"$lockfile"
trap 'rm -f "$lockfile"' EXIT

# The checks that require an isolated paired layout, and refuse to run without one.
PAIRED=" ENTRY_MENUS FILTERED_ROWS LAYOUT_PRESET SEARCH_INPUT SHARED_LISTS SORTED_ROOTS_UI PLUGIN_ROW TAB_RESTORE PROFILE_BUFFER ALERT_LIFECYCLE "
# The checks that do not register a turn, so the screenshot path would shut them
# down mid-run. They are given no screenshot and are ended by the timeout instead.
#
# DEAD_CONTROLS is here for the other reason a screenshot is unwanted: it walks
# every page, and the gate that guards the screenshot waits thirty seconds for both
# live tables to settle on the page it happens to be on. That gate is right to
# report an untrustworthy screenshot, but there is no screenshot worth having from
# a check whose answer is a sentence.
UNREGISTERED=" ENTRY_MENUS FILTERED_ROWS PANEL_CHROME COLUMN_TOGGLE SHARED_LISTS PLUGIN_ROW SORTED_ROOTS_UI DEAD_CONTROLS FOLDER_PAGES EXTRA_BUTTONS "

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
                # The app is a grandchild — timeout, then run-live.sh, then dotnet
                # run, then the host itself — so killing the job's own children left
                # it running. Orphaned hosts piled up at ~120MB each until the
                # machine had no memory left, which is when MO2 started wedging and
                # the compiler was killed mid-build.
                [[ "$quiet" -ge 8 ]] && { pkill -KILL -P "$runner" 2>/dev/null; kill -KILL "$runner" 2>/dev/null
                                          sleep 1; pkill -KILL -f 'bin/Release/net9.0/MockHost' 2>/dev/null; break; }
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
    status_of=$?
    # And after every run, however it ended: a run that is killed by its timeout
    # leaves the same orphan.
    pkill -KILL -f 'bin/Release/net9.0/MockHost' 2>/dev/null
    echo "=== $name (exit $status_of) ==="
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
