# Navigation and Tools follow-up — 2026-09-13

This follow-up supersedes the earlier Rules-tab and per-profile-spine descriptions below.

- One square icon per registered game. It returns to the last selected profile for that game; the native Profiles page is available inside the game workspace.
- Installed contains My Mods and Plugins. Utilities contains Health Check and Tools. Rules is hidden for live MO2 pages and old Rules-tab selections restore to Mods.
- Tools follows the open Vortex Tools page: default launcher, pinned tools, filterable rows and launch buttons. Add / edit opens MO2's original executable settings; extension rows trigger the original enabled tools-menu actions. Pins and their order are presentation preferences per instance.
- The Tools header uses Vortex's actual `assets/pictograms/tools.svg` with its primary colour `#fb923c`; the sidebar/tab uses the same `mdi-wrench-outline` as Vortex. Asset source and GPL licence are retained in `MockHost/Assets/Vortex/`.
- Downloads embeds NMA's actual DownloadsPageView and adapter with an MO2 provider. Native search, selection and transfer controls operate on MO2 archives; the filename, downloaded bytes and status columns fit a half-width panel. Archive import and Nexus-link actions retain profile guards. Unknown transfer totals/speeds are not invented.
- The mod list updates changed records instead of clearing every row on every changed snapshot. Native search combines with enabled, disabled and conflict filters. Conflict rows use green for winning, red for overwritten and amber for mixed/other native reports; the tooltip retains MO2's exact report. Direct priority entry and earlier/later controls call MO2 and retain selection after a move.
- Hidden-host mode disables Qt's automatic quit-on-last-window-close so closing an original extension dialog does not terminate MO2. Explicit original-window close still uses MO2's own shutdown workflow.

Reference: [Vortex](https://github.com/Nexus-Mods/Vortex), local reference commit `089b1c59958e9c9c52d9ba0ae47fbdcfd17ba493`, `src/renderer/src/views/pages/Tools/index.tsx` and `src/renderer/src/views/components/iconMap.ts`. The rest of the shell and panel system continue to use the NMA FNV fork comparison below.

Validation: `MO2_VERIFY_NEW_UI=1` exercises navigation, filter removal/restoration, an actual priority-button move and restoration, original tool enumeration, native download rows and game-scoped Profiles. `MO2_VERIFY_NEW_UI_SWITCH=1` also switches to Skyrim, checks its real conflict rows, and returns to the remembered FNV profile. Captures are `artifacts/new-ui-{mods,tools,downloads,profiles,skyrim-conflicts}.png` at 1280×800. The original INI Editor was separately observed as a mapped `INI Files` window while MO2's main window stayed unmapped, then closed without saving; the frontend remained connected. All 24 FNV profile files matched their pre-check hashes. Bridge contracts: 18 passing; download contracts: 3 passing.

---

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

## Final page comparison — 2026-09-13

The actual fork was reopened at commit `3b0244e` in its isolated XDG profile.
Pointer navigation captured My Games, My Loadouts, the FNV mods workspace,
Health Check and Library at approximately 1280 × 720. No reference Apply,
installation or game launch was requested. Current live pages were then captured
at the same size while visiting FNV → Skyrim → FNV through the actual spine.

| Reference presentation | MO2 mapping and final observation |
| --- | --- |
| My Games portrait tiles and Home navigation | Native MyGamesView shows registered MO2 games with portrait Steam art and View actions. Its description now identifies registered MO2 instances. The empty independent “Other supported games” section is hidden. |
| My Loadouts sections, Create card and profile cards | Native MyLoadoutsView shows every profile from every registered instance, including both Skyrim instances and both FNV profiles. Create/Copy/Rename/Delete map to original MO2 profile operations. The description now says MO2 profiles and contains no Apply instruction. |
| Game spine and contextual sidebar | Square Steam icons select an MO2 profile directly. Home's My Games/My Loadouts leave the game workspace. The game menu contains Downloads, My Mods, Health Check and Plugins. |
| Mods header, tabs, search, toolbar and table | Native LoadoutView supplies these controls. Its rows and actions use MO2 priorities, activation, conflicts and Overwrite. Both games render with mods on the left and plugins on the right. |
| Health Check list and details | Native diagnostic presentation uses refreshed original MO2 reports. Both current games render the zero-report state; populated, unavailable and detail lifetimes retain their earlier runtime evidence below. |
| Library, collection membership, Preview/Apply | MO2's downloads folder replaces Library membership. Installation and transfer controls call MO2. Collections and deployment controls have no independent authority. The Downloads view retains MO2-specific archive/transfer controls inside the native panel system. |
| Panel and tab lifetime | Each profile retains its own native workspace and tabs. The shared Open Downloads action now stays in that profile workspace, matching its game-sidebar counterpart. An unselected startup has no MO2 downloads target. |

`MO2_VERIFY_FINAL_PAGES=1` passed: all registered profiles rendered, both games
connected, paired mod/plugin lists rendered, both native health pages obtained
reports and both download folders rendered without switching to Home. FNV
returned with 12 mod rows and 14 plugins; Skyrim showed 151 mod rows and 150
plugins. Visual inspection confirmed the captures, not just the verifier output.

Evidence: `artifacts/reference/final-{my-games,my-loadouts,fnv-mods,fnv-health,library}.png`,
`artifacts/final-{my-games,my-loadouts}.png`,
`artifacts/final-{fnv,skyrim}{mods,health,downloads}.png`,
`artifacts/final-fnv-returnmods.png`, `artifacts/final-pages-return.png` and
`/tmp/mo2-final-pages-fixed.log`. The game images are original local Steam assets.
Native mods/health/panel/divider/workspace sources match the fork byte-for-byte;
the two Home AXAML files differ only in unused XML namespace declarations, and
their code-behind files match. No pinned upstream source was modified.

The updated download-context verifier also passed after the navigation change: the
old picker retains its original target while the new profile owns a different
downloads page. Unavailable rows are hidden and both profile snapshots restore
unchanged (`/tmp/mo2-final-download-context-fixed.log`).

The sections below preserve the sequence of earlier findings and corrections.
Statements such as “missing” or “remaining” there describe that historical stage;
the final mappings above and the acceptance audit describe the current build.

## Original comparison findings (subsequently corrected)

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

## Two-plugin pointer dragging

`MO2_VERIFY_PLUGIN_DRAG=1 python3 frontend/tools/check_plugin_mouse.py` now
exercises real Ctrl-click selection and pointer dragging through the native NMA
table. In the isolated FNV Frontend Test profile, it selects two adjacent MCM
example ESPs, drags them after MCM, then selects them again and drags them back
before the remaining example ESP. The native drag-start event reported two rows
in each gesture. Both resulting full orders matched an independent bridge
snapshot; no command-based move substitutes for either gesture.

The first attempts started the two-row drag but missed its destination: the
last row's bottom edge was clipped by the horizontal scrollbar. The check now
waits for selection and scrolling to settle and drops inside the visible half
of the destination row. No product reorder change was needed.

The successful run restored all plugin priorities and activation states, and
preserved all mod priorities and activation states. Selection clears after the
actual order changes, unlike the state-only activation updates described above.
The verifier also has a finally-path restoration if a gesture check fails.
Evidence: `/tmp/mo2-plugin-drag.log`,
[after dragging down](artifacts/plugin-drag-down.png),
[after dragging back](artifacts/plugin-drag-up.png), and
[final panels](artifacts/plugin-drag.png). The helper and frontend exited with
code 0. Build passed with the existing upstream OpenTelemetry warning.
This check covers two adjacent ESPs in ascending display order; it does not
establish non-adjacent selection, descending-order dragging or panel dragging.

## Native panel resizing and plugin details

The panel, workspace and divider implementations match the corresponding files
in the FNV reference fork byte for byte. Its tab-header implementation selects
and closes tabs; it does not implement dragging tabs between panels. The native
panel drag interaction is divider resizing.

`MO2_VERIFY_PANEL_DRAG=1 python3 frontend/tools/check_plugin_mouse.py` exercises
real pointer gestures on both divider orientations. It widens and restores the
mod panel, adds a third panel through NMA's native command, expands its horizontal
divider, switches to Skyrim and back, then restores the divider and closes the
temporary panel. FNV keeps its workspace ID, panel bounds and original tabs
across the game switch. The check verifies full panel coverage, unchanged MO2
mod/plugin state and restoration of the normal two-panel arrangement.

The first screenshots exposed a fixed 150-pixel plugin-details region consuming
the shortened plugin table. Details now size to their content, reserve space for
the native explanation banner, column header and a complete row, and use at most
one quarter of the panel height (capped at 150 pixels). With nothing selected,
the details region collapses. A regression check requires room for a full row
after resizing, including the expanded short panel with two plugins selected.

The selection pointer helper now measures each row after the previous click has
settled; changing the details region can move rows. The combined activation,
two-plugin dragging, divider resizing and plugin-metadata checks run with:

```sh
MO2_VERIFY_PLUGIN_MULTI=1 MO2_VERIFY_PLUGIN_DRAG=1 MO2_VERIFY_PANEL_DRAG=1 \
MO2_VERIFY_PLUGIN_DETAILS=1 python3 frontend/tools/check_plugin_mouse.py
```

Evidence: `/tmp/mo2-panel-drag.log`,
[wider mod panel](artifacts/panel-width-expand.png),
[short plugin panel with selected details](artifacts/panel-height-expand.png),
[restored height](artifacts/panel-height-restore.png), and
[final two panels](artifacts/panel-drag.png).
The combined run and frontend exited with code 0; all 14 plugin metadata checks
passed. Build passed with the existing upstream OpenTelemetry warning.

## Diagnostic details across a frontend restart

Two fresh frontend processes passed the native diagnostic restart check. The
first opened Health Check through NMA navigation and opened the temporary MO2
diagnostic extension's entry in a third panel. It saved the layout while the
report contained `phase-one-live-report`. Before the second process started,
the extension's marker changed to `phase-two-live-report`.

The second process started disconnected at Home. Selecting FNV restored the
three-panel workspace and existing diagnostic details tab without navigating to
or opening another report. The tab displayed the new native report. Removing
the marker cleared the report in the restored tab; recreating it refreshed the
tab again. The layout contains the diagnostic title and source identity, but
neither report body. No cached report text substitutes for MO2's result.

`MO2_VERIFY_HEALTH_RESTART=write` and `=read` exercise this with an explicit
`MO2_FRONTEND_LAYOUT` path under `frontend/artifacts` and `MO2_SCREENSHOT`.
The temporary original-API extension is
[`tools/fixtures/health_restart.py`](tools/fixtures/health_restart.py).
With the isolated FNV host stopped, copy it to that host's plugins directory as
`frontend_health_restart.py`, and create
`plugins/data/frontend-health-restart.txt` with the appropriate phase text.
Use a fresh layout file for the write run, retain it for read, and change the
marker only after the write process exits. After testing, stop the host and
remove the installed extension and marker; leave the tracked fixture source.

Both processes exited with code 0. Screenshots were inspected:
[before restart](artifacts/health-restart-write.png),
[after restart](artifacts/health-restart-read.png).
Logs: `/tmp/mo2-health-restart-write.log` and
`/tmp/mo2-health-restart-read.log`. Build passed. The installed fixture and marker
were removed after stopping the host; `modlist.txt`, `plugins.txt` and
`loadorder.txt` retained their original hashes across the full check.


## Overwrite, Logs, navigation and icon follow-up

The game workspace has Mods, Plugins and Overwrite under Installed, with Tools,
Health Check and Logs under Utilities. Home-only page choices are excluded from
game workspaces; restored Home pages in a game layout are replaced with that
game's Profiles page. Profiles labels omit internal instance folder names.
Connections replaces the old MO2 instances heading. The native NMA top bar is
retained without a second OS title bar; Home uses the NMA logo and Downloads uses
the download-arrow icon.

Opening an MO2 dialog no longer disables frontend panel/tab navigation. A stopped
native host is detected while awaiting a bridge response, releasing the busy
state instead of waiting for the full dialog timeout. Unknown command outcomes
remain explicit and are never automatically retried.

Overwrite lists generated paths/sizes through MO2's overwritePath API. Create,
move, sync and clear invoke the original MO2 context-menu actions, retaining
native prompts and conflict handling. Open files invokes MO2's existing Overwrite
dialog. These actions cover the whole folder, as stated in the page. Listing does
not follow symlinks or read file contents. The original Create Mod, Move to Mod and Clear Overwrite prompts were
opened and cancelled with an isolated marker; the marker was retained and then
removed by each check. This is prompt/routing verification, not a claim that all
move, sync and clear outcomes were exercised.

Logs displays the native instance's log files with live tailing, file selection,
severity and text filters. It bounds reads to 256 KiB / 2,000 lines and hides
account headers and URL query strings. Logs remain inside the game workspace.

Tools use the original executable and extension QAction icons. Pinned tools also
appear directly above the native Play button and retain the MO2 launch route.
Base-game/DLC thumbnails use the installed game's icon in both lists, with a
complete proportional foreground over a blurred fill. Both use the same cached
46×26 composition; the foreground is not cropped or stretched. The panel and
sidebar use the label Mods.

`MO2_VERIFY_WORKSPACE_INPUT=1` exercises actual pointer selection/navigation and
plugin activation with restoration, scoped Profiles, tool pins, Overwrite and
Logs discovery, stopped-host detection and FNV/Skyrim switching. It requires the
existing isolated FNV test profiles and the pointer helper under artifacts.
`tools/check_bridge.py` now includes a generated-file symlink boundary test.

The full pointer check passed on 2026-09-13, including Logs discovery, stopped-host
detection, separated page choices and FNV → Skyrim → FNV switching. Plugin state,
original profile selection and existing tool-pin preferences were restored.
Evidence: `/tmp/mo2-workspace-input-check.log`,
`artifacts/workspace-input-check.png`, and the three
`/tmp/mo2-overwrite-*-check.log` prompt checks. Build passed; 19 bridge contracts,
3 download contracts and the profile reader check passed. The pinned upstream
source remains unchanged.


The subsequent thumbnail correction uses Skia to bake the background blur into
pixels; the unattached Avalonia effect path did not reliably render blur.
Foreground rendering preserves the source aspect ratio inside a square, and the
Plugins image uses Uniform rather than the original crop-to-fill style. Both lists
use the same 46×26 bitmap. Logs and Overwrite header SVGs use NMA's orange and
white/lavender palette; the original gradient Vortex Tools pictogram is retained.
The full pointer check passed again in `/tmp/mo2-blur-icons-check.log`; the visible
blur and the Logs/Overwrite header pictograms were inspected in the running app.

## Selection, artwork and Steam Deck follow-up (2026-09-13)

- Selected mods highlight their plugin files in purple and the conflicting file
  providers in green/red. The bridge reads MO2's native conflict models invisibly
  and resolves exact file origins; it does not scan or modify the game directory.
  Selection changes are debounced, stale responses are discarded, and download
  progress does not repeatedly rebuild conflict details. Pointer checks covered
  switching directly between two selected mods, both conflict directions and
  MCM's one-plugin / author-examples three-plugin ownership.
- Create separator invokes MO2's original name dialog, then its priority API.
  Native create/remove round-trip checks preserved all existing priorities. MO2's
  non-toggleable separator state is separate from permission to move/remove it.
  Clearing native selection before removal prevents a delayed marker from
  retaining a deleted mod index. Verification content was removed afterwards.
- Overwrite remains available through its own page and no longer appears in the
  Mods table or its item count.
- Both tables now compose the complete game **cover art**, fitted proportionally
  over its blurred background, in matching 46×26 thumbnails. This supersedes the
  earlier game-icon thumbnail request. Installed mod artwork uses its Nexus ID
  and a local public-image cache. MCM's official image was cached with
  `tools/cache_mod_thumbnail.py`; its mod and ESP rows show the same artwork.
  Automatic artwork acquisition for every newly installed mod is not implemented.
- Home uses Vortex's `mdiHome` symbol and rounded-square button shape, based on
  its local `SpineButton.tsx`/`Spine/index.tsx`. The frontend's separate taskbar
  icon is embedded and installed with `tools/install_desktop.py`; X11 reports
  `WM_CLASS=mo2-nexus-frontend` and a populated `_NET_WM_ICON`.
- Plugins uses the native sorting editor with a flat row source, compact 40px
  rows and proportional name columns. Up/down controls and the decorative trophy
  rail are removed; drag handling remains attached to the original adapter.
  The list retains one real scrollbar, ellipsized names and full-name tooltips.
  Both lists were inspected side by side in a 1280×750 Steam Deck work area.
- Archives is an Installed page with native archive discovery (21 FNV archives,
  125 Skyrim archives), filtering and the original BSA/BA2 Preview extension.
  FNV's `Fallout - Misc.bsa` opened in the original preview with 142 entries while
  MO2's main window remained hidden. Closing it returned control to the bridge.
  An initial automated navigation test used an ineffective uinput pointer;
  direct X11 pointer navigation and the live Archives list subsequently passed.

Validation: frontend build, 19 bridge contracts and four download contracts pass.
Native separator cleanup and both conflict directions passed independently of
frontend rendering. Shared popup login and complete MO2 feature parity remain
open in the active integration goal.
