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

## First corrections verified

The live spine now contains one entry per actual MO2 profile. Entries use local
square Steam application icons, and loadout cards use the same icon source.
My Games retains portrait tiles. Create cards precede the profiles belonging
to each instance.

`MO2_VERIFY_CATALOG=1 MO2_VERIFY_PROFILE_NAVIGATION=1` passed with the actual
Skyrim and isolated FNV hosts. Profile icon commands connected to both games,
updated selection and both live panels, and replaced Home navigation with
the native loadout sidebar. Home restored My Games/My Loadouts. Evidence:
`artifacts/profile-navigation.png` and `/tmp/mo2-profile-navigation.log`.

The loadout sidebar currently maps Downloads, My Mods and Plugins. Health Check
is still absent; deployment/collection controls are omitted. The custom mods
page and launch/header rows still need replacement. This is not full parity.

Frontend-host startup now sets `MO2_FRONTEND_HOST=1`. The bridge suppresses
the main window only in that mode using Qt's `WA_DontShowOnScreen`. Both hosts
still connected during the navigation test. The splash and command window
were visibly present during startup: background UI suppression is incomplete.
Explicit native dialogs and hidden-host lifetime still need separate checks.

## Health Check integration

Health Check now opens NMA's native diagnostic list and detail pages. A guarded
bridge command invokes MO2's Notifications slot, which refreshes all enabled
`IPluginDiagnose` providers before reading their descriptions. The adapter
marks only the temporary ProblemsDialog as `WA_DontShowOnScreen` before it is
shown, reads its tree, and closes it. No guided fix is executed by a health scan.
MO2 supplies no severity, so its notification reports appear as warnings.

The page refreshes while active and invalidates its result when the profile
changes. A missing connection or failed scan produces an unavailable state,
not an empty successful result. Diagnostic descriptions are converted from
MO2 HTML to plain text and escaped for the native Markdown detail renderer.

FNV runtime checks passed for the normal zero-report profile and a temporary
original `IPluginDiagnose` extension in the isolated host. Its issue appeared,
opened a native detail page through the issue-row control, cleared, and returned.
The final rendered issue list was checked after returning from details. All
mod, plugin and profile state stayed unchanged. The temporary extension and its
marker were removed after verification. Evidence: `artifacts/health-fnv.png`,
`artifacts/health-fnv-diagnostic.png`, `/tmp/mo2-health-fnv.log` and
`/tmp/mo2-health-fixture.log`. The bridge contract suite has 15 passing checks,
including stale-profile rejection for health scans.

Skyrim's native host also returned zero reports without changing state
(`artifacts/health-skyrim.png`, `/tmp/mo2-health-skyrim.log`). The disconnected
page displayed unavailability rather than green success
(`artifacts/health-disconnected.png`, `/tmp/mo2-health-disconnected.log`).
With a report present only in the isolated FNV host, the same open Health Check
page changed from one warning to zero on Skyrim and back to one on FNV without
reopening the page (`artifacts/health-cross-game.png`,
`/tmp/mo2-health-cross-game.log`). Native MO2 changes trigger a refresh, in
addition to the periodic scan.

Remaining fidelity work includes the mods page, launch/header controls and
startup splash/console suppression. The current shared profile workspace also
needs an audit of profile-specific tabs and historical diagnostic details;
passing the live list switch check does not prove every tab has the right
profile lifetime.

## Native mods page

The live mod panel now embeds NMA's original `LoadoutView`: its page header,
Mods/Rules tabs, search control, selection toolbar, table and row menus. The
MO2 adapter supplies priority, name, conflicts/status and activation columns.
Its default root ordering follows numeric MO2 priority after data updates.
The Rules tab uses the same MO2-backed plugin order as the right-hand panel.

View files routes to MO2's existing mod details dialog; the empty-state Downloads
button routes to the MO2 download folder page. Publishing and collection controls
are not enabled. Priority actions use native toolbar buttons. Overwrite retains
file access but cannot be activated, removed or moved.

`MO2_VERIFY_NATIVE_MODS=1` and `MO2_VERIFY_CONTROLS=1` passed against the isolated
FNV profile: native search/clear, row selection/deselection, activation/restoration,
Overwrite restrictions, Rules tab data, plugin activation independence and mod
priority movement/restoration. Original state was restored. Evidence:
`artifacts/native-mods-verified.png` and `/tmp/mo2-native-mods-check.log`.
The launch/header rows and startup-window suppression remain separate work.

The native View files toolbar control opened MO2's actual Overwrite dialog and
returned successfully after it was closed (`/tmp/mo2-native-mods-files.log`,
`artifacts/native-mods-files-return.png`). The screenshot attempt while the
dialog was open produced no file; the return screenshot and runtime action
check are the available evidence.

Cross-game verification also passed with the native mods view: Skyrim's 151
mod rows and 150 plugins matched an independent MO2 snapshot, including eight
mods with native conflict messages. The default row order remained numeric
MO2 priority on Skyrim and after returning to FNV. Evidence:
`/tmp/mo2-native-mods-cross-game.log` and `artifacts/native-mods-cross-game.png`.

## Sidebar launch controls

NMA's native PLAY control now sits below an MO2 executable picker at the bottom
of the profile sidebar. The separate launch/profile/refresh header rows were
removed. Downloads remain in the sidebar and profile management remains on My
Loadouts cards. The native top-bar subtitle displays the active profile name.
PLAY uses the existing MO2 launch method and is disabled for a missing connection,
invalid selection or busy host. There is no independent Apply operation.

`MO2_VERIFY_SIDEBAR_LAUNCH=1` passed on FNV, Skyrim and the return to FNV:
three/four executable choices matched independent host snapshots, the native
button bound to the MO2 launch command, invalid selection disabled it, and Home
hid/restored the controls without losing selection. The native mod-count label
also restored. Evidence: `/tmp/mo2-sidebar-launch.log` and
`artifacts/sidebar-launch.png`. These checks verify UI selection/binding; they
did not launch a game. Existing FNV gameplay evidence covers the unchanged MO2
runner. `MO2_VERIFY_LAUNCH` now invokes the sidebar command for future launch checks.
