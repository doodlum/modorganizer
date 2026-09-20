# Saves selection and native confirmation

## Automatic refresh — 2026-09-20

Visible Saves panels poll MO2 every three seconds, pausing while hidden, detached
or while the frontend reports a foreground native action. Unchanged results retain
the table source, scroll position and selection. Reads coalesce; an explicit refresh
during a read is queued. Existing save actions remain available during background
reads, with their normal target and command guards.

`artifacts/saves-polling-native-accepted.log` records passing controlled checks for
unchanged reads, additions/removals, empty lists, selection, search during reads,
queued refresh, error recovery, hidden panels and detach/reopen. Its native FNV
phase automatically observed two disposable save copies appearing and disappearing,
preserving an original selection. Five native reads took at most 176 ms; this small
save set is not evidence of large-library or frame/input latency.

Repeat after building Release:

```sh
python3 frontend/tools/check_saves_polling.py \
  --instance frontend/artifacts/mo2-fnv-host --tag new-unique-tag
```

The final probe check confirmed original save bytes/list and mod/plugin order and
states unchanged, with all disposable copies absent. The existing file-page
lifecycle and native readback suite also passed in
`artifacts/saves-multiple-polling-final.log`. Release build: eight warnings, zero
errors. Physical keyboard acceptance and populated Skyrim Saves remain open.

The frontend uses TreeDataGrid multiple selection. Filename identities survive
filtering and reordered refreshes. Missing saves are removed from retained
selection so a subsequently recreated filename is not automatically selected.
Only selected rows currently visible in the table are sent to an action. Details
and repair require exactly one save; the delete menu displays the selected count.

Bulk deletion uses the existing bridge `fileMenuAction` with view `saves`, selected
filenames and MO2's own count-specific Delete entry. MO2 owns the confirmation,
companion-file handling and recycle-bin operation. Single-save actions retain
their existing bridge route. No bridge package update is needed for this change.

The shared row-menu helper now clears selection before selecting an unselected
right-click target. Its existing pointer handler leaves an already selected group
intact. Keyboard opening keeps the current selection.

Linux does not interpret a Wine `C:` path as a relative local path. For save
directories without a directly usable Linux/Z: path, Open in Explorer delegates
to the owning native host. This fallback is implemented; opening the resulting
file-manager window has not been freshly exercised in this check.

## Verification

Release build: nine warnings, zero errors.

`MO2_VERIFY_FILE_PAGE_LIFECYCLE=1` now covers multiple save selection, menu count
and single-save repair gating, duplicate titles, filter/refresh restoration,
removal/recreation, and exclusive selection of an unselected right-click target.
The existing Archives selection, stale completion/reopening cases and independent
native Archives/Overwrite/Saves/Data comparisons also pass. Evidence:
`artifacts/saves-multiple-final.log`.

The native cancellation check requires Linux/X11, xdotool, a running Wine host,
and at least two existing saves. After building Release, run:

```sh
python3 frontend/tools/check_save_delete_cancel.py \
  --instance /path/to/modorganizer2
```

The observer identifies the exact host executable and uses its Wine prefix to
resolve the directory for hashing. It refuses an existing confirmation, then
observes the new host-owned Confirm window and sends only Escape. The frontend
check calls the same action as its menu, verifies the native two-save menu entry,
and requires the observer report plus unchanged save/companion hashes and save
list after the action returns. It stops only its own test frontend process.

FNV cancellation passed: `artifacts/save-delete-cancel-ready.log` and
`artifacts/save-cancel-observer-ready.txt`; all four files were unchanged. This is
native opening/cancellation evidence. Accepted deletion is covered below; Skyrim
save workflows and broader production acceptance remain unverified.

An initial check exposed the invalid Linux interpretation of `C:/...` before
opening a dialog; that prompted the native Explorer fallback. Another attempt
stopped before attachment of the fixture view. The final check waits for it to
attach and finish reading before selecting saves. The selection fixture also
waits for the search change to render before inspecting the filtered selection.

## Completed bulk deletion and Delete event — 2026-09-20

`tools/save_delete_probe.py` creates two uniquely named copies of a native save
and its companion files. It records original file hashes, native save filenames
and mod/plugin names, order, priority and states before making copies. The
frontend verifier accepts only the specification's exact, unchanged disposable
files as targets. After inspecting MO2's confirmation against those filenames,
accepting it removed both saves and both companions. The frontend refreshed its
rows and cleared its selection. The independent probe check verified that the
complete original save directory and native list/state/order matched baseline.

Evidence: `artifacts/save-delete-outcome-first.log` and
`artifacts/save-delete-disposable-confirm.png`.

MO2's original `SavesTab::eventFilter` also handles Delete. The frontend now binds
that key to the save table, retaining the existing readiness/selection guards and
native confirmation. Search editing remains outside that table's event route.
`MO2_VERIFY_SAVE_DELETE_ROUTED=1` focuses an actual save cell and raises Delete
through Avalonia's normal key-event route. It requires the event to be handled,
then verifies the same native deletion outcome. This passed on the final build:
`artifacts/save-delete-outcome-cell-key.log` and
`artifacts/save-delete-cell-key-confirm.png`.

Both successful runs also placed all four disposable files in desktop Trash with
matching bytes and original restore destinations. Those eight owned Trash files
and their metadata were subsequently removed after verification. The report is
`artifacts/save-delete-trash-verification.json`. This verifies recycle-bin output
and metadata, not a file-manager Restore operation.

### Repeatable disposable-file check

Use a running Linux/Wine host with a real save and companion, the current bridge,
and a freshly built Release frontend. Choose new specification and layout paths:

```sh
instance=/path/to/modorganizer2
spec=/tmp/new-save-delete-probe.json
python3 frontend/tools/save_delete_probe.py prepare --instance "$instance" --spec "$spec"
MO2_BRIDGE_DIRECTORY="$instance/plugins/data/frontend-bridge" \
MO2_FRONTEND_LAYOUT=/tmp/new-save-delete-layout.json \
MO2_VERIFY_SAVE_DELETE_OUTCOME="$spec" \
MO2_VERIFY_SAVE_DELETE_ROUTED=1 \
.tools/dotnet/dotnet frontend/MockHost/bin/Release/net9.0/MockHost.dll
```

Inspect the native confirmation and accept only the two disposable names recorded
in the specification. Close the test frontend after its verdict, then run:

```sh
python3 frontend/tools/save_delete_probe.py check --instance "$instance" --spec "$spec"
```

Omit `MO2_VERIFY_SAVE_DELETE_ROUTED` to exercise the actual menu entry instead.
The optional `cleanup` operation removes only unchanged, specification-owned
copies left after a cancelled/failed check. Inspect any still-open native dialog
before cleanup or another run; a timeout is not permission to replay the action.
Successful deletion can leave the owned copies in Trash; the helper does not
empty Trash or remove unrelated files.

### Input and lifecycle limits

Pointer attempts used an isolated view over the running frontend. X11 simulation
on this Wayland desktop delivered incorrect initial positions and unexpected
Alt/Control modifiers. A temporary Linux input device delivered clicks to the
correct cells and selected exactly the two disposable copies. However, desktop
Delete was intercepted as Ctrl+Alt+Delete and opened the session menu. It was
cancelled; no shutdown/logout occurred. Window-targeted X11 Delete also did not
complete. The final passing key check therefore uses a routed Avalonia event,
and ordinary desktop-keyboard acceptance remains open. No complete physical
Ctrl/Shift gesture or unmodified-key acceptance is claimed.

The pointer verifier now waits for settled row coordinates, and checks focus in
the selected cell rather than requiring the non-focusable table to accept it.
Failed checks were stopped before deletion and their unchanged copies cleaned
up. All original save hashes and native state/order still match the first run's
baseline. Temporary input devices were destroyed.

The failed pointer runs also exposed an actual detach error: a recycled save
template built a new empty TextBlock when its row became null during teardown.
It now returns no control for a null row. Subsequent teardown preserved the
original test failures without that Avalonia exception. The final full file-page
lifecycle/selection/native-readback checks pass in
`artifacts/saves-multiple-deletion-final.log`. Final Release build has nine
warnings and zero errors. Full production readiness remains unproven.


## Populated FNV panel and native menu availability — 2026-09-20

The live preset check now compares Saves' full rows to a fresh native ReadSaves,
requires a rendered first name, selects one and two existing saves, and compares
all displayed menu enabled states with independent native readFileMenu responses.
No save action is invoked. skyrim-preset-fnv-save-menu.log passes for two FNV saves
(the runner's historical log prefix does not identify its game). The compositor
capture desktop-pages-fnv-save-menu/preset-saves.png shows both readable name/file
rows in the right panel at actual 1280x750 client size. The same run passes all
12 page bounds and selected-tab resizing/navigation checks.

Saves now uses the shared asynchronous row-menu availability path, so single
selection alone no longer enables repair: the owning game extension decides via
MO2's menu. Controls disable during lookup. Target, activation and complete
selected filename checks reject stale results. Injected fixture save readers
retain local-only menus because their filenames have no native counterparts.

fnv-save-menu-native-validation.json confirms the native profile/mod/plugin
states remain at baseline and every save/companion byte matches the earlier
original hashes. Native repair was enabled for the tested single save and disabled
for two selected saves. This live sample therefore does not exercise a single
save with no missing assets; shared availability coverage tests disabled native
flags independently. Final Release build: eight existing warnings, zero errors.

Source review found an outstanding fidelity gap: src/savestab.cpp uses itemEntered
to show the game extension's SaveGameInfo widget, hiding it on leave/deactivation.
The frontend currently offers a modal extension widget on double-click. Populated
row/menu acceptance does not establish hover-preview parity. Populated Skyrim
Saves and physical keyboard acceptance also remain open.
