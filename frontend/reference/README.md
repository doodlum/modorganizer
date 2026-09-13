# Fallout: New Vegas reference fork

The user requested a reference fork of `doodlum/NexusMods.App` to test their
New Vegas installation. Development checkout: `/home/deck/mo2/reference-fnv`,
branch `feat/fnv-reference`, based on `48284f38c2d91e53904b41093090fbaa8b5113ed`.

The patch beside this file preserves the reference branch changes in the MO2
repository while remote publication is unavailable. To recreate it elsewhere:

```sh
git clone https://github.com/doodlum/NexusMods.App.git reference-fnv
git -C reference-fnv checkout -b feat/fnv-reference 48284f38c2d91e53904b41093090fbaa8b5113ed
git -C reference-fnv am ../frontend/reference/fnv-reference.patch
git -C reference-fnv submodule update --init extern/SMAPI
```

From the MO2 repository root, `./frontend/run-reference.sh` runs the reference
app using a separate XDG profile under `frontend/artifacts/fnv-profile`.
This is the FNV-enabled Nexus app with its own backend, used as the UI reference.
`./frontend/run.sh` runs the alternate frontend backed by real MO2 instances;
`./frontend/run-fixtures.sh` selects the separate fake-data scenarios.

Implemented reference functionality: Steam game identifier 22380, executable
`FalloutNV.exe`, Nexus game ID 130, NVSE launching, Data-folder/FOMOD installer
routing, header/master parsing, plain plugin activation entries, and timestamp
load-order application. Steam-cached artwork is loaded locally; generic
upstream fallback art is embedded for machines without that cache.

Verification is incomplete. A full app build and standalone checks have run.
The reference app launches in its separate profile, detects the supplied New
Vegas installation and renders its game card; this was visually inspected.
The checks parse synthetic headers and all ten installed ESM headers; timestamp
writes are tested only in a temporary directory. Real-game deployment, rollback,
Proton paths, UI loadout management and installer coverage remain to be verified.
The reference also fixes a Debug startup crash caused by duplicate domains in
the current upstream games.json metadata feed.

Run standalone checks (read-only for the supplied game path):

```sh
dotnet run --project reference-fnv/tools/FnvSmoke/FnvSmoke.csproj -- \
  '/home/deck/.local/share/Steam/steamapps/common/Fallout New Vegas/Data'
```
