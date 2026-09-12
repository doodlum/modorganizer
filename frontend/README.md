> **Current target:** integrate the frontend with MO2-owned profiles, mods,
> plugins and downloads. Earlier fixture sections below describe scaffolding,
> not completed integration. See [MO2 integration and FNV acceptance](MO2_INTEGRATION.md).
> Proton is acceptable; existing MO2 extensions must retain their APIs.

# Alternate Nexus frontend (in progress)

Linux Avalonia frontend for Mod Organizer. `run.sh` opens NMA’s My Games with your detected MO2 games; it starts disconnected until you select a profile. `run-live.sh /path/to/mo2/plugins/data/frontend-bridge` connects to a
running MO2 host with the [bridge extension](mo2-plugin/README.md) installed.
Live mode shows the active MO2 profile's mods on the left and plugins on the
right, and sends mod activation, mod priority, plugin activation and plugin ordering changes through MO2.
The Downloads tab reads MO2's downloads folder, requests Nexus files through
MO2's logged-in downloader, and installs archives through its existing installers.
The sidebar, game widgets, loadout cards, top bar and panel system use the original
NMA views. My Games → View → profile switches the connected game. My Loadouts
shows real profiles grouped by game across registered MO2 instances. Selection
uses each running host. **Manage MO2 profiles…** opens MO2’s original dialog;
**Create Copy**, **Rename** and **Delete** on each card open MO2’s original
prompts and confirmation. The active profile cannot be renamed or deleted. Cards refresh when MO2
profile files change, including changes made in the original interface.
Configured executables launch through MO2. Profile selection starts a configured instance launcher when MO2 is stopped.
Pause, resume and cancel route through MO2’s existing download controls.
FNV gameplay, the MCM menu, enabled/disabled runs and all 14 plugin indices are
verified; see [in-game evidence](FNV_ACCEPTANCE.md). The mod panel includes MO2’s Overwrite and conflict/status messages.
**Details in MO2** opens its original conflict/file dialog. Selecting plugins
shows MO2’s diagnostics and hexadecimal mod indices. Activation and row movement
controls reflect MO2’s restrictions.
Fixture scenarios are available only with `run-fixtures.sh` (or `MO2_FIXTURES=1`).

## Run

Install the .NET 9 SDK, then from the repository root:

```sh
git submodule update --init frontend/upstream
./frontend/run.sh
```

The launcher also recognizes a repository-local SDK at `.tools/dotnet`.

## Instance startup

Open the settings gear, then use **Choose MO2 launcher…** beside an instance to select the
script or executable that starts that instance. Selecting a profile then starts
it if needed and waits for the MO2 bridge. Setup and extension dialogs remain in
MO2. An already-running host is reused, including when its bridge responds slowly.
The bridge extension must be installed in that instance first.

`start-mo2-proton.sh INSTANCE PREFIX PROTON STEAM_ROOT STEAM_APP_ID` starts an
existing Windows MO2 installation with the Qt input workaround used on this
Steam Deck. Use a separate Proton prefix for each instance. Put that invocation
in an executable script and choose it as the instance launcher. A prefix lock
prevents concurrent invocations during startup; the script does not copy mods,
create profiles or replace MO2 configuration.
The launcher reads Proton's `require_tool_appid` and uses that installed Steam
runtime's `run` script. This supplies the matching multimedia libraries needed
by games even when MO2 itself starts without them. If the runtime is installed
in another Steam library, set `MO2_STEAM_RUNTIME=/path/to/runtime/run` in the
instance launcher. A required runtime that cannot be found produces an error.

Connections can also be registered without opening the UI:

```sh
./frontend/run.sh --register-mo2 /path/to/mo2-instance /path/to/launch-mo2.sh
```

Only instance, bridge and launcher paths are stored in
`$XDG_CONFIG_HOME/mo2-nexus-frontend/instances.json` (normally under `~/.config`).
All mod, profile and download state remains in MO2.

## Reference and source reuse

`upstream` pins NexusMods.App release `v0.21.1`, commit
`96cc724a182498ae3b4b81960a29a095437145fe`. This matches the supplied Linux
reference `/home/deck/Downloads/NexusMods.App-0.21.1-1.linux-x64/`.
The GPL-3.0 source and assets remain in the submodule with their license.
The host reuses the upstream theme, views, workspace controller, panels,
new-tab page, and navigation history. Its reflection adapter instantiates
the upstream internal controller without modifying the upstream repository.
The host files are distributed under this repository's GPL-3.0 license.

## Current verification

Build and run a small workspace integration check, saving a native screenshot:

```sh
MO2_VERIFY_WORKSPACE=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/four-panels.png" ./frontend/run-fixtures.sh
```

Create `frontend/artifacts` first. The window closes after capturing. The check
exercises add/select/close tabs, adding panels up to four, and closing/restoring
a panel. It is a partial integration check, not proof of full behavioral parity.

## Remaining scope

The live frontend uses the native NMA navigation and panels with real MO2
games, profiles, mod/plugin lists, downloads and original dialogs. FNV in-game
acceptance and switching between the registered FNV and Skyrim hosts pass.
Remaining work includes:

- Expand verification of native plugin diagnostics, including missing-master and LOOT messages.
- Verify panel drag/drop, history and state restoration more broadly.
- Review remaining page states and visual layout against NMA.
- Publish the branch to the requested GitHub remote when authentication is available.

MO2 remains the owner of installed mods and profiles; the live frontend does
not maintain an independent NMA Library or deployment store.

## New Vegas scenarios and reference fork

`MO2_VERIFY_SCENARIOS=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/fnv-loadouts.png" ./frontend/run-fixtures.sh`
checks fake game add/remove, loadout clone/delete, sidebar navigation and history.
The installation path supplied in this session is shown in the Steam tooltip;
`MO2_SCENARIO_GAME_PATH` and `MO2_SCENARIO_COVER` override local fixture metadata.
The fixture currently starts with one detected New Vegas game and no loadouts.

A separate [Nexus app reference fork](reference/README.md) is being developed to
compare New Vegas screens against the real app, as requested. This does not
connect the mock frontend to MO2 or to real mod data.

## Settings and plugin-order fixtures

The toolbar Settings button opens the original settings page. Nine upstream
settings support in-memory drafts, discard, save and reopening in another tab.
No setting writes to MO2 or the installed reference app.

```sh
MO2_VERIFY_SETTINGS=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/settings.png" ./frontend/run-fixtures.sh
MO2_VERIFY_ORDER=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/plugin-order.png" ./frontend/run-fixtures.sh
```

The new-tab page includes a standalone **Plugin load order** scenario using the
original load-order view, adapter and commands with six fake ESM/ESP entries.
The order check invokes the native row command, checks the updated index and
row position, rejects moving a master after its dependent, and toggles display
direction. Updates are published as a batch after all indices settle.
The standalone editor and the original loadout **Mods/Rules** container were
rendered and visually inspected. Drag/drop verification, file conflicts and
complete reference comparisons remain unfinished.

## Installed-mod and loadout fixtures

Loadout cards and spine entries open the original **All** page. Each loadout has
its own fake mod list and plugin order; cloning copies their current state.
The native table supports enable/disable, selection, uninstall and its empty
state. Disabled or removed mods mark their associated plugin inactive.

```sh
MO2_VERIFY_INSTALLED=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/installed-rules.png" ./frontend/run-fixtures.sh
MO2_VERIFY_INSTALLED=1 MO2_INSTALLED_CAPTURE_MODS=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/installed-mods.png" ./frontend/run-fixtures.sh
```

This check exercises the original tab control, enabled toggle, multi-selection,
uninstall and empty state, visits a loadout card and verifies clone independence.
Screenshot capture waits for the check to finish. An empty in-memory MnemonicDB
connection satisfies the original table adapter's constructor; the displayed
rows come entirely from fixture providers. No installed game or MO2 database is
opened. The loadout sidebar/context, file viewing, collection actions and full
reference image comparisons still need implementation and verification.

## Workspace switching

Home and each fixture loadout now use separate original workspace instances.
Spine navigation restores their current panels and tabs. The top bar, active
spine item and original loadout sidebar follow the active workspace. My Mods
opens the native collection header and Mods/Rules view; new-tab discovery also
provides All and My Mods for the current loadout. Deleting a loadout unregisters
its workspace and returns Home if needed.

```sh
MO2_VERIFY_CONTEXTS=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/loadout-workspace.png" ./frontend/run-fixtures.sh
```

The check verifies workspace independence, panel/tab identity on returning,
sidebar rendering, collection navigation and deletion cleanup. The resulting
My Mods screen was visually inspected. This does not prove full mouse/keyboard
interaction parity or restart persistence. New Collection, utilities, Preview/Apply and Play in this sidebar still use
upstream design placeholders;
they require functional fixtures before the frontend is complete.

## Library fixture

The Library sidebar entry and new-tab discovery now open the native Library
view and table. A shared fake archive catalog installs into each loadout's
independent mod list and plugin order. Native row and batch install commands,
installed status, deselection, library deletion and live catalog additions work.
The Add file picker imports names as fake metadata only; it does not unpack or
install real archive contents. The two initial archive sizes are fixture values.

```sh
MO2_VERIFY_LIBRARY=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/library-panels.png" ./frontend/run-fixtures.sh
```

This check opens Library in a second original panel, invokes row/batch installs,
checks duplicate prevention and installed status, verifies that the already-open
Mods panel updates, and confirms library deletion preserves installed mods.
It also checks the empty-to-populated transition. The resulting two-panel view
was visually inspected. Update flows, advanced installers, collection downloads,
file-picker interaction and reference pixel comparisons remain unverified or
unimplemented; the corresponding scenario commands are still placeholders.

## Native dialogs and collection rename

The fixture window manager now delegates modal and modeless dialogs to the
original NMA dialog implementation. Rename Collection opens the original prompt,
leaves state unchanged on Cancel, and updates shared collection state on Accept.
The sidebar, open collection headers and tab titles, installed-mod collection
labels, and Library installation target all observe the same name. Clone copies
that name into the new loadout.

```sh
MO2_VERIFY_DIALOGS=1 MO2_DIALOG_SCREENSHOT="$PWD/frontend/artifacts/rename-dialog.png" MO2_SCREENSHOT="$PWD/frontend/artifacts/renamed-collection.png" ./frontend/run-fixtures.sh
```

This check opens the actual modal twice, exercises Cancel and Accept, and checks
updates across an already-open collection and Library panel. Both the dialog and
the resulting two-panel screen were visually inspected. It invokes native button
commands rather than physical mouse input; keyboard focus, modeless behavior,
other dialogs and complete reference comparisons remain unverified. Multi-
collection creation/deletion and sharing still need fixture implementations.

## MO2 extension compatibility

[Extension compatibility requirements](EXTENSION_COMPATIBILITY.md) record the
requirement to preserve existing MO2 plugin interfaces, the Windows dependencies
found in the host, and the separate Wine/native-Linux runtime considerations.
The frontend does not load existing extensions yet. A read-only plugin inventory
helper is available at `frontend/tools/audit_plugins.py`.

## External interaction observation

`MO2_INTERACTION_REPORT=/tmp/mo2-state.json` enables a read-only JSON report of
panel/tab IDs and rendered control bounds for external mouse/keyboard checks.
It does not drive commands. A mouse click on the native New Tab toolbar button
was observed to create a tab. Cross-panel tab dragging has not been verified;
the pinned release's tab header implements selection and close/middle-click,
with no tab drag/drop handlers. Panel resize uses the separate native
`PanelResizerView` and still needs a successful pointer test. Shared-desktop focus
changes interrupted the initial drag attempts; these are not passing checks.
