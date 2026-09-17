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
#
# DEFERRED_PANELS, PAIRED_PANELS and INSTALLED_INTERACTIONS were missing from this
# list, and each says plainly what it wants — "Use an isolated layout for the
# restoration check", "Use isolated FNV profile and layout", two visible page
# headers it cannot have in one panel. All three therefore failed every time they
# were run, and read as three broken pages rather than as one line here being
# short. Nothing noticed because no sweep had ever named them.
PAIRED=" ENTRY_MENUS FILTERED_ROWS LAYOUT_PRESET SEARCH_INPUT SHARED_LISTS SORTED_ROOTS_UI PLUGIN_ROW TAB_RESTORE PROFILE_BUFFER ALERT_LIFECYCLE DEFERRED_PANELS PAIRED_PANELS INSTALLED_INTERACTIONS "
# And the ones that need those two tabs in one panel rather than side by side.
# TAB_RESTORE switches between a panel's tabs and asserts on Panels.Single(); given
# the paired layout it waits for a two-tab panel the fixture cannot contain, times
# out, and says "Restored tab state did not settle" — the layout being the wrong
# shape for the question, reported as the frontend failing to restore a tab.
# DEFERRED_PANELS runs the same check as its second stage and needs the same shape.
TABBED=" TAB_RESTORE DEFERRED_PANELS "
# The checks that do not register a turn, so the screenshot path would shut them
# down mid-run. They are given no screenshot and are ended by the timeout instead.
#
# DEAD_CONTROLS is here for the other reason a screenshot is unwanted: it walks
# every page, and the gate that guards the screenshot waits thirty seconds for both
# live tables to settle on the page it happens to be on. That gate is right to
# report an untrustworthy screenshot, but there is no screenshot worth having from
# a check whose answer is a sentence.
UNREGISTERED=" ENTRY_MENUS FILTERED_ROWS PANEL_CHROME COLUMN_TOGGLE SHARED_LISTS PLUGIN_ROW SORTED_ROOTS_UI DEAD_CONTROLS FOLDER_PAGES EXTRA_BUTTONS "

# The checks that need a person at the keyboard, and so cannot be judged by an
# unattended sweep in either direction. SEARCH_INPUT waits for someone to type a
# string into the mod filter and reports what the window saw of the keystrokes;
# NEXUS_ACCOUNT and OPEN_MOD_DETAILS each open a modal MO2 dialog and wait three
# minutes for it to be closed. Left in a sweep they fail on their timeout and read
# as three broken features. They are skipped with the reason unless the run asks
# for them with MO2_VERIFY_ATTENDED=1, which is a promise that someone is watching.
ATTENDED=" SEARCH_INPUT NEXUS_ACCOUNT OPEN_MOD_DETAILS "

# The checks a real pointer drives. tools/check_plugin_mouse.py opens virtual evdev
# devices, starts the app itself and performs actual kernel-level drags in answer to
# the phase file the check writes. Started from here instead, they write that file
# and wait for a pointer that will never move, then time out — PANEL_DRAG reported
# "Pointer did not resize panels", which reads as a divider that cannot be dragged
# rather than as a driver that was never running. The behaviour is covered; this is
# the wrong runner for it.
DRIVEN=" PLUGIN_MULTI PLUGIN_DRAG PANEL_DRAG "

# The checks whose variable carries a value rather than 1 — a plugin to move, a mod
# to enable, an executable to launch, a game to switch to. This script sets
# MO2_VERIFY_<name>=1 for everything, so each of these was handed "1" and failed on
# it: PLUGIN_DOWN wants a plugin named "MCM Example Menu...", and LAYOUT_OPTIONS
# reported "Sequence contains no matching element" looking for a layout called 1.
# Pass one as NAME=VALUE and it is used; without a value the check is skipped rather
# than run against a meaningless one.
# The checks that run against the scenario fixtures rather than a live MO2. They
# are wired into the fixtures window in Program.cs, not the live one, so a run with
# a bridge directory never reaches them: ORDER printed nothing at all and read as a
# check that had stopped saying anything. Started with run-fixtures.sh instead.
FIXTURES=" CONTEXTS DIALOGS INSTALLED LIBRARY ORDER SCENARIOS SETTINGS WORKSPACE "

PARAMETERISED=" CROSS_GAME DISABLE_MOD DOWNLOADS ENABLE_MOD HEALTH_RESTART INSTALL_ARCHIVE LAUNCH LAYOUT_OPTIONS OPEN_MOD_DETAILS PLUGIN_DOWN "

layout_source="$frontend_root/artifacts/verify-paired-layout.json"
tabbed_source="$frontend_root/artifacts/verify-tabbed-layout.json"
status=0
for entry in "$@"; do
    # NAME or NAME=VALUE.
    name="${entry%%=*}"
    value="1"
    [[ "$entry" == *"="* ]] && value="${entry#*=}"
    if [[ "$FIXTURES" == *" $name "* ]]; then
        echo "=== $name (skipped) ==="
        echo "$name runs against the scenario fixtures, not a live MO2 — it is wired into the fixtures window. Run it with MO2_FIXTURES=1 through run-fixtures.sh; from here it is never reached and prints nothing."
        continue
    fi
    if [[ "$DRIVEN" == *" $name "* ]]; then
        echo "=== $name (skipped) ==="
        echo "$name is driven by a real pointer. Run it with tools/check_plugin_mouse.py, which opens the virtual input devices and starts the app itself; from here it waits for a pointer that never moves."
        continue
    fi
    if [[ "$PARAMETERISED" == *" $name "* && "$entry" != *"="* ]]; then
        echo "=== $name (skipped) ==="
        echo "$name takes a value rather than 1 — pass it as $name=VALUE. Run against 1 it fails on the value, not on the behaviour."
        continue
    fi
    if [[ "$ATTENDED" == *" $name "* && "${MO2_VERIFY_ATTENDED:-}" != 1 ]]; then
        echo "=== $name (skipped) ==="
        echo "$name needs a person at the keyboard. Re-run with MO2_VERIFY_ATTENDED=1 and stay at the machine."
        continue
    fi
    log="$out/$name.log"
    environment=("MO2_VERIFY_$name=$value")
    if [[ "$PAIRED" == *" $name "* ]]; then
        source_layout="$layout_source"
        build_with="tools/paired_layout.py"
        if [[ "$TABBED" == *" $name "* ]]; then
            source_layout="$tabbed_source"
            build_with="tools/paired_layout.py --tabbed"
        fi
        [[ -f "$source_layout" ]] || { echo "$name needs $source_layout; build it with $build_with" >&2; status=1; continue; }
        layout="$(mktemp /tmp/mo2-frontend-layout-XXXXXX.json)"
        cp "$source_layout" "$layout"
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
