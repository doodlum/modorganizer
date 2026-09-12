# MO2 host bridge

Copy the `nexus_frontend_bridge` directory into an MO2 instance's `plugins`
directory and restart MO2. This is a normal `mobase.IPluginTool` extension; MO2
continues hosting its existing game, installer and other extensions. It has been
loaded by the installed Windows MO2 2.5.2 under GE-Proton10-4 on Linux.

The Tools menu entry displays the endpoint directory, normally
`plugins/data/frontend-bridge`. The native frontend can read it through the
corresponding Linux path:

```sh
.tools/dotnet/dotnet frontend/MockHost/bin/Debug/net9.0/MockHost.dll \
  --mo2-bridge-snapshot /path/to/mo2/plugins/data/frontend-bridge
```

The JSON mailbox uses protocol 1 and a new session UUID on every host startup.
Write each request atomically to `requests/<uuid>.json` with `protocol`,
`session`, and `action`; read the matching response in `responses`. Supported
actions are `snapshot`, `setModActive`, `setModPriority`, `setPluginActive`, and
`setPluginPriority`. Mutations require the exact `profilePath` from the latest
snapshot, `name`, and either boolean `enabled` or integer `priority`. These call
the existing MO2 list APIs on its UI thread. Responses include host state after
the operation, which may differ from the requested state due to host rules.

Plugin states and mod state flags are the host API's numeric values. Plugin
`priority` and `loadOrder` are distinct; inactive plugins may have load order -1.
The bridge makes no assumptions about activation files or game-specific rules.
`instance.name` is nullable because MO2 2.5.2's Python API lacks `instanceName`.

Requests are serviced only after MO2's UI initialization callback. A timeout
does not establish that a mutation failed: reconnect and refresh before retrying.
Responses retained while a request exists prevent duplicate execution of that
request. This is a local extension endpoint with the same access as the user's
MO2 files; it is not a network service.

Validation: nine Python contract checks (`python3 frontend/tools/check_bridge.py`)
cover authoritative state, mutations, stale profile/session rejection, invalid
values, older-host metadata and mailbox replay. A C# client read a real isolated
New Vegas `Frontend Test` profile under Proton: nine DLC mod entries and ten
plugins. A live plugin activation and restoration also succeeded through the
host API. These are bridge checks, not evidence of a modded game launch.

The live visual frontend is available through
`frontend/run-live.sh /path/to/mo2/plugins/data/frontend-bridge`. It displays
the host profile's mods and priorities on the left and the original NMA plugin
order editor on the right. It polls MO2 every two seconds and sends changes to
MO2, then displays the response. No profile files are written by the frontend.
The mod table uses MO2 columns, without NMA collection or installation-date
semantics. Essential content has no activation toggle.

With `MO2_VERIFY_LIVE=1` and `MO2_SCREENSHOT=/path/to/image.png`, the live mode
checks the isolated `frontend/artifacts/mo2-fnv-host/profiles/Frontend Test`
profile, executes the native plugin row's move command, independently checks
MO2's resulting priority, and restores the original order. This check passed;
the rendered tables contained nine real mod rows and ten real plugin rows.
It does not install a mod or launch FNV.

On this Steam Deck, MO2/Qt under GE-Proton10-4 crashed on Wine's unimplemented
`USER32.GetPointerFrameTouchInfo`. Setting `QT_QPA_PLATFORM=windows:nowmpointer`
inside a Windows `.cmd` launcher before starting MO2 avoided that failure during
the live checks. Setting it only in the outer Linux launch environment did not
work in the already-running prefix. Do not pass `-platform` to MO2: its command
parser treats that as a profile selection.

Configured host startup is connected and tested with the isolated FNV instance.
Cross-game startup is verified with the existing Skyrim instance; FNV gameplay and plugin-order acceptance are recorded in [FNV acceptance](../FNV_ACCEPTANCE.md). Richer plugin diagnostics remain incomplete. Nexus account access reuses MO2's credential and download workflow; no credential belongs in this
repository or in the bridge's diagnostic snapshots.

## Downloads and installation

The live header's Downloads button opens a native workspace tab. It lists files
from MO2's downloads folder and reads only relevant flags from each `.meta` file.
"Previously installed" is MO2's archive history, not membership in the current
profile. Partial files cannot be installed. Signed download URLs and credentials
are excluded from snapshots.

Paste a Nexus file URL containing `file_id` or an `nxm://game/mods/id/files/id`
link. The host calls `IDownloadManager.startDownloadNexusFile` for the current
game using its existing Nexus account. This ID-based path was tested with Premium
access; it does not forward free-account authorization parameters from nxm links.
The bridge rejects links for another game. Progress currently shows downloaded
bytes and partial/paused status; pause, resume, cancel and detailed errors still
need the corresponding MO2 runtime integration.

Install buttons and the local archive picker call `IOrganizer.installMod`.
MO2 selects its original installer plugin and presents any Qt dialogs. The
frontend waits for completion, then refreshes MO2's mod/plugin lists. Nested Qt
timer events cannot repeat an in-flight request. A null installer result means
cancelled or failed, not successful installation. Installer calls allow up to
30 minutes; a timeout still requires checking the host before retrying.

Live evidence: MO2 downloaded the original MCM archive (Nexus mod 42507, file
105803) and its XML FOMOD edition (file 1000164772). The original archive returned
without a mod. The XML edition opened the existing FOMOD installer and installed
MCM's ESP, NVSE DLL and menu files into MO2's mod directory. An isolated Qt test
driver pressed the native Install button with its default choices after Wayland
pointer automation proved unreliable. That driver is not in the distributed
extension. A profile temporary-file error occurred before restarting the test
host, then did not recur during the successful installation.

The frontend's native mod toggle enabled MCM, its ESP appeared in the right
panel, and MO2 saved the entries in `modlist.txt`, `plugins.txt` and `loadorder.txt`.
This proves download/install/activation persistence, not in-game MCM operation.
`check_downloads.py` adds three tests for archive metadata, game validation,
native downloader/installer routing and partial-file rejection. The bridge tests
also cover reentrant polling during installer dialogs.

## Profiles across instances

My Loadouts reads the active host's instance, `~/ModOrganizer2`, conventional
`~/Games/*/modorganizer2` directories, and user-added instances. Add MO2 instance
stores only connection paths in `$XDG_CONFIG_HOME/mo2-nexus-frontend/instances.json`
(defaulting to `~/.config`). Game/profile content remains in MO2. Multiple
instances of the same game stay distinct, with their instance directories shown.
Unreadable instances report errors without hiding other instances.

Selecting a profile connects to that instance's bridge and checks that the host
owns the exact profile directory before changing anything. MO2 2.5.2 lacks the
newer Python profile-management API, so `profiles.py` uses its named `profileBox`
control and `actionAdd_Profile` action. MO2 saves the old profile, selects the new
one, and performs its usual refresh. The bridge waits for `onNextRefresh` before
returning mod/plugin state, and rejects edits while that refresh is pending.
Manage current instance's profiles opens the original MO2 dialog, including its
create/copy/rename/delete actions and local-save/INI options.

The live test copied `Frontend Test` to `Frontend Clone Test` through MO2's
manager, switched to the copy, disabled MCM, and switched back and forth. The
original stayed enabled, the copy stayed disabled, and MO2 wrote those independent
states to each profile's mod list. The test restored `Frontend Test` as active.
An isolated Qt test driver supplied the copy name in the native dialog; it is
not part of the distributed bridge. The rendered catalog also showed both
existing Skyrim SE instances. Cross-game connection requires a running bridge
in each selected instance. Configured host startup is supported; automatic
bridge installation remains pending.

`MO2_VERIFY_PROFILES=1` with a screenshot checks that isolated copy/switch flow;
the native manager must be completed if the copy does not exist. `MO2_SHOW_PROFILES=1`
opens the catalog for a read-only screenshot. These checks do not launch a game.

## Nexus account

Import an existing key file into the running host's credential store:

```sh
.tools/dotnet/dotnet frontend/MockHost/bin/Debug/net9.0/MockHost.dll \
  --mo2-import-nexus-key /path/to/mo2/plugins/data/frontend-bridge /private/key.txt
```

The mailbox contains only the path. The host reads the file and uses Windows
`CredWriteW` with MO2's existing `ModOrganizer2_APIKEY` target, UTF-16 blob and
local-machine persistence. It checks the stored value without returning it.
Restart MO2 afterward so its existing Nexus access manager loads the key.
On Linux the command maps the file through the same machine's Wine `Z:` drive.
The account belongs to that Windows/Proton prefix; other prefixes are separate.

Credential import does not validate the key with Nexus or claim that downloads
work. The supplied test key was separately validated against Nexus's official
API, then credential import/readback succeeded in Windows MO2 2.5.2 under Proton.
After restarting, original MO2 displayed the account in its window title and
populated its Nexus API quota indicator, confirming it loaded the stored key.
The contract test checks path-only dispatch and stale-session rejection; the
Windows credential implementation additionally requires the live host check.

## List controls

Live mod rows retain the native activation toggle. Select one mod and use
Move earlier/later to change its MO2 priority. The native plugin ordering editor
retains its row arrows and drag/drop; Enable selected and Disable selected
change plugin activation independently of its parent mod. The host can enforce
forced content and ordering rules; the frontend always displays its returned state.

`MO2_VERIFY_CONTROLS=1` (with live endpoint and screenshot mode) exercises the
actual toolbar click handlers in the isolated FNV profile: disables MCM's ESP
while keeping its mod enabled, moves its mod earlier, checks both using an
independent bridge client, then restores original activation and complete mod
order. This runtime check passed. It does not establish in-game loading.

## Launch through MO2

The live header lists MO2's configured executable titles. Run through MO2 passes
that title to `startApplication`, preserving host arguments, working directory,
Steam ID and USVFS setup. `waitForApplication` owns handle cleanup and refresh;
frontend editing is disabled until the host returns. A returned exit code is
process evidence only, not proof that gameplay or a mod worked.

`MO2_VERIFY_LAUNCH=NVSE` with live endpoint and screenshot mode launches only in
the isolated FNV test profile and reports the host result when it exits. The
first run reached xNVSE 6.4.8 and its runtime log reported MCM Extensions loaded
correctly through the virtual filesystem. The game exited before gameplay.
After original Fallout Launcher setup of the fresh test prefix, the next run
reached the game title screen. In-game acceptance is still outstanding.

After the missing prefix game directories were initialized and MO2 restarted,
the profile's windowed settings took effect. A black-screen startup stall
occurred with MCM both enabled and disabled. The disabled run's xNVSE log omitted
MCM, and the original profile was restored afterward. This remains incomplete
gameplay validation; see `MO2_INTEGRATION.md` for the current acceptance status.

`MO2_VERIFY_CATALOG=1 MO2_VERIFY_CROSS_GAME=/path/to/skyrim-instance` in screenshot
mode starts from the registered isolated FNV profile, connects the Skyrim host,
compares both panels and game-specific context against an independent snapshot,
and restores the FNV connection. `MO2_CROSS_GAME_SCREENSHOT=/path.png` optionally
captures the Skyrim panels before returning. The check performs no mod toggles
or reordering in Skyrim; MO2 may reconcile newly detected game content at startup.

## Transfer controls

Pause, Resume and Cancel in the Downloads tab route to the original
DownloadListView slots. The bridge looks up downloadPath for each source-model
row at execution time and matches the requested archive (including .unfinished),
so UI sorting and filtering cannot change the target. MO2 validates the current
transfer state; the frontend reports a request and then shows host metadata.
No signed URL or private metadata is copied into the snapshot.

`MO2_VERIFY_TRANSFERS=1` in live screenshot mode exercises these actual buttons
against a throttled localhost transfer in the isolated FNV instance. It requires
the isolated native test driver and local HTTP fixture; it does not make a Nexus
download. This runtime check passed, including preservation of other archives.

## Uninstall

`removeMod` requires the active profile and a current mod name. It calls the
original `ModList.removeRow` path, which owns confirmation and cleanup, and
reports whether the mod actually disappeared. Essential content is rejected.
The frontend disables editing while the native dialog is open and stops a
multiple selection sequence if MO2 keeps a mod.

`MO2_VERIFY_UNINSTALL=1` in live screenshot mode exercises the native frontend
row command with a disposable `Frontend Uninstall Verification` mod in the
isolated FNV instance. It requires a narrow test-only dialog responder. Both
Cancel and Confirm passed, including file/profile cleanup and preservation of
other mods and archives. The responder is not part of the production bridge.


`manageProfile` accepts `name` and `operation` (`copy` or `remove`) with the
usual active `profilePath` and session guard. It targets the existing profile
in MO2’s original profile manager without activating it, then invokes the
original copy or removal control. MO2 owns the name prompt, confirmation,
profile copy semantics, save handling, and active-profile deletion restriction.
The manager remains open until the user closes it; then the bridge returns a
fresh host snapshot. No profile files are written by this adapter.

## Mod metadata and original details

Snapshots include Overwrite and the original mod model’s priority display,
conflict and status tooltips (converted to plain text). `showModDetails` invokes
the original mod-list conflict-column double-click handler, including the
special Overwrite file dialog. It uses the same instance/profile guard as other
actions and waits for the dialog to close before returning. Original MO2 filters
must allow the selected row to be visible.
