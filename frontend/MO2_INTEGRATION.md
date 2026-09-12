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

The current frontend remains mostly fixture-backed. `Mo2ProfileFiles` now reads
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
in its Data directory at inspection time. No FNV MO2 launch has been verified.

Next implementation work is the MO2 host bridge, binding real profiles/mods/
plugins/downloads to the frontend, then the isolated FNV acceptance run above.
Do not claim integration complete based on the reader, mock UI or screenshots.
