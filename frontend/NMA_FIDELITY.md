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

## Frontend-host startup windows

The Proton launcher now invokes `ModOrganizer.exe` directly from its instance
folder, with the Qt workaround and frontend-host flag applied inside the Steam
runtime. This removes the batch command window. The bridge installs a Qt event
filter during extension initialization, before MO2 constructs its splash. In
frontend-host mode, the splash, main window and informational `MessageDialog`
toasts receive `WA_DontShowOnScreen` before showing. Other dialog classes are
unaffected; ordinary MO2 launches without the flag retain the original UI.

A cold-start catalog → FNV → Skyrim → FNV run passed the native sidebar launch
checks and independent host comparisons (Skyrim: 151 mod rows, 150 plugins).
An external X11 monitor sampled visible windows 715 times at roughly 50 ms
intervals, recording both the reference and frontend windows but no MO2 or
console windows, including Steam application classes 22380 and 489830. The
first run exposed a transient MO2 informational toast; the repeat run after
adding `MessageDialog` suppression recorded none. Polling cannot exclude a
window lasting less than a sample interval. Evidence: `/tmp/mo2-quiet-startup.log`,
`/tmp/mo2-startup-windows.log`, and `artifacts/quiet-startup.png`.

Launcher checks passed (3), including direct executable arguments, spaced
paths, working directory, Qt environment and frontend-host flag. Bridge contract
checks passed (15). This run did not launch either game or retest intentional
installer dialogs; existing gameplay evidence is recorded separately.

## Diagnostic detail panel lifetime

Health detail contexts now capture the originating MO2 endpoint and profile at
entry creation, so a retained entry or navigation-history context cannot be
reassigned to whichever game is selected later. Activated detail pages query
MO2 again and refresh every ten seconds. They clear a resolved report, restore
a recurring report, and suppress its contents during disconnection or selection
of another profile. Ambiguous changed reports send the reader back to Health
Check instead of choosing an arbitrary matching title.

The NMA diagnostic page and panel remain native. Its body uses a selectable
plain-text view for MO2 descriptions, preserving punctuation and filenames
without Markdown parsing. The initial screenshot exposed literal backslashes
from the previous escaping approach; direct text rendering removes those.

`MO2_VERIFY_CATALOG=1 MO2_VERIFY_HEALTH_DETAILS=1` passed using a temporary
original MO2 diagnostic extension in the isolated FNV host. The native entry
opened a new panel; its visible details cleared and returned when the report
was removed and restored. Actual profile-spine commands switched to Skyrim
and back while the same detail panel remained visible. The final body matched
its plain-text model and its screenshot was inspected. Evidence:
`/tmp/mo2-health-details-profile.log`, `artifacts/health-details-profile.png`.
The build passed with the existing upstream OpenTelemetry advisory. This proves
detail-panel invalidation, not separate per-profile workspace/layout persistence.

## Separate native workspaces per MO2 profile

The live shell now retains one native workspace and contextual sidebar per
MO2 endpoint/profile-path pair. First visits start with mods and plugins side
by side; subsequent visits restore the existing workspace instead of selecting
or recreating the mods tab. Profile changes observed from MO2 also update the
active workspace. Panel actions are disabled while profile selection is pending.
Home remains separate. All pages still obtain their mod/plugin/download state
from the connected MO2 host; the workspace cache stores presentation state.

NMA recreates the mods search control when a dormant view returns, so the live
mods page now retains its search text and expanded state. The first verification
run exposed search loss; the repeat passed with preserved text.

`MO2_VERIFY_PROFILE_WORKSPACES=1` with catalog startup passed FNV → Skyrim →
FNV → Skyrim → FNV → another FNV profile → FNV. It checked distinct workspace
IDs, independent two/three-panel layouts, bounds, tabs, selected tabs and native
search values, with unchanged FNV mod/plugin state. Evidence:
`/tmp/mo2-profile-workspaces.log`, `artifacts/profile-workspaces.png`.

A separate regression run passed native profile navigation, sidebar launch
selection, Home Back/Forward history and independent host comparisons for
Skyrim's 151 mod rows and 150 plugins, returning to FNV. Evidence:
`/tmp/mo2-profile-workspaces-regression.log` and
`artifacts/profile-workspaces-regression.png`.

This implements in-session restoration. Saving layouts across frontend restarts
remains unfinished; the older untracked layout-storage prototype is not wired in.

Health regressions also passed with the temporary original MO2 diagnostic
extension: each profile retained its own health-list page; the FNV detail panel
was absent from Skyrim's workspace and refreshed when returning to FNV.
Resolved/recurring reports still cleared/restored in the native detail panel.
Evidence: `/tmp/mo2-profile-workspaces-health.log` and
`artifacts/profile-workspaces-health.png`. The fixture and marker were removed
after both MO2 hosts exited.

## Layout restoration across restarts

Version 2 of the frontend layout store now saves Home and each endpoint/profile
workspace independently. It preserves panel bounds, selected panels/tabs,
current pages, game filters, native New Tabs, mod searches, search expansion and
Mods/Rules choice. Diagnostic detail references retain their originating profile
and title; their report text is queried again from MO2, never cached as current
health data. Unvisited saved layouts survive later saves.

The store writes atomically when presentation state changes and on close. It
contains no mod/plugin activation, priority, archive membership or profile
creation/selection instructions. Startup restores Home; a profile layout is
restored only when its MO2 connection is selected. The default file is
`workspace-layout.json` in the frontend configuration folder;
`MO2_FRONTEND_LAYOUT` provides an isolated override. Screenshot checks omit the
default store unless the override is explicit.

Malformed/unsupported files are preserved and reported in the status bar. Bounds,
coverage, overlapping panels, selected tabs, page factories and profile identities
are validated before restoring a saved grid. A failed restore retains the default
workspace and disables writes to that file for the session.

Two fresh processes passed the real FNV/Skyrim round trip: Home, FNV's three
panels, Skyrim's two panels, selected Health Check and independent searches
returned after restart, and saving before visiting Skyrim retained its layout.
Evidence: `/tmp/mo2-layout-save.log`, `/tmp/mo2-layout-restored.log`,
`artifacts/layout-save.png`, `artifacts/layout-restored.png`.
A second write/read pair verified expanded search, Rules, an extra native New Tab
and the selected mods tab: `/tmp/mo2-layout-options-{write,read}.log`,
`artifacts/layout-options-read.png`. Both restored screenshots were inspected.
An intentionally overlapping grid showed the restore error and left its input
file byte-for-byte unchanged after startup/shutdown (`artifacts/layout-invalid.png`).
Build passed with the existing upstream OpenTelemetry advisory.

The saved data uses the native workspace's current-page model; it does not add
cross-restart Back/Forward history or restore live game processes. Diagnostic
reference serialization is implemented, but a restart with a live diagnostic
extension has not yet been exercised.

## Top-bar behavior audit

The live top bar now implements the native interface directly instead of
inheriting the design model's demo username/avatar, login toggle and enabled
no-op commands. The existing Nexus account button still opens MO2 settings.
Welcome, changelog and unsupported account menu actions are disabled rather
than presenting an enabled action with no implementation; forums remain disabled.

The native help menu's **View MO2 logs** command uses a path reported by the
bridge from MO2's QApplication `dataPath` plus its `logs` subdirectory, matching
the original application. It does not infer that path from the executable or
read log contents. The command is disabled without a connected host or existing
log directory, and follows the selected instance. Older bridges without this
optional metadata leave it disabled.

`MO2_VERIFY_TOPBAR=1` passed disconnected → FNV → Skyrim → FNV, verifying the
native menu binding, absence of demo identity, disabled unsupported commands and
correct host log directories. Dolphin processes confirmed both requested log
folders (their selected log files were not opened/read); those test windows
were closed afterward. Combined sidebar launch, native Home history and independent
MO2 data comparisons passed. Evidence: `/tmp/mo2-topbar-runtime.log` and
`artifacts/topbar-mo2.png`. The screenshot was inspected. Bridge checks passed
(16), including absent/custom log-path metadata; build passed with the existing
upstream OpenTelemetry advisory. This change does not implement the disabled
NMA onboarding/changelog features or introduce a second account authority.

## Plugin multi-selection and activation refresh

Actual evdev Ctrl-click input selected two MCM example ESPs in the native plugin
table and clicked Disable selected / Enable selected. The first run exposed
selection loss after the MO2 snapshot refreshed: selected rows were replaced,
and NMA's unconditional list sort reset selection even after preserving row
identity. The provider now updates reactive row components in place and only
publishes list changes for membership/order changes. Plugin-cache replacement
also removes only missing keys. Activation and diagnostic updates retain the
native selection; genuine membership/order changes still update and sort rows.

Activation buttons now reflect whether an editable selected plugin needs that
state, and the command skips fixed plugins and redundant state requests.
`frontend/tools/check_plugin_mouse.py` passed both mouse phases with selection
retained, followed by fixed-only and mixed-selection checks through the native
selection model. It restored every original plugin state/order and checked that
mod activation/priority stayed unchanged. The fixture requires Linux evdev
access and the initialized isolated FNV host; run it from the repository root.

The same run passed native mod disable/restore (plugin removal/addition), fixed
and stale reorder guards, movable-plugin order restoration, all 14 FNV plugin
metadata checks, and Skyrim's 151 mod rows / 150 plugins with independent host
comparisons before returning to FNV. Evidence: `/tmp/mo2-plugin-multi.log` and
`artifacts/plugin-multi.png`. Both real mouse phases completed and the frontend
exited successfully. Build passed with the existing upstream OpenTelemetry
advisory. Multi-row dragging was not exercised by this check.
