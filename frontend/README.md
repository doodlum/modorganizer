# Alternate Nexus frontend (in progress)

Standalone Linux Avalonia host for Mod Organizer. The MO2 C++ application is not required.
All instantiated page data is currently generated in memory; no MO2 data or real game installation is read.

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

This is a foundation, not a completed replica. Upstream design models still
supply the My Games and My Loadouts data, and many of their commands do nothing.
Remaining work includes:

- Replace designer models with interactive deterministic fake game/mod/loadout scenarios.
- Wire sidebar and spine navigation, all topbar actions, settings, dialogs, overlays and downloads.
- Implement library, installed mods, collections, load order, file conflicts, diagnostics and installer scenarios using upstream page interfaces.
- Verify panel resize, drag/drop, history, state restoration and every tab action through UI interaction.
- Match the release footer and the reference window layout; compare every page and state visually.
- Publish the branch to the requested GitHub remote.

The initial source build emits upstream warnings, including an advisory for an
upstream telemetry dependency. The host does not register telemetry services.
