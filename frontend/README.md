# Alternate Nexus frontend (in progress)

Standalone Linux Avalonia host for Mod Organizer. The MO2 C++ application is not required.
All frontend page data is generated in memory; no MO2 data or game files are read. Optional artwork is read from the local Steam cache.

## Run

Install the .NET 9 SDK, then from the repository root:

```sh
git submodule update --init frontend/upstream
./frontend/run.sh
```

The launcher also recognizes a repository-local SDK at `.tools/dotnet`.

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
MO2_VERIFY_WORKSPACE=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/four-panels.png" ./frontend/run.sh
```

Create `frontend/artifacts` first. The window closes after capturing. The check
exercises add/select/close tabs, adding panels up to four, and closing/restoring
a panel. It is a partial integration check, not proof of full behavioral parity.

## Remaining scope

This is a foundation, not a completed replica. My Games and My Loadouts now use
shared Fallout: New Vegas fixtures. Add/remove game, create/clone/delete loadout,
sidebar navigation and history are interactive and checked. Loadout cards and
spine entries now open independent installed-mod fixtures; the loadout sidebar,
dialogs and remaining pages are still incomplete.
Remaining work includes:

- Extend the shared fake scenarios with mod and collection data.
- Complete per-loadout spine navigation, all topbar actions, settings, dialogs, overlays and downloads.
- Implement library, installed mods, collections, load order, file conflicts, diagnostics and installer scenarios using upstream page interfaces.
- Verify panel resize, drag/drop, history, state restoration and every tab action through UI interaction.
- Match the release footer and the reference window layout; compare every page and state visually.
- Publish the branch to the requested GitHub remote.

The initial source build emits upstream warnings, including an advisory for an
upstream telemetry dependency. The host does not register telemetry services.

## New Vegas scenarios and reference fork

`MO2_VERIFY_SCENARIOS=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/fnv-loadouts.png" ./frontend/run.sh`
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
MO2_VERIFY_SETTINGS=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/settings.png" ./frontend/run.sh
MO2_VERIFY_ORDER=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/plugin-order.png" ./frontend/run.sh
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
MO2_VERIFY_INSTALLED=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/installed-rules.png" ./frontend/run.sh
MO2_VERIFY_INSTALLED=1 MO2_INSTALLED_CAPTURE_MODS=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/installed-mods.png" ./frontend/run.sh
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
MO2_VERIFY_CONTEXTS=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/loadout-workspace.png" ./frontend/run.sh
```

The check verifies workspace independence, panel/tab identity on returning,
sidebar rendering, collection navigation and deletion cleanup. The resulting
My Mods screen was visually inspected. This does not prove full mouse/keyboard
interaction parity or restart persistence. Library, New Collection, utilities,
Preview/Apply and Play in this sidebar still use upstream design placeholders;
they require functional fixtures before the frontend is complete.
