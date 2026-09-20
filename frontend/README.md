> **Current target:** integrate the frontend with MO2-owned profiles, mods,
> plugins and downloads. Earlier fixture sections below describe scaffolding,
> not completed integration. See [MO2 integration and FNV acceptance](MO2_INTEGRATION.md).
> Proton is acceptable; existing MO2 extensions must retain their APIs.

# Alternate Nexus frontend (in progress)

Start with the [current handover](HANDOVER.md) and
[production readiness checkpoint](PRODUCTION_READINESS.md). Saves is deferred.

Native action errors retain the last known connected state and show the returned
message. Failed snapshots, invalid response identities, timeouts and host I/O
failures still invalidate the connection. Normal polling observes any changes
after a rejected action; the frontend does not automatically retry it.

Data keeps the selected file or folder through search and refresh, restoring it
when a search no longer hides it. Navigation and profile changes clear that
selection. Native Data action errors stay in the panel until explicit Refresh,
another action, navigation or a profile change.

Enter on a selected Data file invokes MO2's native activation, like a double-click:
MO2's preview preference and installed file handlers decide what opens. Preview
menu availability also comes from the native host.

Saves supports multiple selection and MO2's native bulk-delete confirmation.
Details and repair require one selected save. Selection survives filtering and
reordered refreshes by filename; actions use only visible selected rows. Right-click
preserves an existing selected group or selects the pointed-at unselected row alone.
Delete in the focused save table invokes the same native confirmation.
Visible Saves panels check the native host every three seconds. Unchanged reads
retain the table, scroll position and selection; hidden or detached panels pause
polling. Background reads coalesce and retain access to existing save actions.
Save folders on Wine drives that Linux cannot resolve use MO2's own Explorer action.
See [Saves validation](SAVES_VALIDATION.md) for deletion/cancellation evidence and
the outstanding desktop-keyboard check.

Category filters use all assigned names from MO2's native grouping role, keeping
secondary categories and names containing commas intact. The Category column still
shows MO2's primary category. An older bridge supplies only that primary category;
restart MO2 with the updated bridge to expose full membership. The category list
preserves the host's reported hierarchy, supports expansion, and matches descendant
assignments for selected parents. Category controls cycle through include, exclude
and inactive; right-click or Shift+Space reverses the cycle. Exclusions participate
in And/Or and appear in the filter summary. Native special criteria remain incomplete. See [category validation](CATEGORY_FILTER_VALIDATION.md).

Logs scans and reads run off the UI thread. Closing or reopening the panel,
changing the selected log, or switching profiles prevents an older read from
publishing its text or error. A file selection made during a read is processed
as soon as that read finishes. The retained tail omits its first partial line
before applying credential redaction.

Each Data tab also saves its folder, search text and conflicts-only filter with
its profile's panel layout. Those controls restore after restarting or switching
between games; file selection and action errors are transient.

NMA help dismissals are saved in `mo2-nexus-frontend/alerts.json` under the XDG
configuration directory. The Plugins question-mark button can reopen the same
help. Only alert preferences are persisted by this settings adapter.

Recurring instance/profile discovery reads run off the UI thread and coalesce
overlapping refresh requests. Account checks and login use registered endpoints
directly, without rescanning profile and download files. Initial discovery still
runs during startup before the interactive window is created.

My Mods → overflow menu provides mod-list backup and restore. The Plugins
backup/history button provides plugin-order backup and restore. These invoke
MO2's original controls, including its timestamped backups, retention policy and
restore picker. Restoring mod activation and restoring plugin activation/order
remain separate operations, matching MO2.

Plugin actions include MO2's Lock/Unlock load order. Locked plugins show a
padlock; MO2 remains responsible for lock behavior during ordering and sorting.
Plugin diagnostics open from the info button and have a close control, so
ordinary selection and activation do not resize the plugin list.

Mods rows refresh activation, available actions, labels and metadata when the
native profile changes. Action callbacks retain their original profile target.

Linux Avalonia frontend for Mod Organizer. `run.sh` opens NMA’s My Games with your detected MO2 games; it starts disconnected until you select a profile. `run-live.sh /path/to/mo2/plugins/data/frontend-bridge` connects to a
running MO2 host with the [bridge extension](mo2-plugin/README.md) installed.
Live mode shows the active MO2 profile's mods on the left and plugins on the
right, and sends mod activation, mod priority, plugin activation and plugin ordering changes through MO2.
Ctrl-click selects multiple plugins. Enable/Disable selected retains that
selection across activation updates, skips fixed plugins and leaves mod
activation independent. Buttons are disabled when no selected plugin needs
the requested change.
The Downloads tab reads MO2's downloads folder, requests Nexus files through
MO2's logged-in downloader, and installs archives through its existing installers.
Downloads identifies the connected game and profile. Archive selection retains
that instance/profile target; if it changes while the picker is open, choose the
archive again. Install and transfer actions disable while MO2 is unavailable or busy.
The sidebar, game widgets, loadout cards, top bar and panel system use the original
NMA views. My Games → View → profile switches the connected game. My Loadouts
shows real profiles grouped by game across registered MO2 instances. Each
instance has a native Create new loadout card that opens MO2’s Create Profile
dialog. Selection uses each running host.
Each MO2 instance/profile pair has its own native workspace. Switching away and
back restores its open panels, tabs and mod search.
A newly visited profile starts with mods on the left and plugins on the right.
The shared tab strip scrolls the selected tab into view after navigation or
resizing, keeping the page title visible when its header is compacted. Its arrow
buttons still allow browsing other tabs without changing the selection.
Data and External Files share responsive metadata fitting: optional columns give
way to filenames in narrow panels and return when space permits. Routine layout
passes preserve manual column sizing. Their tree Name column remains mandatory,
including when older saved column preferences are loaded.
Panel layouts, selected tabs, searches and the Mods/Rules choice are saved across
frontend restarts in the frontend configuration folder’s `workspace-layout.json`.
Native panel dividers resize the live pages. Plugin details use available space
and collapse when nothing is selected, keeping rows visible in shorter panels.
Startup restores Home; selecting an MO2 profile restores that profile’s layout.
`MO2_FRONTEND_LAYOUT` overrides the layout file for isolated testing. An unreadable
or unsupported layout is left intact and its error appears in the status bar.
**Create Copy**, **Rename** and **Delete** on each card open MO2’s original
prompts and confirmation. The active profile cannot be renamed or deleted. Cards refresh when MO2
profile files change, including changes made in the original interface.
The native help menu’s **View MO2 logs** action opens the connected host’s log
folder through the Linux desktop. Its path comes from the bridge. Unsupported
NMA welcome/changelog/account-menu actions are disabled; the **Nexus account**
button continues to open MO2’s own account settings.
The native **PLAY** button and MO2 executable picker sit at the bottom of the
profile sidebar. Launch is disabled while MO2 is busy, disconnected, or no valid
executable is selected. The native top bar shows the active profile name.
Profile selection starts a configured instance launcher when MO2 is stopped.
Pause, resume and cancel route through MO2’s existing download controls.
Downloads opened for a selected profile stays in that profile’s workspace; Home
pages describe MO2 registrations and profiles rather than NMA deployment.
FNV gameplay, the MCM menu, enabled/disabled runs and all 14 plugin indices are
verified; see [in-game evidence](FNV_ACCEPTANCE.md). The mod panel includes MO2’s Overwrite and conflict/status messages.
**View files** opens its original conflict/file dialog. The mod panel uses NMA's
native header, Mods/Rules tabs, search, selection toolbar and row menus with
MO2-backed actions. Selecting plugins
shows MO2’s diagnostics and hexadecimal mod indices; native warnings are marked in plugin rows. Activation and row movement
controls reflect MO2’s restrictions. The shared drop path rejects fixed plugins;
reordering refreshes MO2 first and rejects stale plugin state.
The **Nexus account** button opens the selected MO2 instance’s original Nexus
settings tab. Select a profile first when disconnected. Account handling stays
inside MO2.
**Health Check** uses NMA's native diagnostic list and details pages. It reads
MO2's Notifications result, including enabled diagnostic extensions, without
showing that dialog. MO2 notifications appear as warnings because its diagnostic
API does not supply severity. Checks refresh on MO2 changes and periodically;
an unavailable host produces an error state. FNV, Skyrim, a temporary diagnostic
extension, and game switching were tested; see [fidelity evidence](NMA_FIDELITY.md).
Live desktop actions use NMA’s native platform service. The help menu’s
GitHub, MO2 Discord and Nexus status links open through the default handler.
Fixture scenarios are available only with `run-fixtures.sh` (or `MO2_FIXTURES=1`).

## Run

Install the .NET 9 SDK, then from the repository root:

```sh
git submodule update --init frontend/upstream
./frontend/run.sh
```

The launcher also recognizes a repository-local SDK at `.tools/dotnet`.
It builds and runs Release by default; use `MO2_BUILD_CONFIGURATION=Debug`
for a debugging session. After building Release with the local SDK, install
the desktop shortcut with `python3 frontend/tools/install_desktop.py`.
The installer also accepts `--configuration Debug` and checks that the selected
binary exists before changing the shortcut.

For a framework-dependent precompiled Linux build, publish with the local SDK:

```sh
DOTNET_PROCESSOR_COUNT=2 .tools/dotnet/dotnet publish frontend/MockHost/MockHost.csproj -p:PublishProfile=LinuxReadyToRun -m:1 -o frontend/artifacts/linux-ready-to-run
```

After validating that output, point the desktop shortcut at it with
`python3 frontend/tools/install_desktop.py --app frontend/artifacts/linux-ready-to-run/MockHost.dll`.
The output directory must remain in place. Running the installer without `--app`
restores the development Release build target. Startup measurements and remaining
validation limits are recorded in [STARTUP_PUBLISH_VALIDATION.md](STARTUP_PUBLISH_VALIDATION.md).

Use `frontend/tools/check_startup.py` to validate a published build against a
running host and a paired Mods/Plugins layout. Supply `--app`, `--bridge`,
`--layout` and a new `--output` directory. It runs three isolated launches by
default, preserves every verdict/screenshot, and exits unsuccessfully if any
run fails or lacks a startup verdict. It never changes the supplied layout.

Developer diagnostics are opt-in with `MO2_DEVELOPER_TOOLS=1`. Debug uses the
pinned fork's Avalonia inspector (F12); Release uses DiagnosticsSupport for the
external DevTools connection. The two packages are selected by configuration
because they cannot coexist. Leave the variable unset for a normal session.

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
It starts the GUI executable directly, avoiding a command window, and sets
`MO2_FRONTEND_HOST=1`. In that mode the bridge hides MO2’s main window, splash
and informational notification toasts before they appear. Explicit installer,
tool and actionable error dialogs remain available. Launching MO2 normally
without that variable retains its original UI.
With a profile connected, **MO2 instances → Show original MO2** reveals that
same running host. **Hide original MO2** hides it again. Both interfaces use
the same selected profile and MO2 models; opening another executable is not
necessary. Closing the original MO2 window exits the host normally.
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

`frontend/tools/verify.sh` runs `MO2_VERIFY_*` checks against a live MO2 host, one
per run, and says what each of them said:

```sh
frontend/tools/paired_layout.py --endpoint mo2-fnv-host
frontend/tools/verify.sh /path/to/mo2/plugins/data/frontend-bridge QT_WIDGETS QT_ACTIONS PRESET_FIT
```

It reports a check that printed no verdict as **NO VERDICT** and fails the run.
That is not a formality: `MO2_SCREENSHOT` is what ends a run, and it waits only
for the checks that register a turn with `Mo2CheckTurn`, so a check that does not
register is shut down part-way and prints nothing — which in a summary that looks
for the word `FAIL` is indistinguishable from a clean run. The runner gives those
checks no screenshot and ends them itself once they have printed and gone quiet.

Checks that need the mod and plugin lists side by side in an isolated layout are
given one built by `tools/paired_layout.py` and rebuilt for every run, so one
check's navigation cannot decide what the next one sees. `MO2_VERIFY_ICON_ALIASES`
is given the icon styles as upstream shipped them, recovered from the submodule's
own history.

`MO2_VERIFY_QT_WIDGETS` and `MO2_VERIFY_QT_ACTIONS` take their expectations from
`src/mainwindow.ui` at run time — all 73 widgets and all 24 actions MO2 declares —
rather than from a list written down beside them. Each accounts for its side both
ways round: something MO2 has that nothing answers for fails, and an answer for
something MO2 has removed fails too.

Build and run a small workspace integration check, saving a native screenshot:

```sh
MO2_VERIFY_WORKSPACE=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/four-panels.png" ./frontend/run-fixtures.sh
```

Create `frontend/artifacts` first. The window closes after capturing. The check
exercises add/select/close tabs, adding panels up to four, and closing/restoring
a panel. It is a partial integration check, not proof of full behavioral parity.

## Verification and limits

The live frontend uses the native NMA navigation and panels with real MO2
games, profiles, mod/plugin lists, downloads and original dialogs. FNV in-game
acceptance and switching between the registered FNV and Skyrim hosts pass.
The [current acceptance audit](ACCEPTANCE_AUDIT.md) maps the requirements to
implementation and runtime evidence. Original extension enable/requirement rules
and the final page comparison against the FNV-enabled reference fork also pass.
The audit distinguishes broader coverage limits and unverified remote publication
from the completed local integration.

MO2 remains the owner of installed mods and profiles; the live frontend does
not maintain an independent NMA Library or deployment store.

## New Vegas scenarios and reference fork

`MO2_VERIFY_SCENARIOS=1 MO2_SCREENSHOT="$PWD/frontend/artifacts/fnv-loadouts.png" ./frontend/run-fixtures.sh`
checks fake game add/remove, loadout clone/delete, sidebar navigation and history.
The installation path supplied in this session is shown in the Steam tooltip;
`MO2_SCENARIO_GAME_PATH` and `MO2_SCENARIO_COVER` override local fixture metadata.
The fixture currently starts with one detected New Vegas game and no loadouts.

The separate [FNV-enabled Nexus app reference fork](reference/README.md) is the
reference for comparing New Vegas screens against NMA. The live frontend uses
MO2's backend; the reference app retains NMA's backend and isolated app data.

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


## NXM protocol routing

The frontend accepts `--nxm 'nxm://game/mods/id/files/id?...'` and routes it to the
registered native MO2 host for that game. Selecting an instance/profile in the
frontend remembers that game's preferred instance. Ambiguous or stale routes
fail instead of silently choosing a different instance. Only read-only startup
snapshots are retried; a download handoff is not retried on an unknown outcome.

The full signed NXM query is preserved, including free-account authorization
parameters. MO2's own secondary-process forwarding path handles the URL; ordinary
HTTPS file-ID links retain MO2's existing ID-based downloader. Authorization data
is not stored in routing preferences, and bridge request files are private to
the user on Linux.

Install the Linux desktop handler:

```sh
python3 frontend/tools/install_nxm_handler.py \
  --dotnet .tools/dotnet/dotnet \
  --app frontend/MockHost/bin/Release/net9.0/MockHost.dll
```

Restore the prior handler with `python3 frontend/tools/install_nxm_handler.py
--restore` (one command). The installer preserves the prior handler and validates
the resulting default. `--check-nxm` on MockHost tests signed-link preservation,
malformed links and instance-route boundaries without network access.

On September 13, both direct CLI and `xdg-open` handoffs for the already-downloaded
MCM Guide reached MO2's native duplicate-download prompt, cancelled without another
transfer. A real free-account signed transfer remains outside this runtime check.


## Traditional installed lists (September 2026)

My Mods uses native inline MO2 separators again. Legacy collection tabs restore as
My Mods. Both installed lists have left-side activation switches and omit numeric
priority/order columns. The My Mods toolbar retains search and selection actions;
its left overflow menu contains archive installation, separator creation, filters,
and relative priority movement. Plugins has an NMA page header and a left toolbar
overflow menu. Its originating mod is shown in the thumbnail tooltip.

Both lists use the same centred scrollbar thumb, expanding in place on hover.
The plugin scrollbar stays in the help/trophy rail and replaces the inner bar.
Plugins uses Vortex's icon; Archives uses its archive icon in its header.

Validation: incremental MockHost build succeeded (one existing OpenTelemetry
advisory). `MO2_VERIFY_TRADITIONAL_UI=1` on the isolated FNV Frontend Test profile
verified inline separator collapse/expand, mod and plugin activation through their
actual click handlers with native readback and restoration, toolbar overflow, and
trophy-rail scrolling at reduced height. Results are in
`artifacts/traditional-ui-final.log`. The temporary separator was removed through
MO2; existing mod/plugin activation was preserved. Screens were inspected at
1280×750. This UI check requires its explicitly named temporary separator fixture.

Fresh NXM downloads now pass for both FNV and Skyrim through the installed desktop
handler with the existing Premium account. Both completed archives match official
file sizes, pass 7z integrity checks, and leave native hosts responsive and mod/plugin
state unchanged. Evidence: `artifacts/nxm-live-two-game-downloads.json`. Earlier
host exits remain unexplained; these fresh trials did not reproduce them. A live
signed free-account download and duplicate-download dialog handling remain separate
verification gaps.


Header artwork correction: Plugins now uses Vortex's original layered
`puzzle-piece.svg` pictogram, replacing its small plus-circle symbol. Archives has
an original vector box pictogram with NMA's orange and white/blue gradient palette,
200px canvas, and matching inset. Build passed and the Archives header was inspected
in the running app (`artifacts/archives-header-3d.png`).

Plugins navigation correction: inspected the installed Vortex iconMap and confirmed
`plugins: mdiPowerPlugOutline`. Sidebar and panel tabs now use that monochrome plug
outline; the layered puzzle artwork is reserved for the page header.

Plugins header now uses an original layered power-plug vector (`Assets/power-plug-3d.svg`), preserving Vortex’s navigation silhouette and using the orange/white/blue header palette. Rendered successfully in an isolated Plugins panel. Restoring an Overwrite+Plugins two-tab test panel exposed an infinite-layout crash before the Plugins view’s first layout; the added test tab was removed. This specific restoration crash is now fixed; see the tab-strip regression notes below.


### Restored tab-strip stability

The upstream panel view now waits for a nonzero tab-strip viewport before deciding
whether to move its Add button outside the scroll area. Previously, an unsized
restored panel alternated its content extent between 0 and 32 pixels, repeatedly
toggling button visibility until Avalonia aborted with an infinite-layout error.
The zero-width guard preserves normal overflow handling once the panel is sized.

`MO2_VERIFY_TAB_RESTORE=1` requires an isolated `MO2_FRONTEND_LAYOUT` containing
Overwrite and Plugins tabs. It verifies restoration, selected-header state, and
repeated selection at window widths 1280, 900, and 640. The formerly crashing
layout passed (`artifacts/tab-layout-fixed.log`); its rendered two-tab panel is
captured in `artifacts/tab-layout-fixed.png`. Both the upstream UI dependency and
MockHost rebuilt successfully with existing warnings. Preserve the upstream
submodule change alongside its account-icon and tab-history edits.


### Installed list interactions and responsive headers

My Mods and Plugins use the Rules tab’s small drag grip before each row’s activation switch, visible on hover or selection. Row actions open on right-click. Mod dragging
uses a native-priority planner that preserves selected rows' relative order and
rejects stale profile/list state. Separator creation uses an owned frontend modal.
Game thumbnails are fitted on a plain background; narrow lists hide thumbnails.

Descriptions remain visible at normal two-panel Steam Deck widths. Headers shrink progressively over 160px of scrolling (four 40px lines): the description fades and contracts while the icon reduces from 60px to 28px and the title from 24px to 18px. Scrolling back expands the header. Widths
below 360px or heights below 400px also use the compact header. Below 260px wide or
300px tall, the header is hidden and the tab bar stays visible even with one tab.
Viewport changes caused by header collapse do not themselves reset scroll state.

Restored panel view models are now arranged using the existing workspace size:
otherwise a same-size restore left newly created panels with zero actual bounds.

The running FNV NMA fork’s Rules tab was checked with a real pointer gesture.
`artifacts/reference/rules-grip-hover.png` and `rules-grip-drag.png` capture its
10px `DragVerticalDots` grip and insertion line. The frontend uses a plain grip
Border so TreeDataGrid receives the press/drag, rather than a flyout Button that
captures it. Existing row actions are available through the row context menu.


September 13 installed-interaction validation: `MO2_VERIFY_INSTALLED_INTERACTIONS=1`
passes against the isolated FNV Frontend Test profile and a separate layout. It
checks separator modal ownership/cancel/create, multi-row native ordering through
the real drop handlers, context menus, a partially collapsed description after
40px of scrolling, full collapse after multiple lines, restoration, narrow-list
thumbnails, and visible tab bars when headers are hidden. Evidence:
`frontend/artifacts/installed-interactions-responsive.log` (paths are repository-relative).

Real UInput pointer drags from the left grips also passed for both lists: MCM Author
Examples moved before The Mod Configuration Menu; MercenaryPack.esm moved before
TribalPack.esm. Native priorities changed and were restored. Complete mod and plugin
records matched their pre-gesture snapshots afterward. Screenshots:
`frontend/artifacts/mods-grip-drag.png` and `frontend/artifacts/plugins-grip-drag.png`;
summary: `frontend/artifacts/physical-grip-check.json`. The pure mod-order planner
passes 7,172 selection/target cases. No game launch was part of these UI checks.


### Mods table redesign from the supplied screenshots

The latest design supersedes the earlier switch-and-thumbnail Mods rows. It uses
plain activation checkboxes, full-width rounded separator bars with member counts,
compact icon toolbar groups, and right-side removal/action controls. Status dropdowns
were removed at the user's request. Name, version, category and endorsement filters
operate on MO2's current data; the secondary columns disappear progressively in
narrow panels. Versions/categories come from the native model; endorsement uses
IModList's STATE_ENDORSED flag. No update or endorsement state is fabricated.

The header contracts over four 40px scroll steps. Visible descriptions keep their
full height until they have faded away, preventing clipped lines. Hidden headers
retain their panel tab bar. Plugins no longer has a toolbar overflow button. The
original native LOOT workflow is available directly when the host enables sorting;
its newly added dispatch still needs end-to-end native sorting verification.

Native metadata sources:
- https://raw.githubusercontent.com/ModOrganizer2/modorganizer/v2.5.2/src/modlist.h
- https://raw.githubusercontent.com/ModOrganizer2/modorganizer-uibase/master/include/uibase/imodlist.h


Latest user corrections supersede the column-filter design above. Both Mods and
Plugins now use the same plain activation checkboxes, grip and right-side action
styling. The filter row and separate filter button are removed. Each toolbar uses
NMA's expandable SearchControl and wraps groups when space is limited. Plugin search
text/expanded state persist with its tab. Secondary metadata columns collapse in
narrow panels; plugin origins remain tooltips rather than a separate mod-name column.

`artifacts/paired-redesign-interactions.log` verifies the matching live interfaces:
search/clear in each list, native checkbox changes and restoration, multiple-row
native priority changes/restoration, separator modal Create/Cancel, grips and context
actions. The final run preserved the current three-panel layout rather than resizing
it. A prior dedicated resize run verified the four-line fade and hidden-header tab
bars. Real pointer checks also activated a mod using the new checkbox and collapsed
the temporary Interface separator; all complete native mod/plugin records matched
the independent baseline after cleanup. Both temporary separators were removed.
Archives now keeps search, Browse, Extract and Refresh on one compact toolbar row.
Short panels use smaller content margins; narrow panels give the archive filename
the space otherwise used by the Mod column, retaining ownership and loaded state
in the filename tooltip. This leaves selectable archive rows in the existing
Steam Deck three-panel layout. Physical-pointer checks covered search, row
selection, the original 142-file Fallout Misc BSA preview, extraction destination
dialog cancellation, and return to the complete 21-archive list. Evidence:
`artifacts/archives-compact-live.png` and `artifacts/archives-compact-preview.png`.
Account status now distinguishes an unavailable MO2 host from a confirmed logout.
When a native tool or archive dialog delays account checks, the frontend retains
the last confirmed badge only for the same instance set and matching known user
identity. The account tooltip states that current status is unconfirmed. A known
logout, different account, or changed instance set clears the cached display.
`--check-account-monitor` covers these cases alongside the existing refresh/login
race checks. A live FNV archive preview remained open across the account refresh
interval with the Premium badge retained (`artifacts/account-busy-retained.png`).
Tools includes **MO2 → Settings**, using the original Settings action and icon.
This opens native extension and game settings without showing the MO2 main
window. The action uses the existing guarded tool dispatch and can be pinned like
other tools. Physical FNV frontend input opened Settings and cancelled it without
changing mod/plugin state or FNV's sorting preference. All three running hosts
expose the action after deployment; both Skyrim profiles, priorities and activation
states were verified unchanged after their host restart.
Tools now fits a complete tool row in the Steam Deck lower split panel, using
compact toolbar actions, reduced short-panel margins, and no empty status row.
The host reconnect logic refreshes extension tools on connection transitions,
including transitions during an outstanding read. A controlled 22-second pause
of the same FNV host removed the filtered Settings row while disconnected and
restored it automatically on recovery, preserving the search. Physical Add/Edit
opened MO2's original executable editor; Cancel preserved executables and complete
mod/plugin records. Evidence: `artifacts/tools-settings-compact.png`,
`artifacts/tools-disconnected.png`, and `artifacts/tools-reconnected.png`.

Hidden hosts set the native NXM helper's “No, don't ask again” preference at
startup, so they do not compete with the frontend's desktop link handler.
Existing handler mappings are preserved. The bridge uses the helper's portable
or global settings location and leaves migration to `downloadhandler.ini` intact.
Ordinary visible MO2 launches do not apply this bridge startup preference.

Data is available under Installed and through the game workspace's panel picker.
It browses MO2's merged directory one folder at a time, with folder navigation,
search by filename or winning mod, and a conflicts-only option. File tooltips
show other providers and the winning archive when MO2 reports one. This uses
the native extension API, preserving MO2's resolution rules. Open or double-click
a file to use MO2's installed preview extensions. Preview rechecks the displayed
source before dispatch and restores the original native tree selection, filters,
and expanded folders afterward. The visibility button invokes native Hide or
Un-Hide for loose files; hidden entries retain their `.mohidden` suffix in the
list. MO2 handles rename collisions. Files inside archives cannot be hidden
individually. The source-folder button opens the winning loose file’s folder in the desktop file manager on Linux, or invokes MO2’s original Reveal in Explorer action on Windows. Archive members cannot be revealed individually.

Selecting a Data file highlights its winning source in any visible My Mods panel.
The highlight clears when that Data selection is cleared or its panel closes,
and is scoped to the selected profile.
Data also reloads its current folder when the connected mod/plugin snapshot
changes, so enabling or disabling a mod updates its file list automatically.
Archives observes the same changes and updates archive loading status when
plugins are toggled.

The Panel layouts button beside the existing add-panel controls offers “Use MO2
panel layout”: My Mods on the left, with Plugins, Data, Archives, Saves and Downloads as
tabs on the right. “Undo panel layout” restores the previous arrangement for that
workspace during the current session. The preset is available in game workspaces;
normal layout persistence saves the resulting arrangement.

Saves is available under Installed and in the panel picker. It reads the original
MO2 Saves tab, including game-provided character/level/location descriptions and
the native choice of local or shared save directory. Search matches descriptions
and filenames; tooltips show the full entry. Toolbar actions invoke native
“Fix enabled mods” and save deletion, retaining MO2's repair/confirmation dialogs
and companion-file handling. Details (or double-click) opens the game extension's
save widget in a scrollable dialog, including its screenshot and missing-ESP list.
Save-action failures remain visible in the panel until Refresh, another action,
or a profile change, rather than disappearing with general status updates.

The expandable My Mods and Plugins search also accepts combined MO2 qualifiers.
My Mods supports `is:enabled`, `is:disabled`, `is:separator`, `has:conflicts`,
`category:"User Interface"` and `version:1.5`. Plugins supports `is:enabled`,
`is:disabled`, `is:locked`, `has:warnings` and `mod:"Mod name"`. Add a name phrase
alongside qualifiers to narrow results; all conditions must match. Unknown or
unfinished qualifiers remain literal text. Hover the search field for examples.
The filter button and filter bar remain removed. Clearing search restores the
ordinary list, including its existing separator collapse state.

Plugin activation uses MO2's original checkbox model, including native list
persistence, plugin-index refresh and extension notifications. Automatic Saves
reads restore the previous native tab without triggering a Plugins reload.

Archives remembers its search per panel in the saved profile workspace, including
switching games and restarting the frontend. Existing layouts default to an empty
Archives search.

For UI queue-delay diagnostics, set `MO2_UI_LATENCY_REPORT` to a writable JSON
path when starting the frontend. After ten seconds it samples input-priority
UI dispatcher waits for 45 seconds and records percentile/max delays, sample
counts, game coverage and slow instrumented phases. This opt-in probe does not
measure FPS; a successful report is not a responsiveness acceptance result.

The plugin provider counts the full native order once per update batch rather
than repeatedly sorting it for every row. `MO2_VERIFY_ORDER_PROVIDER=1` runs an
isolated UI-model check at startup for full-order read count, movement boundaries,
row identity, list growth/shrinkage and reversed sort direction. It does not
change the connected MO2 profile.


### Compact navigation and MO2 notifications

The bottom sidebar chevron switches between labelled navigation and an icon rail.
The game spine remains available. In compact mode, Play stays on the rail and the
arrow above it opens the original executable selector and pinned tools. The choice
is saved in `sidebar-collapsed.json` in the frontend configuration directory.
The behavior follows [Vortex Sidebar](https://github.com/Nexus-Mods/Vortex/blob/master/src/renderer/src/views/layout/Sidebar.tsx): compact navigation, slim footer and a bottom restore control.

The bell shows warnings from MO2's original Problems dialog, including reports from
its diagnostic extensions. It polls the selected profile, keeps active warnings in
a scrollable notification center, marks new reports unread, and uses NMA's notification
card theme for brief alerts. Mark all read clears the badge without removing active
warnings. Resolved reports disappear; changed or recurring reports become unread
again. Read state is per instance/profile for the current frontend session.
`--check-notifications` verifies deduplication, resolution, recurrence and isolation.

Separator names support double-click renaming in a prefilled modal. Rename preserves
collapsed state. All mod removal entry points now request confirmation in the
frontend; the bridge acknowledges only MO2's matching confirmation for the selected
mod, leaving native removal and profile cleanup in MO2. The opt-in
`MO2_VERIFY_SEPARATOR_INTERACTIONS=1` check creates its own temporary separator and
checks create/rename/delete Cancel and Accept, collapse counts and the routed
double-click handler, then removes its fixture. It changes native state during the
check and requires a ready host and a visible frontend.


External Files scans the connected instance's configured Steam game folder for
paths absent from the exact installed depot manifests, including installed DLC.
It uses SteamKit2 to read Steam's local appmanifest and cached binary manifests;
no Steam account login or game download is needed when all manifests are cached.
The panel is game-scoped, supports search, refresh and opening a source folder,
and is separate from MO2 Overwrite. Scans run off the UI thread and cancel on
navigation or target changes. Missing, encrypted, invalid or mismatched manifests
stop classification; the app never treats an incomplete vanilla list as complete.
Symlinks are listed without traversing their targets. This is file ownership
detection, not a content/hash verification or a cleanup/deployment operation.
No game files are moved or deleted by scanning.

Checks: `--check-external-files`; read-only installed-game scan:
`--scan-external-files <game-folder> <steam-app-id>`.


The shared tree adapter now avoids sorting roots that are already ordered. This
prevents redundant collection resets and duplicate visible row construction on
activation. --check-sorted-roots covers the collection semantics;
MO2_VERIFY_SORTED_ROOTS_UI=1 (with an isolated MO2_FRONTEND_LAYOUT) verifies live
Plugins selection and reverse/restore presentation sorting without changing MO2.
The cold game-switch input pause is still under investigation.


Mods and Plugins now defer their expensive view bodies to separate background
UI turns, allowing input/rendering between them. Pending loads cancel on view
removal; loaded bodies are reused on revisit. A brief loading label appears while
waiting. `MO2_VERIFY_DEFERRED_PANELS=1`, with an isolated two-tab FNV layout,
checks tab restoration/resizing and live plugin selection/sorting. Measured cold
switch pointer gaps improved to roughly 0.9–1.0 seconds; full fluidity is not yet
verified.


Workspace activation now also defers native panel controls, followed by list
bodies and visible row controls. The shared WorkspaceView accepts an optional
panel factory; its default NMA behavior is unchanged, and MO2's factory retains
native panels, dividers, models and layout persistence. Pending row work cancels
when virtualization or filtering detaches the row. Loaded row controls are reused
when reattached. `MO2_VERIFY_DEFERRED_PANELS=1` additionally checks row lifecycle
and native panel geometry while resizing and switching tabs. With an isolated
paired layout, `MO2_VERIFY_PAIRED_PANELS=1` checks divider hit testing and reports
pointer events for manual drag verification.

Two valid cold FNV-to-Skyrim runs with staged panels and rows reduced the largest
native pointer gap to445–489ms from the preceding893–1030ms range. See
`artifacts/staged-panel-comparison.json` for valid and excluded measurements.
This is not yet a claim of fully fluid cold switching.

`MO2_VERIFY_FILTERED_ROWS=1` with an isolated paired `MO2_FRONTEND_LAYOUT` checks
rapid debounced searches, empty/clear results and both scrolling lists at an
actual520px client height. Row controls must match native models and native
activation/order must stay unchanged. This passed for FNV and Skyrim. The tab
restoration check now verifies actual client widths after leaving maximized mode,
rather than assuming an assigned Width was applied by the desktop.

Mod presentation updates are buffered during profile selection so a departing
workspace does not build the next game's models. Same-profile refresh and failed
selection resume the surviving adapter. `MO2_VERIFY_PROFILE_BUFFER=1` with an
isolated FNV layout checks identity/selection retention and FNV/Skyrim round trips
against native IDs, state and priority. Measured model construction dropped from
304 to152 in the cold-switch fixture; see `artifacts/model-buffer-comparison.json`.


### Reusing Mods and Plugins pages (2026-09-14)

Returning to a workspace now reuses its detached Mods and Plugins controls for
that same page model. Concurrent hosts keep separate controls; other page types
continue their existing lifecycle. Set `MO2_REUSE_PAGE_BODIES=0` only when comparing
against fresh page construction. `MO2_VERIFY_PAGE_REUSE=1` checks state transfer,
concurrent hosts, reopening, cancellation and model replacement in a live window.
`MO2_VERIFY_PROFILE_BUFFER=1` checks real game round trips, retained body identity,
filters, recycled rows, native conflict highlights and unchanged native order.
Use an isolated `MO2_FRONTEND_LAYOUT` for the latter check.

This reduces repeated construction; it does not resolve the remaining first-switch
input pause. The default-build revisit measurement was 170.2 ms maximum delivered
pointer gap, compared with 295.8 ms in the recorded baseline. These measurements
are input event gaps, not frame times or a responsiveness guarantee.
