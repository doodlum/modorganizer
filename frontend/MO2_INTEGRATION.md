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

## Current acceptance status

The FNV in-game acceptance run passed: usable gameplay, MCM’s visible menu,
disabling through the frontend and absence on the next launch, restoration,
and agreement between all 14 running-game plugin indices and the MO2 order.
See [FNV acceptance evidence](FNV_ACCEPTANCE.md). Both game instances and the
native NMA navigation/panel controls have also been exercised against MO2.

Full integration remains open. The bridge exports MO2 mod state, Overwrite,
native priority labels, conflict/status text and plugin state. Details in MO2
opens the original conflict/file dialog. Plugin selection shows native diagnostic
text and mod indices; activation and row movement controls reflect native model
restrictions. MO2’s native profile manager handles profile operations, including
the My Loadouts card’s rename action. Broader diagnostic scenarios and the
remaining navigation/extension coverage still need validation.

## Changes originating in the MO2 host

`MO2_VERIFY_CATALOG=1 MO2_VERIFY_EXTERNAL_PROFILE=1` with `MO2_SCREENSHOT`
passed against the isolated FNV instance. After selecting Frontend Test once,
an independent bridge client changes MO2's native profile selector to Frontend
Clone Test. The verifier does not call the frontend's SelectProfile, ShowProfile
or Refresh to make the workspace follow this change. Its normal two-second poll
must observe the native profile and select the corresponding workspace itself.

The check compares every live mod name/state/priority and plugin
name/activation/priority with an independent host snapshot. It also verifies the
active spine icon, native profile caption and both visible panel views. The
clone has its own workspace and search; returning through the native selector
restores the original workspace ID and its previous search text.

In the clone, independent native API changes toggle an editable plugin and move
MCM Author Examples one priority position. Both changes, and their reversals,
appear through periodic polling. The full original and clone mod/plugin
snapshots are unchanged after restoration. The verifier includes cleanup for
interrupted assertions. This tests the native selector and extension API path;
it does not automate mouse input in the original MO2 window.

Evidence: `/tmp/mo2-external-profile.log`,
[returned live panels](artifacts/external-profile.png). The final render has
12 MO2 mod rows and 14 plugin rows; the screenshot was inspected. The process
exited successfully and the build passed with the existing NU1902 warning.

## Download availability

A failed MO2 snapshot now replaces the archive list with an unavailable message.
It does not retain rows labelled Downloaded/Downloading or claim that the folder
is empty. A successful refresh restores rows from the current MO2 snapshot.
Initial startup still asks the user to select a profile, and all transfer/install
controls remain disabled while unavailable.

The download-context runtime check now verifies that a native archive validation
failure removes all Install rows and the empty-folder claim, then checks that
refresh clears the unavailable message and restores exactly the host's archive
count. Existing stale-picker, cross-profile and cross-instance rejection checks
still pass; both profiles' mod/plugin snapshots remain unchanged. Evidence:
`/tmp/mo2-download-availability.log` and `artifacts/download-availability.png`.
Build passed with the existing NU1902 warning.

## Dialog requests during a background refresh

Mod details and Nexus settings now wait for a pending bridge refresh instead
of dropping the click when it holds the command lock. They mark the UI busy
before waiting, so repeated clicks do not queue duplicate dialogs. After the
refresh, they require the same connected endpoint/profile; a profile change
reports that the dialog should be opened again, and a failed refresh preserves
its connection error without opening a dialog.

`MO2_VERIFY_DIALOG_QUEUE=1` with `MO2_SCREENSHOT` runs six deterministic transport
cases in a temporary bridge directory: each action waits through a successful,
profile-changing or failing refresh. The check verifies duplicate suppression,
request target identity, stale/unavailable rejection and the busy-state cleanup.
This test does not open or read native account dialogs.

The same build passed `MO2_VERIFY_CATALOG=1 MO2_VERIFY_PREVIEW=1` against the
isolated FNV host: the actual mod-details action opened the original DDS preview,
rendered with a valid OpenGL widget and returned normally. All mod/profile hashes
remained unchanged. The temporary native preview fixture was removed after host
exit. Evidence: `/tmp/mo2-dialog-queue-native.log` and
`artifacts/dialog-queue-native-return.png`. Build passed with the existing NU1902
warning. The queue check is in `MockHost/Mo2DialogQueueCheck.cs`.

## PLAY and profile-card requests during refresh

PLAY and profile-card actions now wait for an in-progress bridge refresh and
mark the UI busy before waiting, preventing duplicate queued clicks. PLAY keeps
its original endpoint/profile and rejects a changed or unavailable target.
Profile-card actions keep their explicit registration/profile instead of using
whichever profile is selected later. They reconnect to that host, obtain a fresh
profile list and require the card's profile to still belong to it before acting.

The controlled transport suite (`MO2_VERIFY_DIALOG_QUEUE=1`) now has thirteen
cases, including launch, profile-card operations, duplicate suppression, changed
profiles, failed refreshes and missing card targets. A profile-card action can
recover from a failed active snapshot through its own fresh host connection;
a launch or current-profile dialog cannot cross that boundary. No game or native
dialog runs in these controlled cases. Log: `/tmp/mo2-action-queue.log`.

`MO2_VERIFY_CATALOG=1 MO2_VERIFY_ACTION_GUARDS=1` additionally passed against the
isolated FNV host. An unconfigured executable reached the host's validation and
was rejected before startApplication. Renaming the active profile reached the
bridge's native-profile restriction and was rejected without opening a prompt.
This matches MO2 2.5.2's ProfilesDialog rule requiring a different active profile
before rename. The full snapshot remained unchanged and refresh recovered both
connections. No profile was renamed and no game was launched by this check.
Log: `/tmp/mo2-action-guards.log`; returned panels: `artifacts/action-guards.png`.
Build passed with the existing NU1902 warning.

## Implementation history

### Download action profile boundaries

Archive selection now captures the MO2 endpoint and profile before awaiting the
file picker. Installation checks that target again after acquiring the command
lock. Archive-row installation and transfer controls also retain their original
target, and queued Nexus requests validate their captured target before sending.
A changed target reports that the action needs to be chosen again, without
sending it to the new profile or invalidating its valid snapshot.

Downloads displays the game/profile name and provides its path in a tooltip.
Import, download, row-install and transfer controls require an available snapshot
and disable while selecting a profile, installing, launching or managing a mod.

`MO2_VERIFY_CATALOG=1 MO2_VERIFY_DOWNLOAD_CONTEXT=1` with `MO2_SCREENSHOT` passed
against the isolated FNV host. The check uses the production picker continuation
with an asynchronous picker callback that selects Frontend Clone Test while
selection is pending. It verifies rejection of that install, a stale transfer
action and an incorrect instance identity. A current-target attempt reaches MO2's
native missing-archive validation, then refresh restores the controls. Both
profiles' mod/plugin snapshots remain unchanged and Frontend Test is reselected.
This check does not automate the operating system's file picker or install a mod.
Evidence: `/tmp/mo2-download-context.log` and
[Downloads with its profile context](artifacts/download-context.png). Build passed
with the existing upstream OpenTelemetry warning.

The default frontend opens NMA’s My Games and the real profile catalog without assuming an
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
That session subsequently rendered Doc Mitchell's room, processed DLC inventory
messages, accepted the character name and opened character creation. The game
detected two XInput controllers while the keyboard/mouse test could not
operate the remaining controls. The isolated profile's next test sets
`bDisable360Controller=1` under `[Interface]` in FalloutPrefs.ini; the game binary
contains that exact setting name, and the retry restored the mouse cursor.
The game console confirmed that value is 1, and Escape could skip the intro
and open the pause menu. Character-creation mouse navigation still did not
respond; the controller setting alone has not resolved that remaining issue.
Original test INIs are preserved in `artifacts/fnv-before-keyboard-test`.
MCM's in-game menu and the disabled-mod comparison are still pending.
With controller input disabled, the in-game console in Doc Mitchell's room
reported xNVSE 6.4.8 and `GetModIndex "The Mod Configuration Menu.esp"` returned
`0A`. `artifacts/fnv-mcm-esp-loaded.png` captures the result. This establishes
actual ESP loading through the frontend/MO2 launch, beyond the DLL log. The
game also auto-loaded the DLCs, putting MCM after them despite their inactive
checkboxes in the host; this load-order difference still needs investigation.


### Native NMA navigation and cross-game UI — 2026-09-12

Live mode now uses upstream NMA Spine, HomeLeftMenuView, TopBarView,
MyGamesView/GameWidget and MyLoadoutsView/LoadoutCardView with MO2 catalog
adapters. No upstream source was changed. My Games groups real instances by
game; My Loadouts groups their actual profiles by game and identifies the
instance on each card. The settings gear opens the MO2 connection settings.
Profile creation/copy/removal stays in the original MO2 profile manager;
the individual card copy/delete controls are disabled, not simulated.
The top-bar account control is disabled; MO2 still owns Nexus authentication.

Home browsing has a separate workspace from the live mod/plugin panels.
Selecting a profile changes the host and returns to mods on the left and
plugins on the right. Filtered game pages have distinct navigation contexts,
and tab-header selection is synchronized when restoring dormant panels.
These avoid reusing Skyrim’s filtered page when opening FNV and losing the
selected My Games tab during workspace activation.

The combined `MO2_VERIFY_CATALOG=1`, `MO2_VERIFY_NATIVE_PANELS=1` and
`MO2_VERIFY_CROSS_GAME=<registered Skyrim instance>` run passed. It starts
without fixture data, follows the native sidebar and profile-card command to
FNV, adds/closes a tab through the native top bar, splits to 2/3/4 panels and
closes back to one through native commands, and checks repeated My Loadouts
navigation does not duplicate its tab. The game widget View and profile-card
buttons then switch to Skyrim and back to FNV. All 150 Skyrim mod and 150
plugin states/priorities match an independent MO2 snapshot; download and
executable contexts switch too. FNV returns with 11 mods and 14 plugins.
This verifies navigation and MO2 state, not Skyrim gameplay or the remaining
FNV MCM-menu/disabled-mod acceptance checks. Build passes with the existing
upstream OpenTelemetry NU1902 advisory.

### Native profile card actions and live catalog refresh — 2026-09-12

Create Copy and Delete now target the card’s profile through MO2’s original
profile manager controls. The adapter selects the manager row, invokes its
copy or removal button, and leaves the original name prompt/confirmation and
save handling intact. It does not activate the target profile. MO2 rejects
removal of its active profile; the card disables that action too. Acting on
another instance’s profile does not replace the current frontend connection.
The profile manager remains open until the user closes it.

My Games, My Loadouts and the game spine observe catalog changes. Visible
cards update when profiles appear/disappear or their mod counts change; pages
refresh again when reactivated. The catalog comparison excludes download byte
progress, so downloading does not continually rebuild profile cards. The
catalog remains a read-only view of MO2 files, not an independent profile store.

`MO2_VERIFY_PROFILE_CARDS=1` exercised the actual native card commands against
the isolated FNV host. MO2 copied Frontend Test to a disposable profile with
identical modlist/plugins/loadorder files. Its original deletion confirmation
was answered No (profile retained), then Yes (directory and card removed).
The active profile remained Frontend Test. A repeat run saved the before-state
and verified all original profiles’ modlist/plugins/loadorder bytes unchanged.
The first run had detected an unrelated profile-file rewrite without preserving
its before-bytes; its exact change cannot be attributed from that run. MO2’s
native manager constructs every Profile and can flush their pending mod lists;
the adapter does not replace that behavior. The disposable profile was removed
through MO2, and the temporary dialog driver was removed before production
host restart. No Skyrim profiles were copied or deleted by this check.

Thirteen bridge contract checks and the frontend build pass (the existing
upstream OpenTelemetry NU1902 warning remains). This adds real profile actions;
it does not close the pending in-game MCM menu, disabled-mod comparison, or
FNV DLC load-order investigation.

### Native mod metadata and details — 2026-09-12

The mod panel includes Overwrite, with MO2’s blank priority label and essential
state preventing activation, movement or uninstall. Conflict and status text
comes from the original mod-list model’s tooltips; the bridge converts HTML to
plain text and does not compute another conflict result. The combined column
provides full text on hover beside the mod name and activation controls.

Details in MO2 invokes the original mod-list double-click handler on its
conflict column. Regular mods open the native conflict/file dialog; Overwrite
opens its original file dialog. Commands retain the instance/profile guard and
wait for the native dialog to close before refreshing. A filtered-out mod
requires clearing the filter in MO2.

Live checks compared Skyrim’s 151 rows (including Overwrite) and 150 plugins
against an independent host snapshot, including eight native conflict messages,
and restored FNV through native game/profile navigation. The actual Details
button opened and returned from both FNV Overwrite and Skyrim Starfrost’s
conflict dialog. The latter showed its winning conflict with StarfrostInjuries.
Screenshots are in ignored artifacts: mo2-skyrim-native-conflicts-dialog.png,
mod-details-skyrim-dialog-return.png and mod-details-fnv.png.

An initial dialog check timed out after closing its X11 window externally;
repeating with Windows WM_CLOSE passed for both dialogs. Fourteen bridge
contract checks and the frontend build pass (existing OpenTelemetry NU1902
warning). This check does not establish Skyrim gameplay or complete plugin
diagnostics. FNV in-game acceptance is recorded separately above.

### Profile rename through the original manager — 2026-09-12

My Loadouts cards retain the upstream NMA view and add a Rename icon beside
its existing actions. Rename opens MO2’s original profile manager and name
prompt for that card’s profile, without selecting it. The active profile’s
button is disabled, and the bridge rejects attempts to rename the active
profile, matching MO2’s restriction. The original profile manager remains open
after the prompt and must be closed to finish the action.

The former live data-provider rename placeholder was actually NMA collection
renaming. Collection naming now remains fixture-only; live profile renaming
routes through the profile card and MO2’s original dialog. No profile directory
or profile contents are written by the adapter.

The extended isolated FNV card check copied a disposable profile, cancelled a
rename, renamed it and restored its name, then exercised Delete No and Yes.
Cards refreshed after each rename; modlist/plugins/loadorder files remained
identical and all original profiles’ checked bytes and the active profile were
unchanged. A narrow dialog-answer driver was used only for this verification
and removed before restarting the production host.

The final card layout uses NMA’s standard icon button with a Rename profile
accessibility label and tooltip, preserving space for Create Copy. The active
card’s rename command was checked as disabled. Fourteen bridge contract checks
and the frontend build pass; richer plugin diagnostics remain open.

### Native plugin diagnostics and restrictions — 2026-09-12

The bridge reads the original plugin model beneath its proxies, mapping rows
by plugin name. Snapshots preserve the existing API state/order/masters/origin
and add native tooltip text, the displayed hexadecimal mod index, checkable
state and draggable state. HTML is converted to plain text. This includes
plugin descriptions, archive/type information and any additional/LOOT messages
MO2 already holds; the bridge does not run a separate analyzer or LOOT process.

Selecting plugins in the right panel reveals their native diagnostics and mod
indices. Activation operates only on checkable selected plugins and is disabled
when none are checkable. Native NMA up/down row commands also respect MO2’s
draggable flag. Game-specific priority constraints still belong to MO2.

Live checks compare every exported diagnostic, index and restriction against
fresh host snapshots for 14 FNV plugins and 150 Skyrim plugins. They select
FalloutNV.esm and Skyrim.esm, verify rendered native text, and assert disabled
activation and movement controls. Cross-game selection restores FNV.
The Skyrim screenshot is artifacts/plugin-details-skyrim.png (ignored).
These checks exercise real forced-plugin and archive/type messages, but do not
establish coverage of every missing-master, dirty-plugin or LOOT message type.

### Reorder guards and native state changes — 2026-09-12

The shared MoveItems path used by NMA row drops now rejects a selection
containing any plugin MO2 marks as fixed, including mixed selections. This
closes the gap between disabled arrow buttons and drag operations.

Before applying a proposed order, the live adapter obtains a fresh MO2 snapshot
under its command gate. If the active profile, plugin names, priorities or
activation state changed since the proposal was formed, it refreshes the view
and rejects the old proposal. Native MO2 remains responsible for accepted
priority changes and game-specific rules. This is a preflight check, not an
atomic transaction across multiple native priority calls.

MO2_VERIFY_REORDER_GUARDS=1 tested the isolated FNV profile: a fixed FalloutNV.esm
move left host state unchanged; MCM moved through the shared drop path and was
restored; an independently changed native order was preserved when the old
frontend order was submitted. Final host priorities and activation matched the
initial snapshot. The check invokes the shared MoveItems path directly; it does
not claim to test pointer gesture recognition or drag visuals. Build passed
with the existing upstream OpenTelemetry NU1902 warning.

### Pointer drag and stable panel layout — 2026-09-12

Manual navigation from the default My Games page exposed two layout issues
that the preconnected screenshot checks had missed. The star-sized mod name
column could collapse after the initially empty table acquired rows. It now
has a readable initial pixel width; priority, status and action widths fit the
two-panel Steam Deck view, and columns remain resizable. Opening plugin
diagnostics on selection also shifted rows during the start of a drag. The
details area now reserves a stable 150-pixel height while connected and shows
a selection hint when empty.

The desktop uses Wayland; X11 pointer injection did not reliably reach visible
controls. Verification used a temporary Linux uinput absolute mouse, including
real button press, pointer motion and release, through the normal visible NMA
rows. It navigated My Games → FNV → Frontend Test, dismissed the ordering hint,
scrolled the plugin list, and dragged MCM from native priority 13 to 12. A fresh
MO2 snapshot confirmed the change. After the layout fix and normal frontend
restart/navigation, a drag of the unselected MCM row restored it to priority 13
without the details area moving the table. The complete plugin snapshot and
profile identity matched the initial snapshot exactly.

A pointer drag of fixed FalloutNV.esm down the list also left the complete
native snapshot unchanged. Evidence in ignored artifacts: pointer-drag-before,
pointer-drag-moved, pointer-drag-restored and pointer-drag-fixed JSON files,
plus pointer-drag-restored.png and pointer-drag-fixed.png. The virtual input
device closes after each gesture; no input daemon or test driver remains.

This establishes single-plugin pointer drag and a fixed-plugin rejection on
FNV. Multi-selection gestures, panel drag arrangements and broader diagnostics
remain separate verification work. The frontend build passes with the existing
upstream OpenTelemetry NU1902 warning.

### Native warning marker and missing-master scenario — 2026-09-12

The plugin row now prefixes a warning marker when the original MO2 plugin
model reports its warning icon. This is the native warning result, not a
frontend scan or text-based guess. Plugin keys and names used for commands
remain unchanged. Selected details continue to show MO2’s full tooltip text.

A 138-byte diagnostic ESP was packaged in a temporary archive and installed
with MO2’s original Quick Install dialog in the isolated FNV instance. It
contained only a TES4 header referencing MO2 Diagnostic Absent Master.esm.
Enabling its mod through the native frontend toggle exposed the ESP; enabling
the plugin made MO2 report Missing Masters with that exact name. The live
check asserted the native warning flag, row label marker and rendered details.
Screenshot: artifacts/missing-master-diagnostics.png. No game was launched with
the diagnostic plugin.

The mod was then removed through MO2’s original Yes/No confirmation. The
complete plugin snapshot and profile identity matched the pre-test snapshot
exactly, and the diagnostic mod directory was gone. Evidence:
artifacts/warning-before.json and warning-after.json. The diagnostic archive
remains only in ignored test artifacts for reproduction. Both production hosts
have the updated bridge; no temporary MO2 driver was installed for this test.

The missing-master path is now verified. Other LOOT message categories,
multi-selection gestures and the remaining navigation/extension coverage still
require review. Build and fourteen bridge contract checks pass.

### Native create card mapped to MO2 profiles — 2026-09-12

Each registered instance with profiles now contributes NMA’s original Create
new loadout card to My Loadouts. Its caption identifies the target instance,
and the tooltip gives the full instance path. The command starts/connects that
host, verifies its profile ownership and invokes the original profile manager’s
Create button. MO2’s ProfileInputDialog owns the name prompt, default-settings
choice and creation. No new NMA loadout record or profile file writer is used.

The isolated FNV check invoked the actual card button: Cancel created no
directory; confirmation created a fresh profile with MCM disabled and a live
profile card; the active profile stayed Frontend Test. Native Delete removed
the disposable profile. All existing profiles’ checked modlist/plugins/loadorder
bytes remained unchanged. The narrow dialog-answer driver was removed before
restarting the production host.

Build and fourteen bridge contract checks pass. The native card layout was
visually inspected in artifacts/create-profile-cards.png. Creation behavior was
verified on FNV; the card is also supplied separately for each Skyrim instance.

### Real desktop integration in live mode — 2026-09-12

The live workspace had still registered ScenarioOSInterop, whose open-link and
open-file operations only displayed fixture toasts. It now references NMA’s
Backend project and registers its platform IOSInterop service with the existing
frontend settings manager, logging and real filesystem. This selects the
upstream Linux desktop portal implementation on this machine. The service
provider is disposed with the workspace. Fixture mode retains its test service.
Only desktop/runtime services are registered from Backend; MO2 still supplies
the live profile, mod, plugin and download state.

Live top-bar GitHub, Discord and Nexus status commands now open real URLs.
GitHub targets this MO2 fork; Discord uses MO2’s existing support invitation.
The NMA-specific forum entry is disabled instead of retaining a no-op command.
The remaining inherited help entries still need a control-by-control audit.

MO2_VERIFY_DESKTOP=1 resolved upstream LinuxInterop, reported five actual
filesystem mounts and requested the selected instance’s downloads directory.
A desktop screenshot confirmed Dolphin at the real FNV downloads directory
(artifacts/native-desktop-folder.png). This verifies populated-folder opening;
it does not verify every desktop operation or each external help destination.
Native LinuxInterop retains its upstream limitation for empty directories.

The full dependency build passed with upstream NU1902, source-generator CS8785
and CS1690 warnings; the incremental frontend build passed. The live catalog,
2/3/4-panel controls and FNV → Skyrim → FNV regression also passed after wiring
the real service. No archive/profile store was added by this change.

### Nexus account settings stay in MO2 — 2026-09-12

The top-bar NMA login placeholder is replaced by a Nexus account action. With
a selected profile, it invokes MO2’s original Settings action and selects the
native nexusTab in SettingsDialog. Without a selected profile it opens My
Loadouts so the user can choose an instance. The bridge retains the normal
profile/session guard and waits until the original dialog closes before
refreshing the live profile.

The adapter only selects the tab. It does not read account fields, export
credentials, emulate authentication or toggle NMA login state. Login, API-key
and disconnect controls are the original MO2 controls. Changing accounts was
not part of the verification.

MO2_VERIFY_NEXUS_ACCOUNT=1 invokes the actual native top-bar button command.
Both the isolated FNV and registered Skyrim hosts opened SettingsDialog on
nexusTab and returned successfully after Windows WM_CLOSE. The selected tab
is checked inside the bridge without reading its contents. Screenshots only
capture the returned frontend, not the account dialog. Fourteen bridge
contract checks and the frontend build pass with the existing NU1902 warning.


### Explicit original-interface switching — 2026-09-13

The MO2 instances page now offers Show original MO2 / Hide original MO2 for
its connected host. This reveals the existing main window without launching
another executable or replacing the backend. The normal hidden-host startup
still suppresses unsolicited main windows, splash screens and notification
toasts. Explicitly showing MO2 permits its normal UI until hidden again.

The bridge reports current window visibility as optional instance metadata;
older or unavailable bridges disable the action. Visibility commands require
the current bridge session and MO2 profile. The frontend waits for a pending
refresh and rechecks its instance/profile before sending, avoiding a dropped
click when periodic polling holds the command lock.

MO2_VERIFY_CATALOG=1 MO2_VERIFY_ORIGINAL_UI=1 with MO2_SCREENSHOT exercises the
rendered settings-page button against the isolated FNV Frontend Test profile.
The original host starts hidden, becomes visible, and hides again; the full
profile/mod/plugin snapshots stay unchanged. X11 independently confirms the
original New Vegas window appears and disappears. The captured original window
(artifacts/original-mo2-visible.png) shows Frontend Test, both enabled MCM mods
and fourteen active plugins. Two runs pass after the refresh-lock fix. The
returned instances-page render is artifacts/original-ui.png. Seventeen bridge contract checks pass;
the incremental build passes with the existing OpenTelemetry NU1902 warning.


### Separator collections

The live frontend maps MO2 separators to local collections. `My Mods` shows all installed mods, while each collection page filters to the mods after its separator and before the next separator in ascending native priority. `Ungrouped` represents mods before the first separator and creates no files. Separators and Overwrite are excluded from mod rows/counts.

The sidebar uses NMA's original collection navigation/toggle controls; creation uses its `Name your Collection` dialog and invokes MO2's own separator action with the entered name. Collection switches set all toggleable members to the requested state, leaving essential game content alone. `Move to collection` appends selected managed mods to a collection by changing native MO2 priorities. Empty collections remain visible. Collection page identity persists with panel layouts and is scoped by the workspace's game/profile.

Verified on FNV: native separator creation from the NMA dialog, mixed-state collection enable/disable, moving a mod into another collection, cleanup and restoration of original activation/priorities. Verified navigation to Skyrim and back at 1280×750. `--check-collections` covers separator boundaries, sorting, empty groups, ungrouped membership, priority movement, and Overwrite exclusion. These are local MO2 groups; Nexus collection publishing/import metadata is not implemented by this mapping.

### Shared Nexus login foundation

`connectNexusAccount` accepts a private key-file path, validates the key before applying it, then uses MO2's native manual login path. Settings, key entry, and the specific restart/Continue task dialog are handled without mapping their Qt windows. Success requires the native account ID to match the validated account and the persisted Windows credential to match the supplied key. The operation is guarded by the bridge session and is instance-global rather than profile-specific. API responses are whitelisted; neither keys nor native account-log text are returned.

Runtime verification reauthenticated the authorized existing key on FNV (MO2 2.5.2) and Skyrim (2.5.0), confirmed native identities and persistence, and completed without restarting either host. This does not yet prove switching to a different account.

`Mo2NexusSso` implements the deployed MO2 client's protocol-2 SSO exchange with bounded, fragmented text messages, cancellation, and errors that omit response bodies. `--check-nexus-sso` covers protocol behavior; `--check-nexus-sso-connection` verified initialization with the live service and cancellation before browser authorization. The transport is now connected to the top-bar login command through the original NMA login view. The shared workflow and its verification are described below.

Source references: [MO2 v2.5.2 account UI](https://raw.githubusercontent.com/ModOrganizer2/modorganizer/v2.5.2/src/settingsdialognexus.cpp), [MO2 SSO transport](https://raw.githubusercontent.com/ModOrganizer2/modorganizer/v2.5.2/src/nxmaccessmanager.cpp), [MO2 shared Qt task dialog](https://raw.githubusercontent.com/ModOrganizer2/modorganizer-uibase/master/src/report.cpp).


### Shared login popup and all-instance application

The top-bar Nexus account action opens NMA's original login overlay in a centered modal window. Its copy-link and Cancel controls are bound to the MO2 SSO session. Successful authorization applies the returned key to every registered instance through `connectNexusAccount`. The account indicator is populated from confirmed native identities and validated account membership; it reports logged-in state only when all registered instances use the same account. `Connect MO2 instances…` in the account menu allows authorization again.

Credential handoff uses a newly created private directory (0700) and file (0600) on Linux. Only its path enters bridge requests. The file is removed after operations finish. Cancellation interrupts the SSO wait, or lets an already-submitted account operation finish and skips later instances. Failures are reported per instance with generic text, without logging raw backend replies or credentials. A timeout is not retried automatically.

Validation: `--check-shared-nexus-login` covers handoff permissions/cleanup, partial failure isolation, and cancellation after submission. `MO2_VERIFY_LOGIN_POPUP=1` verified the native overlay fits the 1280×750 Steam Deck window, its Cancel command closes it, and it can reopen. `--check-shared-nexus-live` applied the authorized existing key to all three registered installations (FNV, Skyrim, and `/home/deck/ModOrganizer2`), then independently confirmed the same native account in all three. The older Skyrim installation now has the bridge and a launcher using its own Proton prefix. A complete browser-authorized SSO roundtrip remains unverified; the live SSO handshake/cancellation and the post-authorization all-instance workflow were verified separately.


### Collection removal and container launcher locking

Separator-backed collection sidebar entries now offer NMA's `Remove collection…` context menu and confirmation dialog. The wording states that MO2 removes the separator across its profiles while retaining installed mods and their enabled states. `Ungrouped` cannot be removed. The bridge rejects non-separators and stale profile/session requests; after frontend confirmation, it matches MO2's exact native confirmation using the displayed separator name and acknowledges only that dialog. It then uses MO2's original removal/profile-cleanup path. Successful removal returns navigation to My Mods.

Live FNV checks verified ordinary mods are rejected, native separator creation/removal preserves the original priorities and enabled states, frontend Cancel keeps the separator, and frontend confirmation removes it without leaving a native confirmation open. Temporary test collections were removed. The updated bridge is deployed to all three registered instances.

A runtime issue found during deployment is also fixed: Steam's container closes inherited descriptors, so `exec` into it released the per-prefix launch lock prematurely. The launcher shell now remains alive holding the lock while its child runs. A regression test explicitly closes inherited descriptors in a fake runtime; the real FNV launcher also rejected a second start while its host was running. Existing runtime selection and spaced-path checks continue to pass.

### Archive extraction

The Archives panel's `Extract…` button invokes MO2's original archive context action, destination picker, parser, and progress UI. Requests identify both the filename and originating mod, and reject a changed origin, profile, or bridge session before extraction. The bridge reports that the action opened; cancelling the native picker is not reported as successful extraction.

Live FNV verification extracted all 142 entries from `Fallout - Misc.bsa` into an isolated temporary directory, compared that count with the archive header, and removed the test output. A changed origin was rejected. At 1280×750, frontend selection remained active through Refresh and filtering, and `Extract…` opened the original folder picker. Escape cancelled it and returned to the selected row with Browse and Extract enabled. The toolbar wraps for narrow panels; split-panel layout and Skyrim extraction still need runtime verification.

### Collection rename

Collection rename now uses NMA's original rename dialog and MO2's original inline model edit. The bridge requires an existing separator, validates the new name and collisions, and checks profile/session identity before dispatch. Native rename owns directory changes and cross-profile references. The frontend updates collection keys in open panels and stored layouts for that instance; Ungrouped cannot be renamed.

The incremental frontend build and 23 bridge contract checks pass. A live Skyrim 2.5.0 check created, renamed, and removed a temporary separator, then confirmed all original mod enabled states and priorities were preserved. The immediate rename after creation was rejected as busy; a later request, after the native UI settled, succeeded. The updated bridge is deployed and running on all three registered installations. Each live host responded to a snapshot and rejected a missing-separator rename through the new action; source files match the deployed copies.

Rendered verification found collection pages were incorrectly constructed with `isCollection=false`, hiding the upstream status/menu bar. The factory now identifies them as collections, and the menu bar is visible in the two-panel Steam Deck layout. The panel deletion action now uses the same confirmed native removal as the sidebar (see panel removal below).

The apparent immediate menu closure was traced to `Button.OnKeyDown`: X11 reported Escape held (keycode 9). Releasing it allowed the original menu to remain open. Temporary click/stack tracing was removed. The original NMA rename dialog rendered at 320×223, and Cancel preserved the separator. `MO2_VERIFY_COLLECTION_RENAME=1` then exercised the actual dialog commands in-app: Cancel preserved native state, Accept renamed the separator and updated multiple open panel identities, and a second accepted rename restored the test name. Saved layout keys independently matched the restored name in multiple panels. The temporary separator was removed; all original native mod states and priorities matched the pre-test snapshot. This check requires the explicitly named temporary separator and should run with an isolated `MO2_FRONTEND_LAYOUT` file.

Ungrouped now hides the collection action bar, since its placeholder has no rename/delete action. The two-panel 1280×750 collection view was inspected with the Skyrim mod and plugin lists visible. The incremental build passes.

### Conflicts involving multiple providers

Selection highlighting now uses the full native origin ordering instead of only the final winner. `OrganizerCore::getFileOrigins` returns the winner followed by the ascending alternatives vector; `AdvancedConflictsTab::createItem` uses the same alternatives to identify providers before and after the selected mod. The bridge preserves that order, including native archive precedence, without splitting display strings or sorting by mod priority.

The 24 bridge contract checks include a four-provider case, names containing commas, missing origins, and duplicate self-origins. A live FNV check used three temporary enabled mods supplying the same unique test file. Selecting each low, middle, and high provider returned exactly the expected winning/losing mod sets. The fixtures and their profile entries were removed; an independent snapshot after restarting the host matched the original profile, every mod's state, and every mod's priority. Archive/loose mixed conflict combinations and rendered highlight colors still require a broader runtime audit.

Color mapping was corrected against `ModListView::markerColor` and `ColorSettings` in the MO2 source: mods overwriting the selected mod are red; mods overwritten by the selected mod are green. The frontend retains its existing muted tints and purple plugin links. Native archive-specific colors and customized MO2 colors are not yet reflected.

The conflict update is deployed to both Skyrim installations as well as FNV. Both restarted Skyrim hosts returned live snapshots and empty selection-link results; their deployed Python sources matched the worktree. The frontend build and 24 bridge checks passed, and the rebuilt frontend is running. Deployment exposed another lifecycle limitation: the legacy Skyrim container retained an `nxmhandler.exe` after MO2 exited, so its old launcher held the prefix lock. After confirming MO2 was gone, the owned stale launcher process group was terminated and the instance restarted successfully. The launcher supervision change below addresses this case for subsequent launches.


### Native host lifetime supervision

The launcher now holds its prefix lock until the observed native MO2 process exits, rather than until every process in the Steam container exits. A host-side Python observer matches the exact instance executable and Wine prefix, excludes zombie processes, and checks a cached process first to avoid repeatedly scanning the whole desktop while MO2 is running. It keeps the lock during startup; a timed-out bridge request is never treated as an exited host. When no native host was observed, the inner command completion marker or container exit supplies the startup outcome. Python 3 is checked before starting the host.

The completion wrapper runs inside the container, since containers close inherited descriptors. Only the outer launcher holds the lock. Surviving game/tool/Steam processes are left running; neither the observer nor launcher sends termination signals to them. Temporary completion directories are removed when the launcher finishes.

Seven launcher checks cover runtime selection/spaced paths, missing runtime, descriptors closed by the container, preserved command exit status, a surviving container, a surviving Proton stub, duplicate launch rejection, prefix isolation, zombie processes and PID reuse. The first real attempt demonstrated that Proton's Steam stub itself could remain after MO2 exited, so observing only the Proton command was insufficient. With native-process supervision, the legacy Skyrim host connected, closed, and its launcher exited while its original container remained alive. A replacement launcher then connected successfully without killing the surviving container. The other registered instance scripts share this launcher and will use the updated supervisor on their next launch.


### Collection removal from panels

NMA's collection overflow menu now offers `Remove collection…`, bound to the existing separator confirmation and native MO2 removal path. Both sidebar and panel removal publish the same collection-removal event. All open collection panels in that MO2 instance switch to My Mods, and saved layouts replace the removed collection references with the My Mods page identity. Ungrouped retains no removal action.

The live collection dialog check now covers removal as well as renaming. With multiple collection panels open, Cancel preserved the native separator and page identities; Accept removed the separator and switched every tested panel to My Mods. The saved test layout contained no removed collection keys. An independent native snapshot matched the original profile and all original mod states/priorities. The test used a separate layout file, and the temporary separator was removed through the tested frontend action. The build and 24 bridge checks passed.

### Collection navigation history

Collection identity changes now update Back/Forward history as well as open panels and saved layouts. The NMA UI fork exposes `PanelTabViewModel.ReplaceHistoryPages`, backed by a small `TabHistory.ReplacePages` method that changes stored page references without moving the history position or adding visits. The MO2 workspace applies replacements only to the affected instance's collection entries.

The live dialog check navigated from a temporary collection to Ungrouped, then renamed the collection from another panel. Back opened the renamed collection and Forward still opened Ungrouped. After removal, Back opened My Mods and Forward remained usable. Both the UI dependency and frontend built successfully, and the final native snapshot matched the original profile and all mod states/priorities. These checks used an isolated layout file and removed the temporary collection. The two UI-fork source changes are under `frontend/upstream/src/NexusMods.App.UI/WorkspaceSystem/PanelTab` and must be included when rebuilding the dependency.

### Overwrite refresh and selection

Overwrite now preserves file selection through filtering and refresh, retains read errors when the filter changes, disposes replaced table sources, and checks the current profile target when enabling actions. Files smaller than 1 KiB display their byte count instead of rounding to `0 KB`.

`MO2_VERIFY_OVERWRITE_REFRESH=1` verified the original Mods search/clear, selection/deselect, and activation/restore commands, followed by Overwrite filter/clear/refresh selection retention against two temporary files in the isolated FNV instance. The older Mods check was updated to require Overwrite exclusion and a hidden Rules tab, matching the user's current layout requirements. The Overwrite panel was inspected at 1280×750 beside Plugins. Both temporary files were removed, and independent reads confirmed the original Overwrite file list and every native mod state/priority were restored. The frontend build passed. Read-error persistence was reviewed in the rendering path; an injected connection-failure UI check has not been run.

### Shared account indicator refresh (2026-09-13)

The top bar now rechecks registered MO2 account state every 30 seconds and when
its instance catalog changes. Reads are serialized and superseded results are
ignored. Starting login invalidates an older status read; finishing or cancelling
login requests a fresh native check. Both account entry points share the same
login guard. An unconfirmed account clears the username and paid role. Closing
the frontend stops its account timer and suppresses pending updates.

Verification: `--check-account-monitor` passed catalog-change and login races,
serialization, disposal, and sanitized read failures. `--check-account-status-live`
performed two read-only checks against the running catalog and confirmed the same
account across 3/3 native instances both times. The incremental frontend build
passed with the existing OpenTelemetry NU1902 warning. These checks do not prove
a browser-authorized SSO roundtrip or shared logout; those remain outstanding.

### Shared native logout (2026-09-13)

The original NMA Sign out menu command is now connected to shared MO2 logout.
It uses the same account-operation guard and NMA popup styling as login, hides
the browser-link section, and reports per-instance failures. Closed instances are
started through the normal host launcher before disconnecting. Cancellation waits
for the submitted native operation and skips subsequent instances.

The session-guarded `disconnectNexusAccount` bridge action invokes the original
MO2 Disconnect button in its hidden settings dialog, waits for queued account UI
updates, and verifies both disconnected runtime state and absence of the saved
Windows credential. The native restart prompt is completed using Continue after
successful disconnection. The original native action owns credential removal.

Verification: 25 bridge contract tests and shared logout tests passed, including
stored-credential/runtime mismatches, stale sessions, deduplicated instances,
startup ordering, partial failure and cancellation. FNV native disconnect/readback/
reconnect passed. `MO2_VERIFY_SHARED_LOGOUT=1` exercised the original NMA Sign out
command against all three running hosts, verified the cleared top bar, restored
the existing account, and verified the periodic monitor picked up that restoration.
The UI check uses an explicit credential-file path environment variable and prints
only counts and pass/fail messages. `/tmp/mo2-logout-ui-check.log` contains both
runtime passes. Profiles and mod/plugin activation/priorities matched their native
baselines by name after restart (native enumeration order can change).

The updated bridge was loaded in all three hosts, now running through the launcher
lifetime supervisor. The frontend builds with the existing OpenTelemetry warning.
Full browser-authorized SSO remains unverified. Partial account state currently
hides the signed-in account menu; retry access in that state still needs attention.

### Partial account recovery controls (2026-09-13)

Connections now exposes Connect Nexus and Sign out of all instances independently
of the top bar's fully-authenticated state. Both controls use the same guarded
account workflow as the original NMA account menu. A reactive count reports how
many registered instances are connected; its updates do not depend on mod-list
changes. The view subscribes only while active, including when restored from a
saved workspace before the top bar is created.

After a system restart, the frontend rebuilt and all three native hosts reopened.
Repeated native reads confirmed 3/3 persisted accounts. The live partial-account
check disconnected one host, waited for the signed-in menu to disappear, verified
the Connections sign-out control remained enabled and correctly bound, and used
it to disconnect every remaining account. It also verified the Connections count
immediately became zero, restored the existing account across all hosts, and
observed the running frontend detect the restored login. The final check log is
`frontend/artifacts/partial-account-ui-check-final.log`. The rendered controls and
zero-connected status fit the 1280×750 screen; screenshot:
`frontend/artifacts/partial-account-controls.png`.

Build, account-monitor/shared-logout checks, 25 bridge tests and whitespace checks
passed. The existing OpenTelemetry build warning remains. This resolves the
partial-account recovery limitation noted above; full browser-authorized SSO is
still unverified.

### Browser-authorized SSO and visible account menu (2026-09-13)

The upstream top bar now uses its existing Account icon when no avatar image is
available. The original binding previously rendered an empty image for null,
leaving the signed-in account button blank. This is a one-line change in the
upstream UI submodule, alongside its existing local history hooks; preserve those
submodule edits with the frontend source. The UI dependency and frontend rebuilt
successfully (existing warnings only). Mouse input opened the visible account
menu and selected Connect MO2 instances.

The live Nexus browser flow completed successfully using the existing browser
session. `MO2_VERIFY_BROWSER_SSO=1` then independently exercised the real SSO
websocket through the original NMA popup: it observed the browser handoff and
receipt of the credential, confirmed native application to all 3 registered MO2
instances, and performed a separate native account readback. The check passed in
`frontend/artifacts/browser-sso-ui-check.log`. It logs only counts and pass/fail,
never the credential or SSO message bodies. This supersedes earlier notes that
the full browser-authorized roundtrip was unverified.

Rendered evidence: `frontend/artifacts/account-icon.png`, `account-menu.png`, and
`browser-sso-success.png`. The temporary UI check used an isolated layout; the
normal frontend was restored afterward. A separate native MO2 registration prompt
was dismissed with its No, don't ask again action. Linux's current nxm handler
remained `mo2-nexus-frontend-nxm.desktop`; broader routing regression coverage is
still part of the overall completion audit.

### Linux NXM desktop-entry quoting (2026-09-13)

The NXM installer now applies both escaping layers required for a quoted Exec
argument: command argument quoting, followed by desktop-entry string escaping.
Previously a wrapper path containing backslashes, quotes, dollar signs or
backticks could generate an invalid desktop entry. The GIO launcher reproduced
this as “Desktop file didn't specify Exec field” for a temporary path containing
those characters; the same launch passed after the fix.

`/usr/bin/python3 frontend/tools/check_nxm_handler.py` runs four isolated tests
using GIO's real desktop launcher and a fake runtime: normal and reserved-character
paths preserve the exact synthetic NXM URL as one argument, runtime failure is
reported without leaking its output or URL and is invoked only once, and restoring
a previous association respects a newer user selection. No real association or
Nexus download is changed by these tests. The existing `--check-nxm` game-routing,
ambiguous/stale-target, parser and signed-parameter checks passed too.

The corrected installer was run for the installed frontend. FNV and Skyrim route
preferences point to existing registered hosts, and Linux retains the frontend's
NXM desktop entry. These checks do not yet establish a fresh end-to-end native
download from a browser NXM click for both games.

Specification used: https://specifications.freedesktop.org/desktop-entry/latest/exec-variables.html


### Fresh NXM downloads for both games (2026-09-13)

Invoked the installed desktop handler using `xdg-open` with fresh unsigned file
links, under the existing authenticated Premium native accounts. FNV file
42507/1000001534 downloaded MCM BugFix 2 (18,141 bytes); Skyrim SE file 32444/795955
downloaded the offsets archive (2,182,132 bytes). Official Nexus file metadata
supplied expected filenames/sizes. Each archive was absent before its trial,
appeared as a completed native download afterward, and passed `7z t`. Separate
post-download snapshots confirmed responsive hosts and identical mod/plugin data
to pre-download baselines. Neither archive was installed. The frontend remained
on its current game while the Skyrim link routed to the registered Skyrim host.

Durable evidence, including hashes and explicit scope:
`frontend/artifacts/nxm-live-two-game-downloads.json`. No credentials or signed
URLs are present. The earlier native host exits were not reproduced in these
trials, and no cause has been established. The observed pressure-vessel assertion
also appears in older successful startup logs, so it does not establish causation.

This proves fresh desktop NXM routing and native completion for both games with a
Premium account. It does not prove live free-account signed-link completion,
duplicate-download prompt handling, installation, or gameplay. The exact signed
query preservation remains covered by parser/desktop-launch tests only.


### Restoring multi-tab panels (2026-09-13)

Reproduced the Overwrite+Plugins startup crash in an isolated saved layout. The
upstream PanelView's ScrollChanged handler alternated overflow true/false while
viewport width was zero: the inline Add button changed extent 32→0→32 indefinitely.
A guard defers that decision until viewport width is positive. After rebuilding
upstream UI and MockHost, the same saved layout restored successfully. The live
`MO2_VERIFY_TAB_RESTORE=1` check switched both tab selections repeatedly at
1280/900/640px and confirmed selected-header visual state without a layout loop.
Log and screenshot: `frontend/artifacts/tab-layout-fixed.log` and
`frontend/artifacts/tab-layout-fixed.png`. No native mod mutations were required.


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
