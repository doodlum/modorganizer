# Tools acceptance

`MO2_VERIFY_TOOLS_LIFECYCLE=1` checks stale reads after detach/reopen, executable
pin/icon updates, busy-state recovery and pinned extension metadata. Adding
`MO2_VERIFY_PINNED_TOOLS_NATIVE=1` checks startup availability against the connected
host with an empty frontend tool cache. Pins for this check are held in memory;
the user's pin file and native executable settings are not written.

## Real extension dialog

`MO2_VERIFY_NATIVE_TOOL_DIALOG=/absolute/spec.json` invokes the actual Tools entry
for INI Editor. The specification contains fresh absolute paths:

```json
{
  "request": "/absolute/new-tool.request",
  "report": "/absolute/new-tool.json"
}
```

Before starting the frontend, run `tools/fixtures/observe_data_preview.py` with
Windows Python in the target host's Wine prefix. Supply the exact host executable,
the marker and report paths as Windows paths, followed by the exact dialog title
`INI Files`. This is the dialog title used by the installed FNV extension; its
Tools action is named `INI Editor`. The observer writes a report-stem `.ready`
file after rejecting a pre-existing matching dialog. Wait for readiness before
starting the frontend check.

When deriving a launch environment from the running host, remove its process-local
`WINESERVERSOCKET`, `WINELOADERNOEXEC` and `WINEPRELOADRESERVE`; inherited descriptor
values cannot be used by a new observer process. Use the same Wine loader/prefix
as that host. The observer only closes the matching title owned by the exact
executable. It does not read editor contents or send editing/save commands.

The frontend requires visible-dialog evidence, disabled actions while busy,
recovery after closure, the original connected profile and a hidden native main
window. The observer additionally checks that only the requested native dialog
was visible and no native top-level window remains visible afterward. If a run
fails, inspect the owned dialog/process before retrying; do not start a second
dialog over an unresolved one.

## Current evidence

- `artifacts/pinned-tools-native.log`: all four FNV extension shortcuts load from
  an empty tool cache through the live bridge; unchanged rereads retain buttons.
- `artifacts/pinned-tools-native-state-check.json`: profile, mod/plugin state,
  executable list, executable pins and frontend pin-file hash unchanged.
- `artifacts/native-tool-dialog-verified/frontend.log` and `observer.json`: the
  actual Tools entry opens INI Editor, the observer closes it, actions recover,
  and the native main window stays hidden.
- `artifacts/native-tool-dialog-verified/state-check.json`: profile, mod/plugin
  states/priorities, pins, executable list and checked INI hashes unchanged.
- `artifacts/skyrim-native-tool-dialog-verified/`: the same real INI Editor
  workflow passes on the current Skyrim instance, with independent visibility/
  closure evidence and unchanged checked state and INI hashes.
- `artifacts/game-tools-roundtrip-verified/frontend.log`: FNV → current Skyrim →
  FNV compares rendered Tools names and enabled states with each native catalog,
  alongside Mods/native filters, Plugins and Data. FNV has 3 programs and 4
  extension tools; Skyrim has 4 programs and 7 extension tools. Both hosts retain
  their original checked state and profile INI hashes (`state-check.json`).
- `artifacts/tools-recovery-before.log` reproduces two failed-read recovery bugs:
  manual refresh leaves pins disabled during backoff, and that backoff delays a
  different profile. `tools-recovery-after.log` passes both corrected cases and
  the existing lifecycle checks using isolated state and injected failures.

The first observer attempt failed before frontend launch because it inherited a
Wine server descriptor. A later attempt looked for the action name rather than
the dialog title; its owned dialog was closed and its baseline verified unchanged
before the corrected run. These failures are retained in the preceding artifact
directories rather than counted as passes.

This proves the FNV and Skyrim INI Editor workflows, not arbitrary third-party
tools, physical mouse input or executable/VFS launches. The separate native
ProcessRunner build and runtime gates remain in `NATIVE_LAUNCH_VALIDATION.md`.

## Launcher attachment lifetime — 2026-09-20

The launch button model and panel previously subscribed to profile changes in
their constructors and never unsubscribed. Both now subscribe when the panel
attaches, refresh current state on reattachment, and unsubscribe on detachment.
The model disables its launch command while detached. Pinned-tool read completion
cannot rebuild or initiate another read through a detached panel.

The Tools lifecycle check now removes a launcher, changes executables and busy
state, and confirms no new tool reads or executable-list updates while detached.
Reattachment picks up the new executable and performs exactly one pinned-tool
refresh. The existing delayed reads, pins/icons, busy state, unavailable tools,
unchanged notification and failure-recovery cases also pass. Evidence:
`artifacts/launcher-lifetime.log`; Release build had eight existing warnings and
zero errors. Injected failure cases account for its two refresh-error messages.
No native executable was launched or pin changed by this isolated-profile check.

## Shortcut menu request ownership — 2026-09-20

Each menu opening now owns a request version. Closing, detaching or changing
executable invalidates the pending response. Profile/availability changes close
a menu for an obsolete target, and action callbacks check the current executable
and target again before invoking MO2. A response from an older opening cannot
replace the current menu just because its profile target is still the same.

An isolated launcher check opens twice, completes the second read first, then
completes the first read and verifies that the second response remains. A third
pending read completes after changing executable: the menu stays closed and the
old program's actions are not inserted. All existing Tools lifecycle cases pass.
Evidence: `artifacts/shortcut-menu-lifetime.log`. Release build: eight existing
warnings, zero errors. No native shortcut was created, removed or toggled.

## Observe native executable selection changes — 2026-09-20

The launcher tracks the last selection reported by MO2. A changed native
selection updates the launch model and combo even when the previously selected
program still exists. Unchanged snapshots retain a frontend-local choice;
endpoint changes and removal of the selected program retain the existing
fallback behavior. The bridge already reports its original Qt selector's
currentText in selectedExecutable; no bridge change or launch was required.

The isolated-profile check chooses Secondary locally, sends an unchanged
notification and verifies it remains selected. It then reports native Secondary
followed by Primary and checks that both model and combo return to Primary.
The full Tools/menu lifecycle suite passes in
`artifacts/launcher-native-selection.log`. This exercises snapshot-driven UI
behavior with an injected profile, not a new native Qt selection integration
probe. Release build: eight existing warnings, zero errors.

## Live two-game launcher verification — 2026-09-20

Mo2GameSwitchCheck now verifies the launcher alongside Tools for each step of
FNV → current Skyrim → FNV. It compares the executable combo and current choice
with the connected host, verifies model/PLAY availability, selects another valid
program locally, requests a real snapshot and verifies the local choice survives.
The original frontend choice is restored after each check; PLAY is not clicked.

The current build passed all three launcher checks, native Tools, Mods/filter,
Plugins and Data checks. Both hosts retained native selectedExecutable, profile,
mod/plugin states and priorities, pins, executable lists and profile INI hashes.
Evidence: `artifacts/game-launcher-roundtrip/frontend.log` and `state-check.json`.
This proves cross-game launcher state and polling behavior; it does not establish
new gameplay or Windows process-runner argument handling evidence.
