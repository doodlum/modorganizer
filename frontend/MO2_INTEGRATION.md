# MO2-owned frontend integration

This supersedes the earlier fixture-only replication target. MO2 owns all real
state. The original frontend and alternate frontend must be interchangeable,
with existing extensions preserved. Proton is acceptable for now; native Linux
backend support is not a prerequisite.

## Required mappings

- My Loadouts displays MO2 profiles grouped by game/instance. Creation, cloning,
  rename, deletion and selection operate on those profiles.
- Installed mods come from the instance's mod directory and the selected
  profile's mod list. Preserve MO2 enabled state, priority, separators, unmanaged
  content, missing entries, overwrite and conflicts as resolved by its core.
- Plugins come from MO2's plugin list/game support, preserving enabled state,
  ordering, forced plugins, diagnostics and game-specific rules.
- Library/download views represent MO2's downloads folder and download manager.
  No separate NMA archive database or independent install membership may become
  authoritative. Installation uses MO2's installers and extensions.
- NMA collections and Apply/deployment semantics only carry forward where they
  correspond to real MO2 operations. Existing fixture collection behaviour is
  legacy test scaffolding, not the intended integration model.
- Existing MOBase/mobase extensions remain hosted by MO2. Qt extension UI stays
  available. The alternate frontend must not require a replacement plugin API.

## End-to-end FNV acceptance

Successful Fallout: New Vegas loading with mods is required, not optional.
Use an isolated MO2 FNV instance/profile to avoid altering Skyrim instances.

1. Discover FNV and initialize the MO2 instance with its existing game extension.
2. Install a known test mod through MO2. Resolve its actual prerequisites; the
   MCM archive examined earlier contains a nested FOMOD and requires more than
   merely copying the outer archive into a downloads folder.
3. Show the mod list in the left workspace panel and the plugin list in the right
   panel simultaneously, both attached to the same MO2 profile. Selection,
   enabled state, ordering and switching profiles must update consistently.
4. Confirm that reopening the profile in original MO2 shows the same state.
5. Launch FNV through MO2/USVFS under Proton with the selected profile. Verify
   that the game reaches usable gameplay and the test mod is actually loaded,
   using an observable in-game effect or a game/runtime plugin report. A process
   starting or an ESP appearing in a list does not establish success.
6. Verify disabling the mod in MO2 is reflected by both frontends and the next
   launch. Keep test saves separate from existing playthrough saves.

## Current evidence and remaining work

The default frontend opens the real My Loadouts catalog without assuming an
active profile. Fixture mode requires `MO2_FIXTURES=1` or `run-fixtures.sh`.
`Mo2ProfileFiles` reads
actual instance/profile files and the downloads folder without writing them:

```sh
.tools/dotnet/dotnet frontend/MockHost/bin/Debug/net9.0/MockHost.dll --inspect-mo2 /path/to/instance /path/to/another-instance
```

Its output retains modlist file order (MO2 reverses regular-mod priorities) and
literal plugin activation markers. It deliberately does not pretend that disk
files resolve missing/unmanaged mods, forced activation or running downloads.
Those decisions require the MO2 runtime and game extensions. A non-Z Windows
path on Linux requires the actual Wine drive mapping and currently fails
explicitly instead of silently reading another directory.

`frontend/tools/check_profile_reader.py <dotnet> <MockHost.dll>` checks multiple
instances, configurable paths, BOM/CRLF input, duplicate/missing/unmanaged mod
entries, plugin order/markers and archive metadata/partial-download discovery
using temporary directories. These checks pass.

The installed portable Skyrim SE instance was read successfully: one Default
profile, 89 recorded modlist entries, four existing mod directories, 88 plugin
load-order entries and no downloads. This is discovery evidence, not runtime
integration. Two accessible MO2 configurations found in this session both manage
Skyrim SE. FNV's executable exists, but there was no NVSE loader or ESP directly
in its Data directory at inspection time. No FNV gameplay through MO2 has been verified.

The first host bridge now loads as an ordinary MO2 Python tool extension under
Proton. Its C# client read the isolated FNV `Frontend Test` profile (nine DLC mod
entries, ten plugins). A real plugin activation and restoration through the MO2
API succeeded. See [bridge setup and protocol](mo2-plugin/README.md).
Live mode binds the active host profile
to simultaneous left mod and right plugin panels. Its native plugin row command
changed MO2 priority; an independent host read agreed, and original order was
restored. The initial rendered view showed nine mod rows and ten plugin rows.

Downloads and installation now use MO2's downloader and installer APIs. Nexus
account recognition, two MCM archive downloads, original XML FOMOD installer
execution, and MCM activation through the frontend were verified. MO2 saved the
enabled mod and ESP in its profile files; the live view now has ten mod entries
and eleven plugins. FNV gameplay and MCM runtime prerequisites remain unverified.
The supplied Skyrim directory now contains `SkyrimSE.exe`; its two existing MO2
instances remain available for profile discovery and subsequent tests.

My Loadouts now lists these instances' real profiles. A copy made through MO2's
native profile manager retained independent mod activation when switching
between it and the original. The bridge waits for the host refresh and checks
profile ownership before applying a selection. Cross-game host startup and
connection still need testing; listing a Skyrim profile is not runtime proof.

Mod priority and plugin activation toolbar controls now call MO2 directly. A
live check disabled MCM’s ESP independently of its enabled mod, changed mod
priority, verified both against the host, and restored the original state.

Next work is remaining mod-management controls and the FNV acceptance run above.
Do not claim integration complete based on the reader, mock UI or screenshots.

The live executable picker now calls MO2's configured process runner and waits
through its own application lifecycle API. xNVSE 6.4.8 was installed from the
official release into the supplied FNV game root (four new DLL/EXE files;
`artifacts/xnvse-install-manifest.json` records their hashes). The first launch
logged MCM Extensions loading correctly via USVFS. After initializing the
isolated prefix registry and graphics settings with Fallout Launcher, the game
reached its title screen. Gameplay, MCM's in-game menu and a disabled-mod
comparison still need verification. Desktop automation has encountered focus
problems; the isolated test profile now uses windowed 1024×640 preferences
seeded from the original launcher's output. No Skyrim configuration was changed.

Further startup checks: the prefix initially lacked the game's registry entry,
Documents/My Games directory and AppData/Local/FalloutNV directory. The original
launcher initialized the registry-dependent graphics setup; creating the local
application data directory and restarting MO2 made the test profile's windowed
INI take effect. That run stalled at a black screen, including with MCM disabled.
Disabling MCM removed its DLL from the next xNVSE runtime log, confirming that
mod activation affects the virtual game view. MCM was restored afterward and
the stalled test game was closed. The startup issue is unresolved; neither
usable gameplay nor the MCM in-game menu has been verified.

Default startup now opens My Loadouts from real instance/profile files, with no
fixture mods or assumed active profile. An isolated connection-registration
check started without `MO2_BRIDGE_DIRECTORY`, selected the FNV profile using its
catalog button, and verified that both live panels populated from the host.
Skyrim profiles remained discoverable. Legacy workspace fixtures still pass via
the explicit `run-fixtures.sh` / `MO2_FIXTURES=1` mode.

Profile selection now starts a configured instance launcher when no running
host or launcher is found, then waits for bridge readiness. A snapshot timeout
never triggers another launch. The isolated FNV test passed from a stopped
host, with a new bridge session and exactly one launcher invocation. A separate
frontend process then reconnected to the same live host without invoking the
launcher again. A second deliberately stopped-host run also passed. The bundled
Proton launcher holds a per-prefix lock during startup and uses the established
Qt input workaround. Cross-game startup and connection are verified below.

Cross-game startup now passed with the existing Skyrim MO2 instance at
`~/Games/mod-organizer-2-skyrimspecialedition/modorganizer2`, running in a separate
Proton prefix. Its bridge reported 150 mod entries and 150 plugins; every
activation state and priority in both panels matched an independent snapshot.
The game-specific executable and download contexts switched, and returning to
FNV restored its mod list and Nexus domain. Switching profiles reuses the mod
tab instead of adding another copy each time.

Before installing the bridge, the Skyrim instance's configuration and profiles
were backed up privately under `artifacts/skyrim-before-bridge`. Native MO2
startup added 121 detected entries to its mod list and removed none. Existing
plugin activation and load-order files were unchanged. No Skyrim game launch
was performed. The separate `~/ModOrganizer2` instance is still catalog-only.

Download Pause, Resume and Cancel now invoke the existing MO2 download-view
slots. Each command resolves the archive path against the current unfiltered
host download model immediately before acting; frontend requests never retain
row indices. A throttled local HTTP archive test exercised the actual frontend
buttons: paused bytes stayed constant, resumed bytes grew, and cancellation
removed the partial archive while leaving the original downloads unchanged.
The transfer itself was created through MO2's startDownloadURLs API by a narrow
isolated test driver. The driver was removed from disk, its trigger consumed,
and the local server/test payload cleaned up. No Nexus bandwidth was used.

Mod uninstall now uses the original MO2 source model's single-mod removal path,
including its confirmation dialog, profile cleanup, origin removal and download
notification. The frontend resolves the current mod by name, rejects essential
content, and reads actual host state after the dialog. Cancel stops a multiple
selection removal sequence. A disposable mod in the isolated FNV instance passed
the actual frontend uninstall-command check: No preserved its files and entry;
Yes removed both; unrelated mod states/priorities and archives were unchanged.
The fixture was removed through MO2 and the temporary dialog helper was removed.

The FNV startup investigation found that GE-Proton10-4 declares Steam runtime
app 1628350, but the launcher was invoking Proton outside that runtime. A direct
game baseline stalled before menu controls and logged missing multimedia
libraries. Running the same game/prefix inside the installed sniper runtime
reached the full menu. The launcher now resolves Proton's declared runtime from
Steam metadata (with MO2_STEAM_RUNTIME override for another library). The next
frontend/MO2/NVSE launch reached the full windowed menu with MCM Extensions loaded,
accepted New Game, and played the intro. Three isolated launcher tests passed
for argument preservation with spaces, missing runtime, and explicit override.
