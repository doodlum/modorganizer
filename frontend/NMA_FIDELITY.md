# FNV fork comparison

Reference checked on 2026-09-13: `../reference-fnv`, branch
`feat/fnv-reference`, commit `3b0244e7d21ea19afabcc3d81539c9e11ae8d642`.
The supplied release in Downloads does not include this FNV support and is not
the correct reference for the game workspace.

Rebuilt `src/NexusMods.App/NexusMods.App.csproj` successfully with the local
.NET SDK. Ran its Debug executable using the existing isolated
`frontend/artifacts/fnv-profile/{data,config,cache,state}` XDG directories.
The earlier executable was stale. The rebuilt window identifies commit
`3b0244e` and shows both FNV and Skyrim in My Games.

## Observed differences requiring correction

| Area | FNV fork | Current MO2 frontend / required correction |
| --- | --- | --- |
| Home | My Games and My Loadouts sidebar, Home title | Keep these in Home only. |
| Spine | Square game icon for each loadout; click opens its workspace | Currently one portrait cover per game, opening filtered My Loadouts. Map entries directly to MO2 profiles and open the profile workspace. |
| Game sidebar | Installed section, All/My Mods, Utilities, Health Check; launch controls at bottom | Live frontend always uses Home menu and separate custom launch/header rows. Replace with the native contextual menu and MO2-backed launch control. |
| Mods | Native page header, Mods/Rules tabs, search and toolbar, native table | Current custom Mo2ModsView omits much of this structure. Reuse the native view with MO2-supported data/actions. |
| Health Check | Dedicated page, issue list or clear success state | Missing from live navigation. Populate native presentation from actual MO2 diagnostics; do not report success from absence of frontend checks. |
| Artwork | Portrait tiles in My Games; IconImage in spine and loadout cards | Separate tile and icon sources. Skyrim has native embedded artwork. FNV fork currently uses a rectangular Steam header for IconImage, so that source also needs a proper game icon. |
| My Loadouts | Create card first, then loadout cards with small icon/badge | Match placement and styling while showing actual MO2 profiles across games/instances. |
| MO2 windows | Not applicable to standalone reference | User requires background MO2 UI to stay hidden. Current bridge explicitly shows/raises the main window for some dialogs; frontend host lifecycle and dialog ownership need correction and runtime verification. |

NMA Library, collections, Apply and External Changes must only carry forward
where their behavior maps to MO2-owned state. Visual fidelity must not introduce
a second archive library, deployment system or profile authority.

## Runtime evidence

Actual pointer navigation: Home → FNV spine → All → Health Check → Home →
My Loadouts. Screenshots in ignored local artifacts:

- `artifacts/reference/fnv-fork-expanded-20260913.png`
- `artifacts/reference/fnv-fork-health-20260913.png`
- `artifacts/reference/fnv-fork-loadouts-20260913.png`

No Apply, game launch, installation or profile mutation was performed in the
reference app during this comparison. These observations establish remaining
work; they do not establish frontend parity.
