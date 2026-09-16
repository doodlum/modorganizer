# Integration acceptance audit — 2026-09-13

For the current goal checkpoint, see [CURRENT_STATUS.md](CURRENT_STATUS.md).
This file contains chronological evidence; older completion statements are historical.

Follow-up navigation and Tools changes are documented in [NMA_FIDELITY.md](NMA_FIDELITY.md). They replace the earlier per-profile game icons and Rules tab with one icon per game, Profiles, Plugins under Installed, and Tools under Utilities. Downloads now uses NMA's native DownloadsPageView. The new live checks cover mod priority restoration, filters, original tools, downloads, and FNV/Skyrim switching. Earlier evidence below remains historical evidence for unchanged backend functionality.


The earlier local integration audit verified an MO2-owned frontend with NMA
navigation/panels for the registered FNV and Skyrim instances. It did not prove
the full active goal: shared popup login, system NXM routing, and BSA browsing
require the follow-up evidence below. The requirement evidence below includes functional
checks, original extension behavior and the final fork comparison. It does not
claim universal third-party extension compatibility or a native Linux MO2 port.

## Requirement evidence

| Requirement | Current implementation and evidence | Audit result |
| --- | --- | --- |
| Use the FNV-enabled NMA fork as the reference | `reference-fnv` is commit `3b0244e7d21ea19afabcc3d81539c9e11ae8d642`; its FNV comparison screenshots remain in `artifacts/reference`. The current shared-component comparison is recorded at the top of NMA_FIDELITY.md; six of thirteen checked files are identical, with seven deliberate code differences documented there. | Reference identity and panel source reuse verified. |
| My Games/My Loadouts represent MO2 instances/profiles | `Mo2InstanceCatalog`, `Mo2HomePages` and `Mo2ProfileFiles` read actual MO2 instances. Card operations call native profile management. `check_profile_reader.py` passed again with multiple instances, overridden paths, duplicate names and missing/unmanaged entries. Native card/create screenshots and runtime verifiers remain available. | Implemented; original profile actions and cross-game navigation have runtime evidence. |
| Mods left, plugins right, using one MO2 profile | `Mo2LiveWorkspace.CreateProfileWorkspace` creates the two native panels; `Mo2LiveProfile` applies host snapshots to both adapters. Native mod activation, plugin activation/order, pointer dragging and priority changes have passed. `external-profile.png` shows the current FNV arrangement with 12 mod rows and 14 plugins. | Implemented and exercised with FNV and Skyrim. |
| Original MO2 and the frontend share state | Independent native profile-selector, mod-priority and plugin-state requests are detected by ordinary polling. Workspace, spine, caption and both lists follow MO2; both profiles restore unchanged. Explicit original-UI show/hide reveals the same host and profile. | Verified by the external-profile and original-UI checks. |
| MO2 owns archives, transfers and installation | `Downloads` reads the MO2 directory/metadata and calls its downloader, transfer slots and `installMod`. Original MCM downloads/FOMOD installation and transfer-control evidence remain available. Current download-context checks reject stale picker/row targets and hide stale rows on unavailable snapshots. The three download contract checks pass. | Implemented; runtime evidence covers install, transfers and profile boundaries. |
| No independent NMA Library or deployment authority | Live pages use MO2 adapters. Library-style membership is the MO2 downloads folder. Collection/Apply controls are hidden or disabled; the native UI's database service is in-memory presentation infrastructure. Persistent frontend files contain registrations and workspace presentation only. | Source review supports the ownership boundary. |
| Sidebar, game icons and panel behavior match the reference | Live Home and profile sidebars use native views; Home items leave the game workspace. Spine/loadout art uses square game icons and My Games uses covers. Native divider dragging, tab/panel actions, history and per-profile layout restoration have runtime evidence. | Final fork/page comparison passed, including Home, profile navigation, paired lists, Health Check and downloads; intentional MO2 mappings are recorded in NMA_FIDELITY.md. |
| Health Check reports MO2 diagnostics | Native Notifications refreshes enabled diagnostic extensions; list/details show the host's reports. Unavailable, zero-report, populated, resolved/recurring, cross-game and restored-detail states have passed. `health-restart-read.png` remains available. | Implemented and exercised, including a real original-API diagnostic extension. |
| Preserve original extensions with minimal changes | The original MO2/Qt/Python host remains intact under Proton. Existing game, installer, tool and DDS-preview extensions work; original DDS settings survive restart and restore through MO2. `preview-settings-result.json` is `done`, with original RGBA restored. | INI mapping, native Run callback identity and Unlock passed. Original FNIS game requirements and master/child enable rules also passed in FNV and Skyrim, with original states restored. |
| Keep unsolicited MO2 UI hidden | Hidden-host startup suppresses main/splash/toast windows. Explicit original UI and native tool/preview dialogs remain usable. Hidden-versus-normal INI Editor reports agree on dialog structure. | Implemented with runtime evidence; temporary verifier plugins are removed after checks. |
| Successfully load FNV with mods | `FNV_ACCEPTANCE.md` records actual gameplay, MCM menus, enabled/disabled launches and all 14 in-game plugin indices. Current-sidebar PLAY evidence includes loaded gameplay, movement, pause menu and normal return to PLAY. The corresponding artifacts remain present. | Gameplay acceptance has passed; the updated original Run path also loaded the save with xNVSE/MCM, accepted movement and exited normally with code 0. |
| Do not lose actions during polling or cross profile boundaries | Thirteen controlled transport cases cover dialogs, PLAY and profile-card requests, duplicate suppression, stale/unavailable targets and missing card profiles. Real-host validation and native preview checks also passed. | Current command changes verified at transport and native integration boundaries. |

Evidence details and test limitations are in [MO2 integration](MO2_INTEGRATION.md),
[NMA fidelity](NMA_FIDELITY.md), [extension compatibility](EXTENSION_COMPATIBILITY.md)
and [FNV acceptance](FNV_ACCEPTANCE.md). The historical sections of those files
record intermediate states; their earlier “not implemented” statements are not
the current status of workflows subsequently verified there.

## Final acceptance checks

- The original FNIS tools are blocked by their game requirement in FNV and
  permitted in Skyrim. Native settings controls disable/restore the master and
  both children; child controls remain unavailable independently. Structured
  reports and original source hashes are in `extension-rules-{fnv,skyrim}-result.json`.
- The actual reference fork was reopened and its Home, mods, health and Library
  pages were inspected. Current Home pages show all registered profiles; FNV →
  Skyrim → FNV renders the correct paired lists, native health pages and MO2
  download folders. `final-pages-return.png` and `/tmp/mo2-final-pages-fixed.log`
  record the completed check.
- The comparison exposed and corrected empty NMA support-catalog presentation,
  leftover Apply instructions and Open Downloads switching into Home. Downloads
  now stays in the selected profile's workspace. The updated stale-action check
  passed against the real host: an old picker cannot install into a new profile,
  unavailable rows stay hidden and both profiles remain unchanged. See
  `/tmp/mo2-final-download-context-fixed.log`.
- The build passed. Native mods/health/panel/divider/workspace sources match the
  reference; Home AXAML differences are unused XML namespaces only. Original
  pinned UI sources and original MO2 extensions were not edited.

The final direct-endpoint restart exposed an empty-profile-path selection crash
before the first snapshot. The sidebar now leaves profile icons inactive until
MO2 supplies a path. The rebuilt ordinary app started successfully through
`MO2_BRIDGE_DIRECTORY`, connected to FNV and rendered both live lists.
`artifacts/final-direct-startup.png` was inspected; the running app remains open.

## Coverage limits

Additional LOOT categories and archive-preview file types remain broader test
coverage items; they must not be described as already exercised. Native Linux
MO2 and Skyrim gameplay are not prerequisites: Proton was accepted and FNV was
the explicit gameplay target. Remote publication is listed in the earlier README
but has no current authenticated publication evidence; local commits are not a
claim that the branch was published.

## Checks repeated for this audit

- Bridge contracts: 17 passed.
- Download directory/metadata contracts: 3 passed.
- Proton launcher contracts: 3 passed.
- Profile reader: passed when supplied the current dotnet/MockHost invocation.
- Five native panel/divider/workspace source files match the FNV reference fork.
- Referenced gameplay, profile, panel, health, download and extension artifacts
  were checked for presence; structured DDS-setting evidence confirms completion
  and restoration. Presence alone is not treated as a new runtime test.

The initial audit only reviewed evidence and repeated read-only checks. Subsequent
Run-path verification launched the native FNV launcher and modded game, then
restored the profile INIs; see the extension compatibility and FNV records. The
subsequent extension-rule and final page checks close the two remaining audit
items. No requested local integration work remains in this audit; the coverage
limits above are not claims of additional completed tests.


## September 13 UI follow-up

Added Overwrite and live Logs game panels, native tool icons and pinned footer
shortcuts; separated Home/game page choices and fixed the disabled-workspace
input path. Mods/Plugins game icons share proportional foregrounds with blurred
rectangular backgrounds. The sidebar/panel label is now Mods. The NMA top bar,
Home logo and Downloads icon are retained without a second window title bar.

The real-pointer check passed for panel selection/highlighting, plugin toggle
and restoration, game-scoped Profiles, tool pins, Logs and Overwrite navigation.
It also passed stopped-host detection and FNV/Skyrim switching. Original native
Overwrite create/move/clear prompts were cancelled without changing the isolated
marker; sync and complete destructive outcomes are not claimed as tested.
See the final section of NMA_FIDELITY.md for evidence and coverage details.


## Active goal re-audit: open requirements

The prior goal turn made progress (commit 203934d6 and real pointer/native prompt
checks). Completion of the broader goal remains unproven.

- Shared NMA-style popup login: incomplete. The account button currently opens
  one MO2 instance's native settings, and credential import is per Proton prefix.
- System NXM routing: installed and exercised through `xdg-open`. The default is
  `mo2-nexus-frontend-nxm.desktop`, with the prior Vortex handler retained. The
  existing MCM Guide URL reached the FNV host's original Download again? prompt;
  it was cancelled without another transfer. Signed query preservation and
  malformed/cross-game/ambiguous/stale route rejection pass contracts. A real
  free-account signed download has not been exercised; no broader account claim
  follows from the Premium/duplicate-prompt handoff.
- BSA/archive browser: original mod-file/preview extension evidence does not prove
  a discoverable BSA browser with the expected browsing actions. Still to audit
  and implement/test as needed.
- Useful MO2 feature parity and responsive panel layouts: retain the verified
  Mods, Plugins, priority/filter/conflict, profiles, downloads, tools, Overwrite,
  Logs and Health Check work. Audit remaining original tabs/actions against the
  actual frontend rather than treating that list as exhaustive parity.
- FNV NMA design comparison: existing source and rendered evidence must be checked
  for the new popup and pages as they are added.


## Current continuation status: broader goal remains active

The preceding goal turn made concrete progress: real pointer grip drags were compared
with the running FNV fork's Rules tab, and both native orders were changed/restored.
Subsequent user steering supplied a new Mods table design and requested a four-line,
non-clipping header fade and removal of status/Plugins overflow dropdowns.

The old open-items list above is historical: shared browser SSO and all-instance
native credential confirmation now have runtime evidence, and the Archives browser
and extraction actions are implemented. Complete Overwrite operation outcomes,
broader archive coverage, LOOT sorting, and remaining useful MO2 actions still require
requirement-level verification. The new native sort dispatch is not yet evidence of
a successful LOOT run. Do not treat this document's older completion language or
source-identity statements as a current completion claim; the bounded upstream panel
and account fixes are now documented in README/MO2_INTEGRATION.

### Overwrite outcome verification — 2026-09-13

The running frontend's Overwrite panel was exercised with physical pointer and
keyboard input against the isolated FNV `Frontend Test` profile. Its Overwrite
folder was confirmed empty before creating a controlled text fixture.

- **Create mod:** clicked the frontend action, named and accepted the native MO2
  dialog, then verified exact file bytes in the new mod and an empty Overwrite.
- **Move to mod:** added a second controlled file, refreshed the frontend, clicked
  its action and selected the temporary mod in MO2. Verified exact destination
  bytes and an empty Overwrite.
- **Clear:** used a fresh controlled file in the otherwise empty Overwrite folder.
  Cancelling the native confirmation preserved its exact bytes. Reopening Clear
  and accepting removed it; Overwrite became empty and native mod/plugin records
  remained identical to the pre-test snapshot.
- **Sync to mods:** created and enabled a temporary mod through native MO2, then
  placed a different version of the same file in Overwrite. The frontend action
  opened the native Sync Overwrite dialog, which identified the owning mod.
  Accepting replaced the destination with the exact generated bytes and emptied
  Overwrite. The temporary mod was removed through native confirmation and full
  mod/plugin records matched the baseline. See `artifacts/overwrite-sync-dialog.png`.
- **Restoration:** removed only the temporary mod through MO2's confirmed removal
  action. Complete native mod and plugin records match the pre-test snapshot;
  the temporary mod directory is absent and Overwrite is empty.

Evidence: `artifacts/overwrite-workflows-check.json`. These checks establish
successful create/move/clear/sync outcomes on FNV, beyond the earlier cancelled-dialog
checks. Move collision handling and equivalent Skyrim outcomes remain
unverified; the broader parity goal is still incomplete.

The same run exposed stale disconnected messages in Archives. Archives and
Overwrite now refresh on both connection transitions and recheck a transition
that occurs during an outstanding read. A controlled 22-second pause of the
same isolated FNV host produced the disconnected state in both panels; resuming
that process restored the archive count (21) and empty Overwrite automatically,
without frontend navigation or Refresh clicks. See
`artifacts/overwrite-archives-disconnected.png` and
`artifacts/overwrite-archives-reconnected.png`. The frontend build passed with
three existing upstream warnings and no errors.
### Skyrim archive workflow — 2026-09-13

Switched from the FNV workspace to Skyrim through the game sidebar and opened
Archives. The selected legacy Skyrim MO2 instance reported 125 archives. Physical
search and row selection opened `Skyrim - Misc.bsa` in the original BSA preview,
which reported version 105 and 14,031 files. The frontend Extract action opened
MO2's destination picker; selecting a temporary folder with a Windows-form path
completed extraction.

An independent read of the BSA folder/file records verified all 14,031 extracted
relative paths and every file's exact bytes. Full native Skyrim mod/plugin records
were unchanged. Temporary extracted output was removed after verification.
Evidence: `artifacts/skyrim-archives-preview.png` and
`artifacts/skyrim-archive-extraction-check.json`. This establishes successful Skyrim
BSA browsing and extraction in addition to the earlier FNV outcome; it does not
prove BA2 or every compressed archive variant.
### Native LOOT workflow — 2026-09-13

FNV's original MO2 2.5.2 Sort button was inspected directly and remained disabled
after native Refresh, matching the bridge capability. Its main window was hidden
again after inspection; a supported FNV sorting outcome remains unproven.

For Skyrim, copied Default through MO2's native profile manager to a temporary
profile, selected it, and physically clicked the frontend's Sort with LOOT button.
Accepted the native confirmation. LOOT 0.23.0 / lootcli 1.6.0 produced a report for
149 plugins with one warning about an SSE Engine Fixes requirement. After closing
the completed native workflow, 65 native plugin priorities had changed and plugin
activation was unchanged. The native Default profile was selected again and its
original mod records, plugin order and activation verified. The temporary profile
was removed through MO2's confirmation, then the frontend returned to FNV.

Evidence: `artifacts/skyrim-loot-result.png`, `artifacts/skyrim-loot-check.json`.
This supersedes the earlier statement that LOOT dispatch alone had been tested.
FNV sorting follow-up: the bridge now exposes the original disabled Sort button's
tooltip, and the frontend keeps Sort visible with that explanation. The restarted
native host reports: “There is no supported sort mechanism for this game. You
will probably have to use a third-party tool.” This identifies the game-extension
capability as the reported limitation; it is not a frontend click-dispatch failure.
The original window is hidden and FNV native mod records, plugin priority and
activation match the pre-inspection baseline. Frontend build and all 26 bridge
contract checks passed.

The cause is now identified: the official FNV extension's `enable_loot_sorting`
setting defaults to false; its `sortMechanism()` returns NONE until opted in.
Source: https://raw.githubusercontent.com/ModOrganizer2/modorganizer-game_falloutnv/master/src/gamefalloutnv.cpp
The bridge now reads that setting through the original organizer API and explains
the opt-in path in the disabled button tooltip, retaining the extension's default
and manual ordering. Live verification received this specific explanation with
sorting still disabled and complete mod/plugin records unchanged. This is an
intentional native preference, not a missing FNV sorting implementation. The opt-in
FNV LOOT outcome itself has not been exercised.

### Hidden-host protocol registration — 2026-09-13

Deployed the startup preference to all three native instances and restarted them.
Each responded to a fresh snapshot with identical profile, complete mod records,
and complete plugin records (compared independently of response array order).
All native main windows were unmapped. Each selected portable helper INI had
`General/noregister=true`; hashes of every other parsed setting matched the
pre-restart baseline. Linux's NXM association remained
`mo2-nexus-frontend-nxm.desktop`.

An existing registration dialog belonged to the old legacy helper PID 100603,
not a restarted host. Physically choosing “No, don't ask again” ended that helper;
subsequent window inspection found no registration dialog. Rechecked settings
hashes after dismissal, with all handler mappings still preserved. All 27 bridge
contract checks passed, including portable/global selection and retaining legacy
INI migration. Evidence: `artifacts/quiet-protocol-check.json` and native window
inspection. This verifies registration-prompt handling; it does not constitute
a new end-to-end NXM download test or completion of broader MO2 parity.

### Merged Data browser — 2026-09-13

Added a game-only Data page to the sidebar, page factory and persisted workspace
catalog. The bridge reads one directory with `listDirectories` and `findFileInfos`,
retaining native winner-first origins and archive metadata. Relative-path validation
rejects traversal and absolute paths. The frontend provides folder navigation,
parent navigation, current-folder search, conflict filtering and source tooltips.
It guards profile changes and refreshes after connection transitions.

All three deployed hosts returned real Data roots: FNV 52 entries, legacy Skyrim
315, and the other Skyrim instance 313. Both Skyrim roots reported four files with
multiple providers. Full native profiles/mod/plugin records matched the pre-restart
baseline. Physical frontend folder selection and Open navigated to `NVSE/Plugins`
and displayed MCM.dll with The Mod Configuration Menu as its provider. Search for
an absent filename returned zero entries, and the conflicts-only option excluded
that single-provider file. The frontend build passed with the existing telemetry
package warning; 28 bridge checks passed. Evidence:
`artifacts/data-browser-check.json` and `artifacts/data-browser-live.png`.

This closes the missing merged-directory browsing gap, but does not establish full
native Data-tab parity: file preview, hide/unhide, shell operations and recursive
search are not yet provided. Native Data archive metadata depends on MO2's current
archive parsing configuration. These remain limits of this implementation.

Data preview follow-up: Open and double-click now dispatch MO2's native Preview
action for files, resolving nested tree rows and restoring temporary expansions.
The bridge rejects a missing file or changed provider list before dispatch.
Physical folder navigation from the Steam Deck lower split panel opened
`Config/MCM.ini` in MO2's installed text preview with the correct contents and
The Mod Configuration Menu attribution. Closing it restored frontend controls;
native profile/mod/plugin records were unchanged and the main MO2 window stayed
unmapped. Evidence: `artifacts/data-native-preview.png`.

The shared preview change also opened `Fallout - Misc.bsa` successfully and showed
142 files in the existing BSA extension. The diagnostic caller's 30-second wait
expired while that dialog was open; after Close, its response completed with
`opened=true` and the same host answered a fresh snapshot. Both Skyrim deployments
match source and preserve full profile state after restart. Build and 29 contract
checks pass. This supersedes the missing-preview limitation above; hide/unhide,
shell operations, recursive search, and cross-format preview coverage remain open.

Data visibility follow-up: added a compact native Hide/Un-Hide action for loose
files, with provider validation and archived-file rejection. The native Data
tree temporarily includes hidden files during dispatch, then restores its prior
filter. The native rename can finish before the merged-directory rebuild, so
the bridge observes the listing change for up to five seconds before returning;
this avoids the immediately stale row seen in the first live hide check.

Physical Hide on a uniquely named temporary Overwrite text file produced the
`.mohidden` file with identical bytes. Physical Un-Hide restored the original
filename and bytes; the frontend updated automatically. The temporary file was
removed afterward. Evidence: `artifacts/data-hidden-file.png` and
`artifacts/data-unhidden-file.png`. Build and 29 bridge checks passed, including
invalid-action, stale-provider and archived-file rejection. Collision-dialog
outcomes and hide/unhide on the Skyrim hosts remain unexercised; this does not
prove every native Data operation or complete the broader parity goal.

Final deployment check: all three running hosts match the updated source. Skyrim
profile state matched each pre-restart snapshot. FNV matched the clean baseline
from before the temporary fixture, including the cleared Overwrite hidden-file
flag; neither fixture filename remains in its merged Data root. See
`artifacts/data-visibility-check.json`.

Data-to-Mods selection linking: selecting a file now highlights its native winning
provider in visible My Mods panels, matching the source-link behavior in MO2's
DataTab. Highlight ownership is scoped to the selecting view and profile; another
Data view cannot clear it. Navigation, refresh and deactivation release that view's
highlight. No native mutation or extra bridge request is needed for selection.
Physical selection of `Config/MCM.ini` highlighted The Mod Configuration Menu in
the adjacent panel; navigating to the parent cleared the highlight. Restored the
unfiltered mod list after the check. Build passed with the existing telemetry
warning. Evidence: `artifacts/data-source-highlight.png` and
`artifacts/data-source-highlight-cleared.png`. Multiple simultaneous Data views
and profile-switch highlight behavior have code guards but were not exercised
in this visual check.

Data refresh follow-up: the live profile now tracks changes to its mod/plugin
snapshot, and Data observes that revision as well as profile and connection
transitions. A revision arriving during an outstanding directory read schedules
another read when it finishes. This fixes the previous stale file list after
enabling/disabling mods. Physical input disabled MCM while Data/Config was open:
the file list automatically changed from one entry to zero. Re-enabling MCM
restored MCM.ini without Refresh or navigation. Complete native mod/plugin records
and the selected profile matched the saved pre-test snapshot afterward. Restored
the unfiltered Mods list and Data root. Build passed with the existing telemetry
warning. Evidence: `artifacts/data-mod-disabled.png` and
`artifacts/data-mod-restored.png`. This test covers mod activation changes, not
unreported external filesystem changes or every asynchronous rebuild duration.

Archives refresh follow-up: Archives now observes mod/plugin content revisions,
including revisions arriving during an outstanding read. In the legacy Skyrim
workspace, physically enabling the initially disabled SkyUI_SE.esp changed
SkyUI_SE.bsa's displayed Loaded status from No to Yes without refreshing Archives.
Disabling the plugin restored No automatically. Full native mod/plugin records
and the selected profile matched the baseline afterward. Cleared the test search,
restored Skyrim's Mods/Plugins arrangement, and returned to FNV. Build passed with
the existing telemetry warning. Evidence: `artifacts/archive-plugin-enabled.png`
and `artifacts/archive-plugin-restored.png`.

MO2-style layout preset: added a native-styled toolbar menu beside NMA's panel
controls. It creates two equal columns: My Mods left; Plugins, Data, Archives and
Downloads tabs right. Undo stores the previous serialized workspace arrangement,
including selected tabs and existing Mods/Plugins search state, scoped to that
workspace for the current session. Both operations use the existing validated
layout restoration and persistence path; Home cannot apply the preset.

Physical menu input applied the preset at 1280×750. The persisted workspace
contained one left tab and four right tabs; selecting Data displayed the real
52-entry FNV root. Undo restored the three-panel arrangement, and the entire saved
layout JSON matched the pre-test baseline exactly. Build passed with the existing
telemetry warning. Evidence: `artifacts/mo2-layout-preset.png`,
`artifacts/mo2-layout-data-tab.png`, and `artifacts/mo2-layout-undo.png`.
This adds a usable starting layout; it does not supply the still-missing native
Saves panel or establish full MO2 parity.

Saves browser follow-up: added a game-only Saves page to the sidebar, panel
catalog, persisted layouts and MO2 layout preset. Its bridge refreshes and reads
the original native Saves tab, restoring the previously active tab, selection and
scroll position afterward. This delegates save-directory overrides, local saves,
sorting and game parsing to MO2. The frontend provides search and complete entry
tooltips with compact-panel margins and the shared responsive header behavior.

All three deployed hosts responded: FNV reported two real saves with character,
level and location descriptions; each Skyrim instance reported zero saves for its
active Default profile. Complete profile/mod/plugin records were preserved across
deployment. Physical sidebar navigation displayed both FNV saves in the Steam Deck
lower split panel. Build and the existing 29 bridge checks passed; those contract
checks do not independently exercise the new native Qt save reader.
Evidence: `artifacts/saves-browser-check.json` and
`artifacts/saves-panel-compact.png`. This supersedes the missing Saves-page note,
but native save details, missing-plugin repair and deletion remain unfinished.
Physical search for `integration` matched the integration-test filename and
reduced the two-entry list to one (`artifacts/saves-search.png`); cleared the
search afterward.

Save actions follow-up: connected Fix enabled mods and Delete to the original
native Saves context menu. Dispatch rechecks a unique current filename and the
current profile, selects only that save, and restores prior native tab/selection
afterward. Native dialogs own repair choices and deletion confirmation.

Copied the existing FNV integration-test `.fos` and `.nvse` files to unique temporary
names. Physical Delete opened a confirmation naming only the temporary save.
No preserved both copies and all original save hashes. A second Delete followed
by Yes removed both temporary files; every original save still matched its
baseline hash. Evidence: `artifacts/save-delete-confirm.png`.

For repair, temporarily disabling just the plugin did not leave a repairable
missing-mod condition by dispatch time. Disabling the whole MCM mod did: physical
Fix enabled mods opened Activate Mods with The Mod Configuration Menu.esp and its
source mod. Accepting restored the complete baseline mod/plugin records and
selected profile, while all original save hashes remained unchanged. Evidence:
`artifacts/save-repair-dialog.png`. This verifies actual native repair and deletion
outcomes on FNV; native save detail widgets and equivalent Skyrim outcomes remain
unverified. Build and the existing 29 bridge checks passed.

Save details follow-up: the Details action embeds the game extension's own
ISaveGameInfoWidget in a scrollable, screen-bounded dialog. It resolves the save
directory using the same local-profile, first-game-INI SLocalSavePath, and default
directory rules as native SavesTab, then revalidates the native filename against
the game extension's parsed saves. Contract coverage now includes local-save
precedence, INI override, empty override, absent INIs and absent LocalSavegames
support (30 bridge checks pass).

The installed plugin_python wrapper exposes the underlying QWidget through
`_widget()`, which must be passed to Qt's scroll area while retaining the MOBase
wrapper. Reference: https://github.com/ModOrganizer2/modorganizer-plugin_python/blob/master/src/mobase/wrappers/wrappers.cpp
Physical frontend Details opened the FNV integration-test save with its screenshot,
character, level, location, date, script-extender indication and “Missing ESPs:
None.” The dialog fit the Steam Deck screen and closed normally. Original save
hashes and complete profile/mod/plugin records were unchanged. Evidence:
`artifacts/save-details-native.png`. Build passed with the existing telemetry
warning. This verifies FNV details; Skyrim's active profiles have no saves, so
their detailed widgets remain unexercised.

During deployment both Skyrim hosts were found absent after read timeouts;
their supervising processes were also absent and prefix locks were free. Started
them with the updated bridge, then verified fresh snapshots and preserved profile
state in both instances. A timeout alone was not treated as evidence of exit.

Save-action feedback: SaveAction now returns its failure message to the requesting
view. Saves keeps action feedback separately from list-read/connection status,
so the reconnect refresh triggered by error reporting cannot erase the result.
Feedback is scoped to the original profile and clears on explicit Refresh, a new
action or a profile change. Physical Repair on a valid FNV save displayed
“MO2 reports no repairable missing mods for this save” after normal connected
status had returned; Refresh cleared it. Evidence:
`artifacts/save-repair-feedback.png`. Build passed with the existing telemetry
warning. The broader classification of native action rejection versus connection
failure is unchanged and remains a potential source of unnecessary reconnects.

Native rejection follow-up: correlated `ok:false` responses now carry their
action name in Mo2BridgeCommandException. Report preserves the last known
snapshot for command rejection; snapshot rejection, identity mismatch, timeout
and I/O failure still invalidate the connection. Normal polling remains responsible
for observing state after rejection, including any partial native change.
`--check-bridge-errors` exercises actual response parsing with isolated endpoint
files and checks Report's connection state and feedback. It also verifies that
rejection cannot fabricate a connection when no snapshot exists. Build passed
with the existing telemetry warning. The updated running frontend's physical
Repair action displayed the expected “no repairable missing mods” feedback;
explicit Refresh cleared the panel feedback. Evidence:
`artifacts/save-rejection-connected.png`. The screenshot verifies visible feedback;
the connection-state checks prove rejection classification, not general host stability.

Data selection and feedback follow-up: selection is remembered by name and entry
kind within the current directory/profile. Rebuilding the filtered rows restores
the matching selection and current source highlight; filtering out the row clears
the visible selection/highlight without forgetting it. Navigation and profile
changes clear the remembered selection, and a successful refresh removes a
selection whose entry disappeared. Data actions now return failure feedback to
the view, which retains it separately from directory read status and scopes it
to the original profile and directory.

Physical FNV checks selected CaravanPack.esm, filtered it out with a nonmatching
search, cleared search and refreshed: the row remained selected after both
restorations. Its unsupported Preview displayed the native error after the
directory reload. Evidence: `artifacts/data-search-selection-restored.png`,
`artifacts/data-refresh-selection-restored.png`,
`artifacts/data-action-feedback.png`. Build passed with the existing telemetry
warning. These checks used read-only preview rejection and do not add new hide/
unhide mutation coverage or cross-profile selection runtime coverage.

Persistent NMA help: the live settings adapter now persists only upstream
AlertSettings in the frontend XDG configuration directory. Fixtures retain their
in-memory settings, and no MO2 settings are persisted through this adapter.
Writes use a unique temporary file followed by replacement. Missing preferences
use upstream defaults; malformed/unreadable preferences and failed writes report
a diagnostic without crashing the UI. These failure paths were source-reviewed,
not runtime fault-injected.

Physical Plugins “Got it” wrote the original NMA alert key as dismissed. After
restarting the frontend, the small Plugins panel showed its rows without the
help message. The question-mark button reopened the original help, and Got it
dismissed it again. Evidence: `artifacts/plugin-help-dismissed-restart.png` and
`artifacts/plugin-help-reopened.png`. Build passed with the existing telemetry
warning; diff whitespace checks passed. This proves single-frontend restart
persistence and help reopening, not concurrent-process preference merging.

Data workspace state: version-2 layout tabs now have optional DataDirectory and
DataConflictsOnly fields and reuse Search for the Data search text. Older layouts
default to the root folder with no filter. Saved directory values must be relative
and cannot contain parent traversal. Each Data page retains its owning profile;
when the shared host changes games, it hides stale rows without overwriting that
profile's remembered presentation state.

Physical verification used a separate copy of the user's layout. Navigated to
FNV Data/Config, searched for mcm and enabled Conflicts only. The saved fields
matched those controls; a new frontend process restored them and read one native
entry (zero visible under the conflicts filter). Switched to the real Skyrim
profile and back to FNV: folder/search/filter remained intact. Evidence:
`artifacts/data-workspace-restored.png` and
`artifacts/data-workspace-profile-return.png`. Build passed with the existing
telemetry warning. Multiple Data tabs with different folders and missing-folder
recovery are not newly runtime-verified by this check.

Catalog responsiveness: periodic and Profile.Changed catalog refreshes now take
a registration snapshot and perform filesystem reads on a worker. The workspace
coalesces overlapping requests, applies results on the UI continuation and ignores
completion after disposal. Startup still loads the initial catalog synchronously.
The account monitor and popup login/logout obtain endpoint registrations directly,
avoiding the previous unrelated scans of all profiles and downloads.

`--check-catalog-read` uses a temporary MO2 profile whose modlist is a Linux FIFO.
The call returned in 0.8 ms while the file remained blocked; after release the
completed catalog contained the fixture mod. This proves the read no longer
blocks its caller, not an end-to-end frame-rate budget. Account-monitor contract
checks passed; repeated read-only native account checks confirmed 3/3 instances.
The rebuilt frontend rendered the live Skyrim lists after a physical game-icon
switch (`artifacts/async-catalog-skyrim.png`). Build passed with the existing
telemetry warning. Coalescing/disposal are source-reviewed here; the FIFO test
does not inject delayed completions into a live workspace.

Native order backups: My Mods overflow and the Plugins history button invoke
the original saveModsButton/restoreModsButton and saveButton/restoreButton.
The bridge validates profile/session before dispatch; the frontend captures the
profile when the menu opens and uses the normal busy/command guard. MO2 owns
flushing state, ten-backup retention, restore selection, copy errors and refresh.
The returned opened flag means native dispatch finished, not proof of a chosen
restore or successful backup.

Physical FNV Plugin Restore with no backups opened the native No Backups dialog.
Created and selected a temporary copied profile through MO2's own Profiles UI.
Plugin Backup produced timestamped plugins.txt, loadorder.txt and lockedorder.txt
copies. After a native API activation change, physical Restore opened the native
picker; selecting the backup restored all baseline plugin records. Mod Backup
produced its modlist.txt copy; after disabling the temporary MCM mod via MO2,
physical Restore recovered all baseline mod records. Plugin activation remained
independent of mod-list restore, as native MO2 implements. Preparation used the
native API because the initial physical plugin-toggle attempt did not establish
a changed state; it is not new checkbox interaction coverage.

Returned to the original profile and independently compared every mod/plugin
record and profile identity with the baseline: unchanged. Removed the temporary
profile using MO2, verified its directory was gone, and removed only its saved
frontend workspace. Evidence: `artifacts/order-backup-check.json`,
`artifacts/order-restore-no-backups.png`,
`artifacts/plugin-order-restore-picker.png`,
`artifacts/mod-order-restore-picker.png`. The updated bridge is deployed to all
three hosts; both Skyrim snapshots preserved their full original records and
deployed source bytes match. Skyrim backup/restore dialogs themselves were not
exercised. Build and 31 bridge contracts passed.

Plugin load-order locks: snapshots now expose MO2's native locked icon role.
Row actions dispatch the original PluginListContextMenu Lock/Unlock action on
exactly the selected plugin, restoring the host tab, search, selection and scroll.
The bridge verifies the resulting native lock state. Manual move and LOOT behavior
remain MO2-owned; this change does not impose a separate frontend ordering rule.

Physical FNV MCM Lock wrote native lockedorder.txt and reported locked=true;
Unlock returned the complete profile/mod/plugin records and lock-file bytes to
baseline. The frontend now refreshes preserved row objects on profile changes,
updating the padlock, action label, checkbox, opacity and action availability.
The activation helper reevaluates current availability after completing a change
instead of restoring the value captured during row construction.

Physical verification also exposed list movement when selection automatically
opened diagnostics. Diagnostics now open explicitly from the info button and
have a close control. After this change, physical MCM Disable and Enable at the
same coordinates both matched native snapshots and restored all original records.
An integration check also made four activation changes through the same existing
row, checking enabled/checked/model state and independent native snapshots.
Its first run timed out before identifying the stage; retrying row realization
until layout settled made the stage-specific check pass.

Evidence: `artifacts/plugin-locked-live.png`,
`artifacts/plugin-unlocked-live.png`, `artifacts/plugin-stable-disabled.png`,
`artifacts/plugin-stable-enabled.png`, `artifacts/plugin-details-explicit.png`,
and the PASS line in `artifacts/plugin-row-diagnostic-check.log`. Build and 32
bridge checks passed. All three hosts have matching updated bridge source;
both Skyrim instances retained their existing records and expose lock state.
Actual Skyrim lock changes and LOOT behavior with locks are not covered by this
FNV interaction check.

Mods row refresh: attached mod rows now observe profile changes and refresh
activation, current command availability, Enable/Disable labels, name/version/
category, endorsement and file/conflict tooltips. Separator action availability
and titles refresh too. Subscriptions are removed when rows detach; action
callbacks continue to guard the profile target captured for that row.

Physical FNV validation used the initially disabled MCM Author Examples mod.
An external native setModActive request enabled it; ordinary polling updated the
checkbox, row opacity, Disable menu entry and plugin count (its three plugins
appeared). Physical frontend Disable restored the complete baseline profile,
all mod records and all plugin records. Evidence:
`artifacts/mod-row-external-enabled.png`,
`artifacts/mod-row-external-actions.png`, `artifacts/mod-row-restored.png`.
Build and whitespace checks passed. Metadata-only edits and separator refresh
were source-reviewed rather than separately mutated during this check.

Expandable search now supports MO2 status/conflict/category/version qualifiers
and plugin status/lock/warning/origin qualifiers, without restoring the removed
filter button or bar. The NMA SearchControl has an optional page filter factory;
other pages keep its ordinary TextFilter. Unknown/incomplete syntax stays literal.
Structured searches reapply on ContentRevision changes, including state-only
plugin updates that intentionally preserve existing composite row objects.
Build and 17 query checks passed. Physical FNV searches showed only disabled MCM
Author Examples and the enabled MCM plugin using a combined name/status query.

The live state-change check exposed a preexisting persistence defect:
IPluginList.setState updates the in-memory flag without the native checkbox's
writePluginsList, index refresh, extension notifications or missing-master tests.
Returning to the native Plugins tab during the Saves panel's automatic read
could reload the old file and revert activation. The initial four-second stable
check after suppressing that tab-restoration signal was insufficient: restarting
the frontend exposed that activation had never been persisted.

Plugin activation now invokes the original PluginList model's CheckStateRole
path, validating identity and native checkability. Saves reads restore their
previous tab under QSignalBlocker to avoid unrelated reloads. Native MO2 owns
all list writing and notifications; the frontend writes no profile files.
34 bridge contracts pass, including native activation dispatch/profile guards
and Saves refresh restoring the tab without emitting its activation signal.

After the final fix, native MCM Disable remained disabled in three snapshots
over six seconds and was absent from FNV plugins.txt. Ordinary frontend polling
updated the active is:disabled search to show its unchecked row. Physical
frontend Enable removed it from that search and persisted it in plugins.txt.
Every FNV mod/plugin record and profile identity matched the original baseline.
Both searches were cleared afterward. All three native hosts run matching updated
source; both Skyrim instances retained their original profile/mod/plugin records.
Skyrim activation mutation and host-restart persistence were not newly exercised.
Evidence: artifacts/search-mods-disabled.png,
artifacts/search-plugins-enabled.png,
artifacts/search-plugins-disabled-stable.png,
artifacts/search-plugins-state-refresh.png. The full goal remains open.

Native activation restart verification: FNV MCM was disabled through the native
checkbox path, then its MO2 host was stopped and relaunched. A fresh native
snapshot retained state=1. Restored state=2 and restarted again during deployment;
the complete original profile/mod/plugin records were preserved. On the Skyrim
2.5.0 host, initially disabled SkyUI_SE.esp was enabled, survived a native restart,
and was restored to disabled. Its complete original records also survived the
subsequent restart. No game was launched during either activation check.

Automatic Archives reads now restore the previous native tab with QSignalBlocker,
as Saves reads do, preventing unrelated native tab reloads. The contract verifies
both requested refreshes run and neither emits a Plugins-tab restoration event.
34 bridge contracts and whitespace checks passed. All three hosts run matching
source. Native Archives/Saves reads returned 21/2 entries for FNV and 125/0 for
each Skyrim instance; snapshots afterward preserved every original mod/plugin
record and profile. The running frontend reconnected and rendered its normal
FNV workspace. Evidence: artifacts/plugin-persistence-restart-check.json and
artifacts/plugin-persistence-restart-restored.png. This supersedes the previous
section's unverified FNV/Skyrim host-restart persistence limitation; it does not
establish complete parity across every MO2 action.

Archives workspace search persistence: the page now owns SearchText, and layout
capture/restore includes it in the existing per-tab Search field. A recreated
Archives view restores this value before loading native archive rows. Other
pages retain their existing search state and layouts retain schema version 2.

Verified with a copy of the real layout: a seeded voices search restored one
matching FNV archive. Physically changed it to meshes, switched to Skyrim and
back, and confirmed the copied layout captured meshes and the panel retained it.
Restarted the frontend with that saved layout: meshes and its matching archive
were restored. Returned the frontend to the original normal layout afterward.
Evidence: artifacts/archives-search-restored.png,
artifacts/archives-search-game-return.png,
artifacts/archives-search-process-restart.png. Build and whitespace checks pass;
only the existing telemetry advisory warning remains. This check did not mutate
native mod/plugin state or extract archive contents.

Data source-folder action: added a selection-dependent toolbar button for loose
files. It rechecks the directory entry and complete origin list through MO2
before acting. Windows dispatches the original FileTree Reveal in Explorer
context action. Under Proton that dispatch completed without a visible Explorer
window, so Linux now asks MO2 resolvePath for the winning source and opens its
parent through xdg-open. Arguments use ProcessStartInfo.ArgumentList. Archive
members, stale origins and vanished files are rejected; unmapped Wine drives
produce an explicit error instead of guessing a local path.

Physical FNV check selected Config/MCM.ini in the Data panel and clicked the
button. Dolphin created a new window; querying every Dolphin window confirmed
exactly one displaying the MO2-resolved source folder. Checking only the existing
Dolphin process initially returned false because the new window had a separate
process. Closed only the newly created test window. 34 bridge contracts pass,
including stale-origin/archive rejection and native path resolution dispatch.
Build passed with the existing telemetry warning. Evidence:
artifacts/data-reveal-check.json. The normal frontend layout is restored after
testing with a copied layout. Native Windows Explorer presentation and other
Linux desktop environments are not newly verified by this check.

Desktop-launch responsiveness: Linux source-folder opening no longer waits for
the desktop handler's entire process lifetime while holding the MO2 command gate.
Mo2DesktopLauncher observes the first second for immediate nonzero exits, then
returns without terminating a still-running file manager. A later desktop failure
is not observable through this startup check and is not reported as a confirmed
folder-open outcome.

The --check-desktop-launcher check launched a real Python process with a twenty-
second lifetime. The call returned in 1053 ms, the child was still alive, and its
argument receipt exactly preserved a folder containing spaces and literal shell
characters. Immediate zero and nonzero exits were distinguished. The test then
terminated only its own child and removed its temporary directory. Build and
whitespace checks passed; the updated frontend was relaunched using the normal
layout. This check verifies launch lifetime handling, not a general frame-rate
claim or every Linux desktop's file-manager behavior.

UI responsiveness measurement: opt-in MO2_UI_LATENCY_REPORT samples input-priority
UI dispatcher queue waits for 45 seconds after a ten-second startup delay. This
measures queue delay, not rendered frame time or end-to-end physical click latency.
The test physically switched FNV to the 152-mod/151-plugin Skyrim profile and
performed 32 wheel-scroll bursts across both lists. An initial run mixed more idle
FNV time (p95 0.2 ms, max 1988 ms), so it is not the comparison baseline.

The timed-phase baseline with almost all samples on Skyrim measured p95 40.9 ms,
max 2027 ms, 18 samples above 100 ms. Snapshot application took 239 ms; no single
mod-thumbnail load exceeded 50 ms. Managed thread sampling during scrolling
identified repeated header discovery and row-highlight visual traversal as large
custom-code costs. Header discovery now prunes TreeDataGrid subtrees and reuses
its attached ScrollViewer. Row discovery stops at each row and inspects that
row's visuals once instead of repeatedly walking them.

The matched queue-delay repeat measured p95 36.9 ms, max 2107 ms, and still 18
samples above 100 ms. This does NOT resolve the large stall or prove a general
latency improvement from a single run. Separate 30-second scrolling stack traces
sampled header callback inclusive time at approximately 1723 ms before / 846 ms
after and row highlighting at 1183 ms before / 940 ms after. Those sampled times
are workload evidence, not independent CPU totals or a frame-rate benchmark.

Build and whitespace checks pass. Physical rendering still shows normal row
styling, hover grips and the compact header after scrolling. Evidence:
artifacts/ui-latency-phases.json, artifacts/ui-latency-after.json,
artifacts/ui-scroll-hotspots-before.json, artifacts/ui-scroll-hotspots-after.json,
artifacts/ui-scroll-styling-after.png, artifacts/ui-header-compact-after.png.
Raw traces are in /tmp/mo2-ui-cpu.nettrace and /tmp/mo2-ui-after.nettrace. The
remaining ~2-second outlier and substantial scrolling queue waits are OPEN
responsiveness issues, not accepted completion criteria. The normal frontend is
restarted without the opt-in probe after measurement.

First-switch trace: collected /tmp/mo2-switch.nettrace before the first FNV-to-
Skyrim transition (no scrolling). UI-thread managed leaf attribution was led by
observable publication and style attachment. Snapshot processing sampled at
239 ms inclusive, provider creation 182 ms, sort-item enumeration 104 ms and game
thumbnail generation 63 ms. These overlapping sampled times narrow the work to
list/style setup but do not establish the complete cause of the queue-delay
outlier. Scoped summary: artifacts/ui-switch-trace-summary.json.

Tried deferring both row action menus until opening. The matched 45-second run
still measured p95 38.0 ms, max 2693 ms and 17 waits above 100 ms. Worse, the first
Mods dropdown produced a blank 300x200 popup. Reverted the entire deferred-menu
experiment. Rebuilt and physically verified the restored Mods and Plugins menus
render their original entries and native availability, then closed the popups.
No activation, order, file or profile mutations were requested in these checks.
Evidence: artifacts/ui-latency-lazy-menus.json (rejected experiment),
artifacts/mod-menu-restored.png and artifacts/plugin-menu-restored.png.
The normal frontend is running with no latency probe enabled. Build and whitespace
checks passed. The performance stall remains OPEN; no menu optimization or
responsiveness improvement is claimed for this turn.

Plugin provider batch count: GetSortOrderItems(id).Count was sorting/materializing
the complete order during Create and Update for each row. It now runs once in
the incoming change-set pipeline, before row transformations. An isolated
DispatchProxy counter verifies exactly one full-order read per batch. The UI-
model check covers 151 -> 152 -> 2 plugins, ascending/descending movement limits
and preserved row references. Its initial harness reported default command states
because it omitted CompositeItemModel.Activate; matching the real adapter's model
lifecycle and awaiting observable delivery made all checks pass. This does not
mutate native profile state. PASS is in artifacts/provider-count-check.log.

The live repeat (first Skyrim switch plus 32 scrolling bursts) measured p95
39.1 ms, max 2452 ms, 18 waits above 100 ms; native snapshot Apply took 235 ms.
These results do not establish an overall responsiveness improvement. Keep the
verified reduction in full-order reads, but the latency issue remains OPEN.
Evidence: artifacts/ui-latency-batch-count.json. Build and whitespace checks pass.
The launch scripts were found to use dotnet run's default Debug configuration;
a separate full Release build is the next comparison, with the current runtime
left available until that build is verified.


Release deployment (September 13): full Release build succeeded with 53 warnings
and no errors, using the repository SDK on PATH for StrawberryShake generation.
Release search-query (17 cases), provider batch-count/model-lifecycle, and NXM
parser/routing checks pass. The matched live scroll/switch probe reports p95
37.206 ms, maximum 2048.077 ms, 18 waits over 100 ms; Apply native snapshot
191.967 ms (artifacts/ui-latency-release.json). Release does not resolve the
first-switch stall or establish fluid scrolling; responsiveness remains OPEN.

run.sh now defaults to Release (MO2_BUILD_CONFIGURATION can override it).
install_desktop.py defaults to Release, supports --configuration Debug, and
validates the binary and local SDK before installing. The installed desktop
and NXM wrappers now use the verified Release binary. NXM's previous-handler
record is preserved. Its registered default and wrapper were inspected; the
Release --check-nxm passed without making downloads. The installed desktop
launcher was started and its /proc command line verified as Release with no
latency probe. FNV reconnects with 11 mods, 11 plugins and two saves, account
indicator and saved panel layout visible at 1280x750 in
artifacts/release-desktop-launch.png. This verifies the launch script; it is
not a new end-to-end browser download test. Shell syntax and diff whitespace
checks pass. No native mod/plugin state was changed by this deployment.


September 14 UI follow-ups: Mods row backgrounds removed, row height reduced to
40px in both lists; expanded headers use 48px icons/22px titles and description
opacity .65 while retaining the four-line fade. Toolbar groups match search control
height; redundant direct actions moved/retained in overflow. Plugins' extra MainGrid
top margin removed. Mods gained the matching centered rail, direction/trophy markers
and a functional help flyout. Rounded separator bars now have 16px side insets.
Separator hover/selection uses an inset overlay and native full-row highlight is
suppressed for separator rows, cleared on row recycling. Live visual verification
of the final highlight/sidebar patch is still pending below.

Separator Create/Cancel, native creation, member-count collapse, routed double-click
Rename/Cancel with prefilled name, rename preserving collapsed state, expansion,
and frontend Delete/Cancel/Accept passed in artifacts/separator-interactions.log.
The first harness attempts timed out because the host had exited, and then because
only Plugins was open; the harness now explicitly opens Mods. Its RenderTargetBitmap
captures omitted compositor-rendered rows and were discarded. Actual X11 capture
artifacts/compact-mods-live.png shows the real rounded separator and rows. The user's
existing ggg separator was preserved. After removing the temporary Overwrite warning
file, all native mod and plugin records and the active profile matched the saved
baseline exactly. No test separator remains.

35 native bridge contracts pass, including exact boolean confirmation dispatch and
stale-profile rejection. All three hosts have the current core/mod_actions files.
The FNV host had crashed in USER32.GetPointerFrameTouchInfo. The launcher now sets
QT_QPA_PLATFORM inside a Windows batch launcher, matching the previously documented
workaround; seven launcher contracts pass. A Skyrim startup exited with an allocator
error; after its process was confirmed gone, a retry succeeded. Repeated native
account checks subsequently confirmed 3/3 instances connected. This is recovery
verification, not proof that every Proton startup failure is eliminated.

Notifications: a temporary Overwrite file produced a real MO2 Problems warning;
the bell showed 1 and the popup displayed the matching title in
artifacts/mo2-notification-center.png. After the file was removed, native warnings
returned to zero and the badge cleared (artifacts/profiles-sidebar-live.png).
The latter also verifies the game sidebar opens Profiles with only New Vegas
profiles. Game-scoped page creation now stays scoped even while catalog metadata
is unavailable. Notification model checks pass for deduplication, unread/read,
resolution/recurrence and same-named profiles in different instances.

The additional Send-priority latency probe produced only FNV samples (no game
switch was captured). artifacts/ui-latency-priorities.json therefore cannot test
the outstanding Skyrim switch stall or support a performance-improvement claim.
The broader responsiveness goal remains OPEN.


Final live sidebar/highlight verification: collapsed state survived a process
restart; all navigation icons are visible after removing the native Expander
minimum width in compact mode. Original section expansion states are restored
when expanded. Physical navigation between Mods and Plugins worked; original
launch selector/Play are available in the compact tools popup. Expanded mode
restores the original launcher and labels. Evidence: sidebar-collapsed-live.png,
sidebar-mods-navigation.png, sidebar-plugins-navigation.png,
sidebar-launch-tools.png, sidebar-expanded-restored.png (all in artifacts).
The first collapsed rendering clipped section icons due to the upstream Expander
minimum width; this was corrected before the final captures. An opt-in
MO2_TRACE_SIDEBAR diagnostic prints only input/control geometry for investigation.

Physical selection of the existing separator highlights only the inset rounded
bar (artifacts/separator-highlight-inset.png). A physical double-click opened
Rename separator with its current name prefilled; Cancel preserved it. The toolbar
Delete action opened the frontend separator-specific confirmation; Cancel kept
it. Captures: separator-double-click-modal.png and separator-delete-modal.png.
The Mods question mark opens the complete help flyout (mods-help-popup.png).
Both Skyrim instances' complete mod/plugin records match their pre-deployment
snapshots; FNV matches the pre-fixture snapshot after cleanup. Repeated native
account refresh confirms 3/3 connected. All temporary warning files and test
separators were removed. The broader startup/switch responsiveness issue is still
open; none of these UI interaction checks proves a frame-time target.


2026-09-14 — Steam External Files panel:
- Checked upstream NMA MyGamesViewModel clean-folder flow and External Changes
  navigation (FolderEditOutline icon). Implemented a separate game-scoped panel
  for the physical game directory; existing MO2 Overwrite actions remain available.
- SteamKit2 reads every InstalledDepots manifest by exact depot/version ID.
  Missing/mismatched/encrypted manifests fail without classifying partial results.
  The configured MO2 game path must match Steam's appmanifest install directory.
- Build Release passed (one existing advisory warning). --check-external-files
  passed ownership/DLC exclusion, case and slash matching, invalid relative paths,
  extra files, symlink non-traversal, missing-manifest failure and cancellation.
- Actual read-only scans: FNV 17 depots / 464 Steam paths / 11 external entries;
  Skyrim SE 3 depots / 46 Steam paths / 2239 external entries. JSON evidence:
  artifacts/external-files-fnv.json and external-files-skyrim.json.
- Physical sidebar navigation works collapsed; state survived frontend restart.
  External Files displayed correct game-specific data after FNV -> Skyrim switch,
  including in the left half of the paired layout. Search nvse_loader yields two
  FNV entries; refresh functional. Screenshots: external-files-fnv-live.png,
  external-files-search.png, external-files-skyrim-live.png in artifacts.
- Scope: ownership detection/search/reveal only. No modified-Steam-file hash
  checks, cleaning or movement were performed or claimed. Native mod/plugin
  activation and priorities were not changed by this work.


2026-09-14 — Confirmed game-switch performance investigation (goal still open):
- Previous turn was progress: Steam External Files implementation, installed-game
  scan results and physical panel checks changed authoritative state.
- Diagnostics start from the same isolated saved layout/FNV endpoint, request the
  already-selected Skyrim profile after sampling begins, and record both Input
  and Send dispatcher priorities. These are queue-delay measurements, not FPS.
- Earlier baseline: Input max2352ms / Send max320ms / switch424ms. Flat-list
  initialization alone: Input2635ms / Send380ms / switch564ms; no improvement claim.
- Added opt-in row/view creation counters. Baseline counter run: Input2329ms,
  28 plugin rows +22 mod rows. Window detachment around confirmed snapshot commit
  yielded2735ms with identical counts and was reverted. Identity-safe recycling
  template yielded2684ms, zero reuse and identical counts; template and its check
  were removed. No failed experiment remains in production code.
- ShowProfile now avoids ChangeActiveWorkspace when already on the destination;
  NMA's controller otherwise raises ActiveWorkspace even for an unchanged ID.
  Guarded run: Input max2069.549ms / Send309.756ms / switch498.503ms; exactly one
  new Mods view and one Plugins view, yet28 plugin rows +22 mod rows. One run is
  insufficient evidence of a stable timing improvement; the cold-switch stall
  remains unresolved. Source is built Release with0 errors (existing warning).
- Reports: artifacts/ui-latency-flat-init.json, ui-latency-row-count.json,
  ui-latency-detach.json, ui-latency-row-reuse.json, ui-latency-switch-once.json.
  Further work should identify duplicate row realization within/around activation
  and reduce layout/styling cost. Do not treat existing source reuse or isolated
  idle latency as proof of fluid input across game switches.


2026-09-14 — Eliminate duplicate row realization from redundant root sorting:
- Previous goal turn was progress: added diagnostics, measured and reverted failed
  experiments, and removed a redundant workspace activation. This turn traced
  model/adapter identity rather than assuming that duplicate rows meant two views.
- ui-latency-row-identities.json proves each visible Skyrim model was realized
  twice by the same adapter:11 unique Mods models and14 unique Plugins models,
  each built twice. First realization came from initial layout; second from cell
  re-realization after its Content/DataContext changed.
- TreeDataGridAdapter unconditionally called ObservableList.Sort after batches
  and comparer emissions. Sort publishes Reset even when already ordered. Added
  ObservableListSortingExtensions.SortIfNeeded, checking adjacent items first;
  both adapter sort sites now skip unnecessary resets. Real reorder still uses
  the original sort implementation. This is a shared NMA UI adapter fix.
- Confirmed switch after fix: exactly11 Mods +14 Plugins row constructions (half
  the previous22/28), one view each. Input max2413.722ms / Send410.227ms /
  switch547.116ms. No latency improvement claim: input starvation remains open.
  Evidence artifacts/ui-latency-sorted-roots.json.
- Full Release build passed with48 warnings and0 errors. --check-sorted-roots
  passed sorted/empty/equal-order no-reset cases, reverse comparer and priority
  changes. Live FNV UI test passed equivalent-comparator selection preservation,
  reverse/restore visual source order, and unchanged native plugin state.
  Evidence artifacts/sorted-roots-ui-final.log. First UI harness assumed an open
  Plugins panel and timed out; corrected to navigate there. A subsequent harness
  failure was ICollection.CopyTo unsupported by the synchronized source view;
  changed snapshot capture to enumeration. Neither failure mutated native state.
- Temporary latency/check layouts were isolated from normal workspace-layout.json.
  Sorting checks change only presentation and restore the original comparator and
  selection. No MO2 mod/plugin order, activation, files or profiles were edited.


2026-09-14 — Profile remaining pause and verify native pointer delivery:
- Previous goal turn was progress: redundant root-sort resets eliminated, visible
  row construction halved, and collection/live sorting checks passed.
- New25s sampled-thread trace after that fix: /tmp/mo2-postsort.nettrace and
  /tmp/mo2-postsort.speedscope.json. Dominant4–9s burst includes1335ms in layout
  and1058ms in styling (inclusive, overlapping sampled thread times, not sums).
  Artifacts/postsort-switch-hotspots.json records the method samples. Obsolete
  plugin thumbnail composition still accounted for~63ms inclusive.
- Tried Avalonia DispatcherOptions.InputStarvationTimeout=50ms (default1s per
  installed11.3.5 docs/MediaContext source). First queue max1780ms, repeat2464ms;
  no consistent improvement. Reverted the option and its explanatory comment.
- Tried creating row context/flyout items immediately before opening. Queue max
  2802ms, no improvement; restored all3 row/menu files byte-for-byte from the
  pre-experiment working copies. Existing row menu behavior remains intact.
  Reports ui-latency-input-budget.json, ui-latency-input-repeat.json and
  ui-latency-menu-lazy.json preserve the unsuccessful measurements.
- Added opt-in real pointer observation to the latency diagnostic. XTest injector
  moved250 times, every100ms, over the blank top-bar band; no clicks or native
  game-state operations. Sender largest interval101.214ms. Listener observed182
  positions from695..899 (it starts after initial injection);23 positions within
  that range were coalesced across a2416.676ms gap. Received-event lag itself
  max20.694ms must NOT be reported as responsiveness: the delivery gap proves
  actual pointer input was stalled while injection continued. Dispatcher max
  2355ms during this confirmed Skyrim switch. Reports native-pointer-injected.json,
  ui-latency-native-pointer.json and native-pointer-summary.json in artifacts.
- Removed obsolete GameArt/ModArt loading from native plugin snapshot mapping:
  the redesigned plugin rows no longer display images, yet this still decoded
  and composed them for the unused original column. Game/sidebar/card artwork
  remains supplied by its existing paths. This removes known unused work; it is
  not evidence that the cold-switch input stall is fixed.
- Goal stays open. Continue reducing synchronous layout/styling cost, with actual
  pointer delivery gaps as well as queue probes. No successful game-state edits
  or completed fluid-input claim follows from this investigation.


2026-09-14 — Defer panel bodies to separate UI turns:
- Previous turn was progress: established actual native pointer starvation,
  reverted ineffective experiments and removed unused plugin thumbnail work.
- Tested DOTNET_TieredCompilation=0 only in the diagnostic process environment.
  It still produced a2015.709ms native pointer gap (dispatcher1941ms). No runtime
  or launcher setting was adopted. Microsoft runtime compilation docs were checked:
  https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation.
- Added Mo2DeferredView<T> for the live Mods/Plugins view-locator branches. The
  workspace displays a loading label, then constructs each full native-backed
  body at Background dispatcher priority. Rendering/input can run between panel
  bodies. Detached or replaced pending loads are invalidated by a version token;
  already loaded bodies are reused on reactivation. Native page models, layouts,
  source adapters, drag/drop and actions remain the same.
- Two confirmed cold FNV->Skyrim runs: native pointer delivery gaps1030.425ms and
  893.015ms; dispatcher maxima1018.694ms and880.483ms. Both rendered exactly one
  Mods view/11 rows and one Plugins view/14 rows. Compare prior real-pointer gap
  2416.676ms. These measurements demonstrate progress, not full fluidity: nearly
  one second of interruption is still too long. Sender/receiver statistics and
  report references are in artifacts/deferred-panel-comparison.json.
- Release build passed (one existing warning,0 errors). New opt-in
  MO2_VERIFY_DEFERRED_PANELS chains the existing two-tab restoration/resize check
  and live sorting/selection check on an isolated layout. It passed1280/900/640px
  tab selection, repeated switching without a layout loop, equivalent-comparator
  selection retention, reversed/restored presentation order and unchanged native
  plugin state. Evidence artifacts/deferred-panels-ui.log. Rendered Skyrim paired
  lists: artifacts/deferred-panels-skyrim.png.
- Tests used temporary layouts and no game-data mutations. Normal configuration
  and collapsed-sidebar preference restored; no diagnostic/JIT environment is
  used for the normal app. Goal remains open for further responsiveness work and
  the full requirement-level completion audit.


2026-09-14 — Cold-switch scheduling experiments and timeline diagnostics:
- Prior turn was progress: detached unused collection/Rules controls in two
  measured trials, found no reduction in native input gaps, and reverted the
  change. Evidence: artifacts/detached-controls-experiment.json.
- A fresh sampled-thread trace of the retained deferred-panel implementation
  still attributes most non-idle UI-thread work to styling and layout. Inclusive
  samples overlap and must not be summed as independent CPU durations. Evidence:
  artifacts/current-profile-hotspots.json; raw trace is /tmp/mo2-current-profile.nettrace.
- Tried deferring individual row bodies, then a 50ms input-starvation timeout,
  then pacing row creation with a 16ms timed yield. Native pointer maximum gaps
  were 1009.664ms, 966.720ms and 1047.500ms respectively. None improves on the
  retained baseline's 893.015–1030.425ms range. All experimental row code and
  timeout settings were reverted. Native profile data was not edited.
- Added timestamps to the opt-in row-build and slow-phase records, plus a timed
  span around deferred panel construction/attachment. This allows the native
  pointer delivery gap to be correlated with actual work, rather than inferring
  a bottleneck from aggregate samples alone. These are diagnostics, not a claim
  of improved responsiveness. Scheduling comparison:
  artifacts/row-scheduling-experiments.json.
- The timestamped retained-baseline run located a 934.795ms native pointer gap.
  Snapshot application occupied 63.983–180.320ms relative to its start. Mods row
  construction began at 1135.397ms and Plugins at 1612.224ms, both AFTER that gap.
  This contradicts the working theory that visible row creation causes the
  largest remaining pause. Investigate workspace transition/activation and its
  preceding layout next. Evidence: artifacts/cold-switch-timeline.json and
  artifacts/ui-latency-timeline.json. This establishes timing, not yet the exact
  responsible workspace method. The full goal remains open.
- Release build and both worktree whitespace checks passed. Normal user layout
  and collapsed-sidebar preference restored; ordinary frontend has no diagnostic
  or alternate dispatcher settings.


2026-09-14 — Stage workspace panels and rows (retained):
- Previous turn was progress: timestamped evidence ruled out list rows as the
  cause of the FIRST/largest remaining pause. Added timing around workspace
  creation, activation and catalog/sidebar updates. In the new baseline,
  activation took225ms, followed by layout before the first deferred body;
  the native pointer gap was1024ms. See ui-latency-workspace-timing.json.
- Added optional WorkspaceView.PanelViewFactory. Default NMA construction is
  preserved. The MO2 host uses Mo2DeferredPanel to position a lightweight shell
  from native ActualBounds, then attaches the original PanelView at background
  priority. Native panel controls, divider controls, models and persisted layouts
  are retained. This is an intentional small difference from the pinned fork's
  WorkspaceView code; it is no longer claimed byte-identical.
- Outer deferral split the first1024ms gap into519/323/225ms gaps. A later Plugins
  load then dominated at877ms. Earlier row experiments had been judged only by
  the largest gap, masking their effect on this later stage. Reintroduced simple
  per-row background construction together with outer deferral; no special
  dispatcher timeout or timed16ms queue was retained. Mo2DeferredRow cancels
  pending work on detach and reuses an already loaded body on reattach.
- Two valid cold FNV->Skyrim measurements: native pointer maximum gaps489.348ms
  (XTest) and444.833ms (evdev kernel absolute pointer); dispatcher maxima471.893ms
  and405.564ms. Both began receiving native moves before snapshot application,
  both senders maintained approximately100ms intervals, and both created one
  Mods view/11 rows and one Plugins view/14 rows. Evidence:
  artifacts/staged-panel-comparison.json, ui-latency-staged.json and
  ui-latency-staged-device.json. This is an improvement, not full fluidity.
- Excluded staged-repeat's mouse metric because injection started15.249 seconds
  after the snapshot, missing the cold switch. Excluded staged-final's mouse
  metric because XTest produced no in-band native samples. Their dispatcher
  samples remain evidence, but are not substitutes for native input delivery.
- Full Release build passed48 existing warnings/0 errors; subsequent host-only
  builds passed1 existing warning/0 errors. Both worktree diff checks passed.
  staged-tabs-check.log passed deferred row detach/cancel/reattach/reuse,
  repeated selected-tab updates at1280/900/640px, native panel geometry, equivalent
  comparator selection retention, reversed/restored presentation sorting and
  unchanged native plugin state.
- staged-paired-pointer-check.log verifies paired native geometry and divider
  hit testing. XTest button injection was observed atx1279 despite a request for
  x792, explaining failed automated drags; no divider code was changed. An evdev
  drag reached the native divider and changed saved panel widths from.5/.5 to
  .588235/.411765. Evidence staged-panel-divider.json and staged-divider-fnv.png.
  Only an isolated layout was changed; no native mod/plugin action was invoked.
- Normal user layout and collapsed sidebar restored, with no verification or
  latency environment in the ordinary frontend. Goal remains open for remaining
  responsiveness work and the full requirement-level completion audit.


2026-09-14 — Verify deferred rows through filtering and real viewport changes:
- Previous turn was progress: staged native panels/rows reduced valid cold-switch
  native input gaps to445–489ms and passed native divider dragging. This turn
  adds interaction evidence; it does not claim another performance reduction.
- Added MO2_VERIFY_FILTERED_ROWS for an isolated paired layout. It crosses the
  actual SearchControl100ms debounce repeatedly with enabled/no-match/clear
  queries, confirms empty results, then scrolls both lists at an actual520px
  client height. Every realized row's drag handle and activation control must
  match its current native model. Native mod activation/priority and plugin
  activation/order are compared before/after.
- Passed against the actual FNV Frontend Test and Skyrim Default hosts. Evidence:
  artifacts/filtered-rows-fnv-ready.log and filtered-rows-skyrim.log. The latter
  rendered the large Skyrim lists (150 mods/150 plugins at this check), captured
  in filtered-rows-skyrim-complete.png. Counts describe this observed host state,
  not a fixed expected count for all future snapshots.
- Tightened test readiness: finding a visible page is insufficient while its
  native table template is still attaching. The check now waits for populated
  native tables before inspecting them. The initial startup race was a test
  readiness failure, not evidence of a production table failure.
- Found that a maximized desktop window ignored requested height changes. Earlier
  tests which only assigned Width did not independently prove each viewport size.
  Tests now temporarily leave maximized mode, wait for actual ClientSize to match
  the requested dimension, then restore the original window state. The hardened
  tab/deferred-row/geometry/sorting check passed actual1280/900/640px widths:
  artifacts/actual-tab-viewport-check.log. This supersedes the earlier indirect
  resize evidence. The first height-only attempt failed because both rails were
  not forced to scroll; that failure was not counted as a pass.
- Re-ran the real separator workflow after deferred row loading: Create/Cancel,
  collapse count, routed double-click Rename/Cancel, prefill, collapsed-state
  retention, Delete/Cancel and confirmed deletion all passed. Original
  modlist.txt, plugins.txt and loadorder.txt hashes match before/after; both owned
  separator fixture folders are absent. Evidence staged-separator-check.log and
  staged-separator-restoration.json. The user's existing ggg separator is retained.
- Release build and both worktree whitespace checks passed. Normal layout and
  sidebar preference restored with no verification flags. Goal remains open for
  remaining cold-switch responsiveness and the full requirement-level audit.


2026-09-14 — Buffer presentation updates during profile selection:
- Previous turn was progress: live filtering/scrolling passed for both games,
  separator operations restored native files, and viewport tests were hardened.
- Instrumented CreateModModel in the existing opt-in latency probe. A cold
  FNV->Skyrim switch built304 mod models for152 presented entries: the departing
  adapter transformed the new snapshot before its workspace detached, followed
  by the new adapter building the same game again.
- Mo2LiveProfile.ObservePresentationMods now uses DynamicData BatchIf to buffer
  raw mod change sets while SelectingProfile is true. The initial pause state is
  evaluated at subscription time with Observable.Defer. Departing subscriptions
  dispose without transforming the buffered next-game updates. A surviving
  subscription receives its batch on resume. The authoritative snapshot and
  native operations remain independent of this presentation buffer.
- Both measured buffered runs built152 models. First snapshot application fell
  from137.510ms in the measured baseline to67.452ms and52.280ms. Native evdev
  pointer maximum gaps were570.239ms before, then476.902ms and374.867ms. All three
  injectors ran before snapshot application with approximately100ms intervals.
  Timing varies between cold runs; model-count reduction is deterministic in
  these samples and does not imply fully fluid input. Evidence:
  artifacts/model-buffer-comparison.json and its referenced latency reports.
- Added MO2_VERIFY_PROFILE_BUFFER with an isolated FNV layout. The live check
  passed same-profile selection and rejected unowned-profile selection while
  preserving source/model identity and selection. It then switched to the
  already-selected Skyrim profile and back, checking every presented native ID,
  state and priority. Original native activation/order matched on return.
  Evidence artifacts/profile-buffer-check.log. No missing profile was created.
- Release build passed and whitespace checks passed. Normal user layout and
  collapsed sidebar restored without diagnostic flags. Goal remains open for
  remaining responsiveness and the full completion audit.


2026-09-14 — Isolate panel chrome and workspace replacement:
- Previous turn was progress: removed duplicate mod projection work and verified
  selection/identity through refresh, rejection and cross-game return.
- Experimentally delayed parenting the native tab-header controls on large,
  single-tab Mods/Plugins pages. It shortened some later construction work, but
  the initial native input gap remained380ms versus375ms in the preceding run.
  Reverted the helper and factory change; no lazy-header behavior is retained.
  See artifacts/lazy-header-experiment.json and ui-latency-lazy-header.json.
- Split activation diagnostics into Replace workspace canvas and Retarget sidebar
  controls. The observed switch spent135.88ms replacing the canvas and31.87ms
  rebinding the sidebar (167.76ms combined). Snapshot application was59.11ms.
  This points the next investigation toward workspace replacement, rather than
  sidebar navigation or hidden tab controls. Evidence:
  artifacts/ui-latency-canvas-timing.json. These are measured call durations,
  not a claim that one exact internal canvas operation has been identified.
- The activation callback now calls the existing SyncMenu helper instead of
  duplicating its property assignments. Existing native panel/header behavior
  is preserved. Release build and both worktree whitespace checks passed.
- Normal layout and collapsed sidebar restored, without diagnostics in the
  ordinary app. Full goal remains open.


2026-09-14 — Steam path correction and workspace teardown measurement:
- Previous turn was progress: corrected trailing directory separators before
  locating Steam app manifests. The external-files regression check now covers
  both spellings. Real scans of the user-provided paths passed: FNV 17 depots,
  464 Steam files, 11 extras; Skyrim 3 depots, 46 Steam files, 2239 extras.
- Tried disposing the old workspace subscription before clearing the canvas.
  Canvas replacement remained135.30ms versus135.88ms previously, with a515.79ms
  maximum native pointer gap. Reverted the experiment; no performance benefit
  was established. See artifacts/workspace-disposal-experiment.json.
- Temporary detailed instrumentation isolated clearing the departing panel at
  110.63ms, arrangement10.47ms, and subscribing/attaching the new shells9.53ms.
  The complete run had a472.09ms native pointer gap. Both evdev injectors began
  before snapshot application and maintained approximately100ms intervals.
  See artifacts/workspace-clear-timing.json and its ui-latency-workspace-detail
  report/log. Removing the old visual tree is now the specific next target;
  these results do not identify its internal expensive controls or prove fluid
  switching. Temporary instrumentation was removed and original WorkspaceView
  source restored byte-for-byte from the pre-experiment backup.
- No workspace optimization from this turn is retained. The full goal remains
  open, including input responsiveness and the requirement-level completion audit.
- Restored Release build passed (48 existing warnings, zero errors); both
  worktree whitespace checks passed. External-files regression check passed
  after restoration. Normal layout/sidebar restored and normal process has no
  UI latency or verification environment flags.


2026-09-14 — Profile teardown and recheck integration boundaries:
- Previous turn was progress: isolated110.63ms in departing-panel removal and
  rejected ineffective subscription reordering. This turn sampled the current
  Release process during a cold FNV-to-Skyrim transition with dotnet-trace.
- artifacts/workspace-clear-profile.json filters the main-thread samples to
  WorkspaceView clear stacks after trace second10, excluding startup. It covers
  83.42ms of samples during the switch. Leading managed leaf samples were
  Avalonia observable publication18.07ms, observer removal8.39ms and property
  reevaluation6.69ms; row recycling and binding/style teardown also contribute.
  Inclusive samples overlap and must not be added. This trace perturbs runtime
  and is not evidence of native input latency or a measured improvement.
- No speculative teardown optimization was added. Removing subscription order
  as the main cause remains supported; the cost spans framework control cleanup.
- Fresh contract runs passed35 bridge tests,8 credential boundary tests,4 download
  tests and4 handler-installation tests. CLI NXM validation/routing and account
  monitor race/failure checks passed. These use fixtures and do not independently
  prove a new real download, archive extraction or browser authorization flow.
- Fresh read-only account refresh twice confirmed3/3 registered native instances.
  xdg-mime still resolves nxm to mo2-nexus-frontend-nxm.desktop; its installed
  wrapper points to the current Release host. No account credentials were output.
- Normal frontend, saved layout and collapsed sidebar restored. Source unchanged
  by profiling. Full completion remains unproven; next audit should exercise the
  current popup/layout and useful native action surfaces, while keeping the
  measured switching pause an explicit open requirement.
- Current Release popup check passed: real NMA overlay fits the current Steam
  Deck owner window, bound Cancel closes, and a second popup opens/closes. The
  injected authorization task only waits for cancellation, so this verifies UI
  ownership/cancellation and does not claim a new browser SSO authorization.
  Evidence artifacts/current-popup-check.log. Normal layout restored afterward.


2026-09-14 — Archives and Overwrite activation-scoped refreshes:
- Previous turn was progress: fresh popup/account/routing checks and sampled
  teardown evidence. Current source review found that pending reads could publish
  into detached Archives/Overwrite views and reopening during a read could keep
  the older activation's result.
- Both views now track activation identity, discard detached/older results and
  errors, avoid follow-up reads while detached, and refresh the current activation
  when an earlier read completes after reopening. Actions disable on detachment.
  Read-only native operations already in flight are allowed to finish.
- Added MO2_VERIFY_FILE_PAGE_LIFECYCLE. Controlled asynchronous readers exercise
  closing a page with a read pending and reopening both before and after that
  completion, for both page types. Tests assert no detached rows appear and the
  reopened table contains the fresh response. Reader injection is internal; the
  ordinary constructors still call the native profile reader.
- Live tests then use those same native readers and compare every table record
  against independent MO2 readback. Both FNV and current Skyrim passed. Evidence:
  artifacts/file-page-lifecycle-fnv-final.log and
  artifacts/file-page-lifecycle-skyrim-final.log.
- The first native comparison failed because the test accepted an initial empty
  Archives label before its first read completed (0 rows vs21). The verifier now
  waits for the native response, not just that label. That failed attempt is not
  counted as a production regression or passing evidence.
- Release build passed and worktree whitespace check passed. Native archive/file
  operations were read-only in this turn; prior extraction/Overwrite mutation
  evidence is not replaced by these lifecycle checks. Normal user layout restored.
  Overall goal remains open, including switching responsiveness and full audit.


2026-09-14 — Extend activation-scoped reads to Saves and Data:
- Previous turn was progress: Archives/Overwrite pending-read fixes passed both
  controlled detach/reopen cases and real readback for FNV and Skyrim.
- Source review found the same pattern in Saves and Data. Both now discard stale
  activation results/errors, stop follow-up reads while detached, and refresh
  after a pending read completes on a reopened view. Detached controls disable
  actions. Save/Data action error messages are also guarded by activation identity.
  Data selection callbacks cannot highlight another page's mods while detached.
- Extended the existing lifecycle check to all four page types. It waits for the
  native connection before fixtures, then exercises reopen before/after pending
  read completion. Both games passed. Native readback checks compare full Saves
  records and merged Data names, directory flags, archive names and ordered source
  origins (structural comparison, not array identity), alongside prior pages.
  Evidence artifacts/all-file-page-lifecycle-fnv.log and
  artifacts/all-file-page-lifecycle-skyrim.log. These reads did not modify saves,
  files, archives, native activation or plugin order. Action mutation workflows
  were not re-run in this turn; late-action message guards were source-reviewed.
- Release build passed (one existing warning, zero errors); whitespace checks
  passed. Normal user workspace restored. Full goal remains open for measured
  switching responsiveness and the remaining requirement-level audit.


2026-09-14 — Preserve Saves selection through refresh:
- Previous turn was progress: Saves/Data lifecycle changes passed delayed-read
  checks and native table comparisons for both games.
- Saves previously rebuilt its source without remembering selection. It now keys
  selection by native file path, restores it after filtering/refresh/reordering,
  and clears it on profile change or a completed read that no longer contains the
  file. A subsequently recreated file does not silently regain selection.
- Added the selection workflow to the existing live file-page check. Controlled
  rows exercise identical display titles, filtering the selected row out and back,
  reordered/renamed refresh, removal, and recreation. Both games passed alongside
  all four page lifecycle and native readback checks. These fixtures do not touch
  actual saves. Evidence artifacts/save-selection-fnv-settled.log and
  artifacts/save-selection-skyrim-settled.log.
- Initial selection assertion ran before TextBox filtering settled. Waiting for
  the expected row count corrected the test; no additional production change was
  needed for that failure. Release build and whitespace checks passed. Normal
  workspace restored. Full goal remains open, particularly switching performance
  and the broader completion audit.


2026-09-14 — Current FNV shared-component audit:
- Previous turn was progress: Saves selection retention passed both-game checks.
- Verified local reference HEAD is3b0244e7d21ea19afabcc3d81539c9e11ae8d642.
  Compared committed contents against the current upstream working tree for11
  shared panel/workspace/history/login files. Six match exactly; five contain
  documented layout, history and deferred-construction fixes. Stored hashes and
  exact diffs in current-fnv-source-comparison.json and
  current-fnv-shared-components.patch. Updated NMA_FIDELITY.md's leading section
  and corrected the old blanket source-identity claim in this audit.
- Current Release tab/viewport/geometry/sorting checks passed at actual widths
  1280/900/640/1280 with native plugin state unchanged. Inspected X11 capture
  current-fnv-panel-check.png; runtime log is current-fnv-panel-check.log.
- This is bounded source and runtime evidence, not full visual parity or completed
  responsiveness. Normal workspace restored; overall goal remains open.


2026-09-14 — Reject snapshot/workspace yield experiment:
- Previous turn was progress: refreshed exact FNV source comparison and actual
  viewport/tab checks. Tried yielding for1ms after snapshot application while
  SelectingProfile and the command lock remained active. This was experimental,
  not an accepted change to profile timing.
- Cold evdev injection began775ms before snapshot application, with maximum sender
  spacing100.59ms. Native delivery still had a527.11ms maximum gap; canvas
  replacement135.32ms. Counts remained152 models, one Mods view/11 rows and one
  Plugins view/14 rows. The yield showed no responsiveness benefit and was reverted
  byte-for-byte. Evidence switch-yield-measurement.json and its raw report.
- Mapped all event gaps above150ms to measured phases in
  artifacts/switch-yield-gap-phases.json. The initial snapshot/workspace gap was
  498.97ms; the largest527.11ms gap overlapped later Plugins body attachment
  (63.30ms measured), and another423.85ms gap overlapped Mods attachment139.10ms.
  These phase durations do not account for subsequent framework layout/style work.
  Do not attribute the entire maximum gap solely to the110ms old-panel teardown.
- No experimental production change retained. Full goal stays open; future
  performance work must cover subsequent panel realization/layout as well as
  workspace removal, with native input evidence.
- Restored Release build passed (one existing warning, zero errors); whitespace
  checks passed. Normal workspace and collapsed sidebar preference restored.


2026-09-14 — Skip hidden header-discovery branches:
- Previous turn was progress: rejected an ineffective snapshot yield and mapped
  later panel-realization input gaps. Re-examined the existing sampled trace after
  startup: header discovery111.83ms and row highlighting114.68ms appeared in
  inclusive samples. These overlap other frames and are not new latency timings.
  Evidence artifacts/post-startup-row-callback-profile.json.
- Header discovery now skips invisible ancestor branches while still yielding a
  hidden PageHeader directly, allowing it to reappear after enlargement. This
  removes unnecessary traversal without changing header dimensions/fade rules.
- Expanded paired-panel verification to constrain actual client height to250px,
  confirm both headers hide and tab bars remain, then enlarge to750px and confirm
  both headers return. Native geometry and divider hit testing passed afterward.
  Evidence artifacts/header-pruning-check.log.
- A valid evdev cold switch measured505.18ms maximum pointer gap (injector100.10ms,
  first delivered event699.11ms before snapshot). Model/row counts remain152,
  11 Mods and14 Plugins. This does not establish a material end-to-end speedup;
  retain only the bounded traversal reduction, not a claim of fluid switching.
  Evidence artifacts/header-pruning-measurement.json.
- Noticed the older full installed-interaction verifier still expects60->28px
  header icons; current design is48->28px. That historical full check must be
  corrected and rerun before being used as fresh four-line-scroll evidence.
  No changes to that verifier are retained in this turn.
- Release build and whitespace check passed. Normal workspace/sidebar restored.
  Overall goal remains open, including responsiveness and full acceptance.


2026-09-14 — Current four-line header evidence:
- Previous turn was progress: pruned hidden header branches and verified header
  restoration. Corrected the older installed-interaction assertion from obsolete
  60->28px to the current48->28px design; its full mutating workflow was not rerun.
- Added optional MO2_VERIFY_HEADER_SCROLL to the paired-panel check. It exercises
  four40px scroll increments in both long native Skyrim lists, checks icon sizes
  43/38/33/28, confirms visible description lines retain unbounded height and
  positive opacity for the first three steps, and verifies descriptions disappear
  after four steps and return at the top. No game state is mutated.
- Hardened the verifier to set AND verify client width1280 and height650, allowing
  page viewport layout to settle between resizing and scrolling. Initial attempts
  accidentally tested at restored width853 (page width341), which correctly
  triggered automatic compact mode. The initial FNV run also failed its scroll
  range precondition. Those attempts are not evidence of a header regression.
- The corrected native Skyrim run passed all four scroll steps, constrained-height
  hiding with retained tab bars, enlargement/restoration, native geometry and
  divider hit testing. Exact measured sizes and owner/window dimensions are in
  artifacts/header-actual-size-check.log. This is fresh bounded evidence for the
  four-line behavior; it does not claim a newly exercised pointer-wheel path.
- Release build passed (one existing warning, zero errors); whitespace checks
  passed. Normal workspace restored. Overall completion remains open, especially
  measured cold-switch responsiveness and full acceptance coverage.


2026-09-14 — Delayed file-read failures:
- Previous turn was progress: actual-width four-line header evidence. Reviewed
  Save/Data action completion guards; their activation/profile error checks are
  already present. No production action behavior changed in this turn.
- Extended the existing four-page lifecycle checker to delayed failures as well
  as successful responses. Each page is closed with a pending read and reopened
  before or after completion. Assertions reject error text or rows published into
  a detached view and stale errors in the reopened view; reopening must read fresh
  data. These are controlled reader failures, not simulated native file mutations.
- FNV and Skyrim passed all16 success/failure/reopen combinations, Saves selection,
  and independent native readback comparisons for Archives/Overwrite/Saves/Data.
  Evidence artifacts/file-failure-fnv.log and artifacts/file-failure-skyrim.log.
  No actual native action timeout, deletion or repair was induced, so these tests
  do not independently prove delayed mutation completion behavior.
- Release build and whitespace checks passed; normal workspace restored. Overall
  goal remains open for cold-switch responsiveness and full acceptance coverage.


2026-09-14 — Guard plugin details against departing profiles:
- Previous turn was progress: delayed read-failure coverage passed for both games.
  Inspected current row menu construction and command guards. No lazy-menu or
  other speculative construction optimization was introduced.
- Plugin details was the exception to surrounding target guards: it could select
  through a row captured for a departing profile. The click callback now returns
  if its captured target differs from the active profile, before looking up row
  selection or changing the page details state. Native mutation menu items already
  guard their captured targets.
- Release build and whitespace checks passed. This small read-only selection guard
  was source-reviewed; no new cross-profile pointer-race runtime claim is made.
  Normal workspace relaunched. Responsiveness and full acceptance remain open.


2026-09-14 — Scope highlight traversal to the main tables:
- Previous turn was progress: guarded departing-profile Plugins details clicks.
- Both highlight callbacks now pass the existing main TreeDataGrid directly to
  Mo2RowHighlights.Apply instead of the entire native page/editor. This removes
  traversal of unrelated page chrome and hidden alternative table branches.
  Conflict/link color rules, row decoration and update subscriptions are unchanged;
  the table retains the same PanelView ancestry used for sizing.
- Current FNV and Skyrim checks passed rapid debounced filtering, empty/clear,
  scrolling both lists, and realized handle/toggle correspondence to native models.
  Native activation and priority/order remained unchanged. Evidence:
  artifacts/table-highlight-fnv.log and artifacts/table-highlight-skyrim.log.
  These checks do not independently exercise every conflict-color permutation.
- Release build and whitespace checks passed; normal workspace restored. This is
  a bounded reduction of traversal work, not a measured end-to-end responsiveness
  improvement. Cold-switch performance and full acceptance remain open.


2026-09-14 — Native linked-plugin and conflict colors:
- Previous turn was progress: table-scoped highlight traversal passed row recycling
  checks. Added a native selection/highlight check to that existing live workflow.
- The checker reads selectionLinks through MO2, selects the corresponding mod in
  the actual frontend table, waits for frontend sets to match native readback,
  then scrolls every row into view to verify purple linked-plugin and red/green
  conflict brushes. Clearing selection must remove those brushes after recycling.
  It selects the other side of an available conflict to exercise both directions.
- FNV passed one linked plugin (no conflicts for that candidate). Skyrim passed
  a real pair: first selection one linked plugin/one red conflict row; counterpart
  selection one linked plugin/one green conflict row. Both cleared correctly.
  Native activation and order remained unchanged. Evidence:
  artifacts/highlight-behavior-fnv.log, artifacts/highlight-behavior-skyrim.log,
  and artifacts/highlight-directions-skyrim.log for the two-sided check.
- Uses native read-only conflict/model APIs, which briefly inspect original mod
  details without showing them. No fixture mods/files were needed. These results
  now provide direct rendered-row color evidence after table-scoped traversal.
- Release build and whitespace checks passed; normal workspace restored. Overall
  goal remains open for responsiveness and remaining acceptance coverage.


2026-09-14 — Skip custom entry bodies when decorating rows:
- Previous turn was progress: native purple/red/green highlight readback and
  clearing passed, including selecting both sides of a real Skyrim conflict.
- Mo2RowHighlights now enumerates only surrounding row chrome, stopping at each
  Mo2DeferredRow. Custom entry labels/buttons/icons contain none of the native
  borders, presenters or cells this helper decorates. Removed the obsolete
  thumbnail traversal; current redesigned row bodies contain no thumbnail controls.
  No visual cache or theme-dependent reuse assumption was introduced.
- FNV linked-plugin clearing and Skyrim both red/green directions passed again,
  alongside rapid filters and recycled row handle/toggle checks. Native activation
  and order remained unchanged. Evidence row-decorations-fnv.log and
  row-decorations-skyrim.log.
- Cold evdev measurement remains537.22ms maximum input gap, with sender100.27ms and
  first delivered event664.68ms before snapshot. Counts remain152 models and one
  set of11 Mods/14 Plugins rows. This does not prove an end-to-end speedup; retained
  change is bounded traversal reduction with verified decoration behavior.
  Evidence artifacts/row-decorations-measurement.json and its raw report.
- Release build/whitespace checks passed. Normal workspace/sidebar restored.
  Responsiveness and full acceptance remain open.


2026-09-14 — Round-trip panel reuse experiment:
- Previous turn revalidated Steam ownership detection on both installed games and
  restored the normal frontend after diagnostics; no new ownership feature claim.
- Added opt-in MO2_UI_LATENCY_ROUND_TRIP measurement: current game -> target ->
  original -> target, with six seconds between switches to include deferred layout.
- Compared fresh processes with the same isolated layout and 250 physical evdev
  pointer moves over the blank top bar. Both recorded input before the first switch.
- Baseline maximum delivered pointer gaps: cold target 486.1 ms, return original
  248.8 ms, revisit target 295.8 ms. Weakly caching panel wrappers produced 527.7,
  449.2 and 474.9 ms respectively. These are event-delivery gaps, not frame times;
  one paired comparison does not establish a general performance distribution.
- Both runs constructed three Mods views and two Plugins views during sampling:
  retaining panel wrappers did not retain their page bodies across this lifecycle.
  Removed the cache experiment rather than enabling it. Retained round-trip probe.
- Evidence: artifacts/panel-reuse-comparison.json and ui-latency-panel-reuse-
  {baseline,cache}.json with matching native-pointer injection artifacts.
- Final host-only Release build passed (one existing dependency advisory warning).
  Game switching remains visibly blocking; responsiveness requirement is not met.


2026-09-14 — Detached page reuse and early-navigation fix:
- Previous goal turn made progress by rejecting outer-panel caching with measured
  evidence and retaining a round-trip probe. This turn tested page-body handoff.
- Opt-in MO2_REUSE_PAGE_BODIES=1 retains a detached Mods/Plugins body for the same
  page-model identity. It does not take a body from a view with a visual root.
  Default behavior remains unchanged pending explicit concurrent-host and teardown
  lifecycle checks; this is not yet a production responsiveness improvement.
- Round-trip measurement constructed one Mods and one Plugins view, reusing three
  page bodies. Delivered pointer maxima: cold target 378.3 ms, return original
  292.4 ms, revisit target 176.0 ms. Baseline revisit was 295.8 ms. One paired run
  is not a performance distribution and the remaining cold pause is unresolved.
  Evidence: artifacts/page-body-reuse-measurement.json and full matching probe.
- Initial integration check exposed a NullReferenceException in upstream
  WorkspaceController.CreateOpenPageBehavior before deferred workspace selection
  activated. Fixed it to fall back to an existing panel/tab, or NewPanel/NewTab
  where needed. This production navigation fix is independent of the reuse flag.
- Full Release build passed (48 existing warnings). Re-running the failing check
  passed same-profile and rejected-profile selection, FNV/Skyrim/return/revisit,
  retained body identities, rapid filters, empty/clear, both scroll rails, recycled
  handle/toggle identities, linked-plugin colors and both conflict directions.
  Native activation/order remained unchanged. Evidence: page-reuse-verify.log
  (original failure), page-reuse-verify-fixed.log (complete PASS sequence).
- Extended current FNV shared-source audit to include WorkspaceController.cs:
  twelve files compared, six identical and six with documented differences.
- Overall goal remains open: page reuse lifecycle checks and stable fluid game
  switching are still unproven, in addition to the full feature completion audit.


2026-09-14 — Page reuse enabled after lifecycle verification:
- Previous turn made progress by fixing early navigation and measuring detached
  body handoff. This turn replaces its opt-in status: Mods and Plugins explicitly
  request body reuse, enabled by default; MO2_REUSE_PAGE_BODIES=0 disables it for
  comparison. Other page types and outer panels do not request this behavior.
- Live-window lifecycle fixtures verify retained text/control identity, exclusive
  handoff from a detached owner, simultaneous independent hosts, reopening an
  emptied owner, cancelled deferred construction and rapid model replacement.
- The first combined check found the test's fixed 100 ms completion assumption
  too short under concurrent game switching. Changed that assertion to await the
  replacement body itself. Production code did not change for this test failure.
- Final combined default-build check passed all lifecycle cases plus actual
  FNV/Skyrim/return/revisit body identities, search filtering, empty/clear, both
  scroll rails, recycled row toggle/grip identities, linked-plugin highlights and
  both native conflict directions. Activation and priority/order stayed unchanged.
  Evidence: artifacts/reuse-settled-verify.log; earlier timing-assumption failure
  and the passing native-state checks remain in reuse-default-verify.log.
- Default-build round-trip sampling built one Mods and one Plugins view and reused
  three page bodies. Maximum delivered pointer gaps were 420.0 ms cold target,
  230.3 ms return original and 170.2 ms revisit target (recorded baseline 486.1,
  248.8 and 295.8 ms). An earlier reuse run also measured 176.0 ms on revisit.
  These few runs demonstrate reduced construction and a smaller measured revisit
  gap, not a guaranteed latency distribution. Cold-switch responsiveness remains
  incomplete. Evidence: page-reuse-default-measurement.json, full corresponding
  UI latency report and native-pointer injection artifact.
- Final host-only Release build passed with one existing dependency advisory;
  root and upstream diff whitespace checks passed. Normal frontend restored with
  the user's layout/sidebar state and no diagnostic flags. Full goal stays open.


2026-09-14 — Cached native view resolution and first-display trace:
- Previous turn made progress by enabling page reuse after lifecycle and native
  round-trip checks. This turn removes repeated assembly-wide reflection from
  FixtureViewLocator: candidate view types are collected once, and each model
  interface resolves to a cached Type. Each request still constructs its own view
  and applies its contract; explicit MO2 page branches are unchanged.
- --check-view-locator passed all 71 discoverable native model mappings against
  the original resolution algorithm, including repeated lookups and missing-view
  failure. Host-only Release build passed with one existing dependency advisory.
- Fresh round-trip runtime retained the expected one Mods/one Plugins construction
  and three reused page bodies; both games connected. Pointer gaps: cold 545.4 ms,
  return 230.2 ms, revisit 168.4 ms. Caching reflection did NOT demonstrate a cold
  input improvement. Evidence: view-locator-cache-measurement.json and full probe.
- The largest cold pointer gap overlaps Plugins body construction for only 60.3 ms;
  fourteen plugin row bodies build in that gap. Most delay follows construction.
- Captured a fresh .NET sampled-thread-time trace after the first trace command
  terminated with an unsupported profile name; the corrected profiler completed.
  Trace is /tmp/mo2-plugins-layout.nettrace and matching speedscope JSON. Main
  thread is 316728; Plugins constructor samples begin at 16007 ms trace time.
  In 16000–16800 ms, inclusive style application accounts for 503.5 ms and measure
  for 482.4 ms (overlapping stacks, not additive CPU totals). This instrumented
  run cannot be compared directly to uninstrumented pointer latency.
- Aggregates: artifacts/plugins-first-layout-hotspots.json (focused interval),
  plugins-layout-hotspots.json and plugins-layout-styling-owners.json (10–20 sec).
  The next performance target is first-display style/layout work, not resolver
  reflection or outer-panel caching. Normal frontend/layout/sidebar restored.
  Full goal remains open; cold responsiveness is still unproven/incomplete.


2026-09-14 — Retain Plugins column across page activation:
- Previous turn made progress by caching view resolution, checking all native
  mappings, and locating the remaining first-display styling/layout cost.
- Found Plugins clearing/replacing its custom column in every activation callback.
  It now retains a column per page model, configures source changes before binding
  the native editor, and skips replacement when the source already has that column.
  Row templates capture their owning page explicitly. Native editor, themes, row
  presentation and ordering commands remain the same.
- Release build passed with one existing dependency advisory. Extended native
  round-trip verification passed column reference identity on revisiting Plugins,
  same-profile and rejected-profile selection, cached page identities, filtering,
  both scroll rails, row toggle/grip identities, linked-plugin highlights and both
  conflict directions. Native activation and order remained unchanged. Evidence:
  artifacts/column-retention-check.log (complete PASS sequence).
- Uninstrumented pointer measurement: cold target 533.9 ms, return original 282.6,
  revisit target 199.3. This does not demonstrate a latency improvement over the
  preceding 545.4/230.2/168.4 ms run. View and row construction counts also remain
  unchanged; the broader first-display style/layout bottleneck is unresolved.
  Evidence: column-retention-measurement.json and matching full/injection reports.
- Root/upstream whitespace checks passed. Normal frontend, layout and sidebar
  restored without diagnostics. The full goal remains open.


2026-09-14 — Activation-toggle template observer lifetime:
- Previous turn made progress by retaining the Plugins column and verifying its
  identity and native behavior; pointer timing did not improve. This turn inspected
  shared theme/list templates and found a concrete observer-lifetime defect in
  the host activation toggle used by Mods and Plugins.
- The template previously subscribed an anonymous handler to the toggle's
  PropertyChanged event and never removed it. Detached/replaced icons continued
  observing and were retained by the live toggle. The icon now subscribes while
  visually attached, unsubscribes when detached, and synchronizes on reattachment.
  Template appearance and click behavior remain unchanged.
- MO2_VERIFY_TOGGLE_LIFECYCLE=1 passed in a real window: four detach/reattach cycles
  and four template replacements; disconnected icons no longer respond to state
  changes, and current icons always reflect the checked state on reattachment.
- The simultaneous native profile-buffer suite also passed same/rejected profile
  selection, FNV/Skyrim/return/revisit, retained page/column identity, filtering,
  empty/clear, both scroll rails, row grips/toggles, linked-plugin highlighting and
  both native conflict directions. Native activation and order stayed unchanged.
  Evidence: artifacts/toggle-lifecycle-live.log (complete PASS sequence).
- Host-only Release build passed with one existing dependency advisory. No claim
  of reduced first-display latency follows from this lifecycle fix; the broader
  styling/layout pause remains unresolved. Normal frontend/layout restored and
  the full goal remains open.


2026-09-14 — Archives selection and current goal checkpoint:
- Previous turn made progress by fixing activation-toggle observer lifetime and
  verifying native round trips. This turn inspected broader feature requirements
  and found an Archives selection defect: explicit deselection was not remembered,
  and a removed/reappearing archive could silently regain selection.
- Archives selection callbacks now ignore replaced sources and remember explicit
  clearing. Successful refresh forgets selections whose name/owning-mod pair is
  absent. Filtering still restores a temporarily hidden selection by that identity.
- New fixture check passed duplicate names from distinct mods, filtering, explicit
  clear, reordered/changed-state refresh, removal/recreation and Browse/Extract
  availability. The full FNV file-page suite passed delayed success/failure and
  reopen timing for Archives, Overwrite, Saves and Data, plus native table readback.
  No archive extraction/deletion or actual game-file mutation was performed.
  Evidence: artifacts/archive-selection-lifecycle.log.
- Release build passed with one existing dependency advisory. Added CURRENT_STATUS.md
  to separate open requirements, current checks and historical workflow evidence;
  the chronological audit no longer leads with an unqualified current-status claim.
- Rechecked all twelve shared-component current hashes against the recorded FNV
  comparison: unchanged, six identical and six documented differences. Inspected
  structured Overwrite and Skyrim extraction records; the latter records 14,031
  files (not 10,031), matching paths/bytes and unchanged native state.
- Full goal remains open, including first-display responsiveness and final combined
  workflow/UI review. Normal frontend/layout restored without diagnostic flags.


2026-09-14 — Physical screen review and Tools read lifecycle:
- Previous turn made progress by fixing Archives selection and separating current
  checks from historical evidence. This turn inspected the running frontend at
  its actual 1280×750 client size and used physical evdev clicks in an isolated
  copy of the user layout. External Files, Mods, game-scoped Profiles and Tools
  fit the full-width window and responded to their sidebar navigation.
  Screens: current-screen-review.png, screen-review-mods.png,
  screen-review-profiles.png and screen-review-tools.png in artifacts.
- Checked the internal bridge entry seen in Tools: its original display action
  only reports the endpoint. It does not launch another frontend and was left
  intact. No tool, game or native dialog was launched during this review.
- Found Tools missing the activation guards already present in file pages. A
  completed request could update a detached page or schedule another read after
  target/connection changes. Added active/generation guards, stale error/result
  rejection, inactive row clearing and a fresh read after reopening.
- MO2_VERIFY_TOOLS_LIFECYCLE=1 passed controlled delayed success/failure for
  reopening before and after completion. The latter waits on actual read state,
  not an assumed timeout. Old content/errors are rejected and detached launch
  actions are absent. Evidence: artifacts/tools-lifecycle-live.log.
- Physical Refresh on the ordinary native Tools page completed and rendered the
  original executables/extensions correctly: screen-review-tools-fixed.png.
- Release build passed with one existing dependency advisory, and whitespace
  checks passed. Normal user layout/frontend restored without diagnostic flags.
  This is a partial screen review, not final keyboard/panel-combination coverage.
  First-display responsiveness and the full goal remain open.


2026-09-14 — Keyboard search focus regression fixed:
- Previous turn made progress by guarding Tools reads and performing physical
  full-width screen checks. This turn used an isolated paired Mods/Plugins layout
  and physical evdev pointer/keyboard input at 1280×750.
- Ctrl+F and text entry initially worked independently in each focused panel.
  Reproduced a defect: Escape hid the focused search textbox, leaving no focused
  page descendant; a subsequent Ctrl+F failed until another pointer click.
  Before-fix evidence: keyboard-review-reopen-search.png (search stays closed).
- Shared SearchControl now focuses its search button before hiding a focused
  search panel, both for Escape/clear and Ctrl+F toggling. Programmatic clearing
  while focus is elsewhere does not steal that focus. Markup/styles are unchanged.
- MO2_VERIFY_SEARCH_FOCUS=1 passed four repeated routed-key cycles covering
  Ctrl+F/Escape/reopen, Ctrl+F close/reopen and outside-focus preservation.
  Evidence: artifacts/search-focus-fixed.log.
- Physical keyboard checks passed the formerly failing sequence in both real
  lists. Separate queries remained independent. Also passed after an actual
  Skyrim -> FNV return and after closing/reopening the Plugins panel. Screens:
  keyboard-review-plugins-reopen-fixed.png, keyboard-review-both-searches-fixed.png,
  keyboard-review-skyrim.png, keyboard-review-returned-fnv.png and
  keyboard-review-reopened-panel.png. No install/launch/toggle/reorder action was
  invoked in this keyboard review. This is not exhaustive keyboard coverage.
- Full Release build passed (48 existing warnings), followed by a successful
  host-only verifier build (one existing dependency advisory). Updated FNV source
  comparison to include SearchControl's existing MO2 query support and new focus
  fix: thirteen shared files, six identical and seven documented differences.
- Normal user layout restored with no diagnostic flags. First-display performance,
  final combined workflows and the remaining UI review keep the full goal open.

## 2026-09-14 — Steam-owned versus external game files

Rechecked the existing External Files implementation for the requested Steam-based detection. The Release scanner passes its ownership, DLC, case/slash normalization, extra-file, symlink, invalid-path, missing-manifest and cancellation checks. Fresh read-only scans of the supplied trailing-slash game paths report FNV: 17 depots, 464 Steam paths, 11 external entries; Skyrim SE: 3 depots, 46 Steam paths, 2,239 external entries. These counts describe path ownership, not modified original files.

Reviewed the FNV reference fork: its External Changes group and clean-folder dialog also provide managed keep/clean behavior. Our current page supports detection, search and reveal only; it does not yet reproduce that managed import/cleanup workflow. No game files were changed. Restored the normal frontend and saved user workspace after the preceding popup diagnostic run.

## 2026-09-14 — External Files retained-view lifecycle

Previous turn classified as progress: fresh Steam scans and reference-fork inspection established the existing detection scope and the missing managed cleanup scope. This turn fixed a concrete lifecycle gap: a closed retained External Files view kept its scan rows and enabled Reveal action. Deactivation now clears the scan, rows and status and disables Reveal; Reveal also checks active state. An injectable read preserves the production Steam scanner and enables controlled delayed responses.

Release build passed (one existing telemetry package advisory). `external-files-lifecycle.log` passes delayed success/failure with reopening before/after completion, fresh row identity, absence of stale error and disabled/empty closed state. No game files, activation or priority were changed. Normal workspace restored after verification. Cold first-display latency, final combined workflow and remaining UI review are still open.

Prior popup verification evidence is now linked in CURRENT_STATUS: `login-fit-diagnostic.log` verifies 600×344 popup in a 1280×750 owner and 456×296 in a 480×320 owner, two cancellation/reopening cycles each. Earlier runs failed before compact-owner geometry because of verifier window-state setup; they are not measured proof of previous oversizing. The native overlay markup stays unchanged; the host adjusts layout width and wraps it in a height-constrained ScrollViewer. Subsequent `account-status-after-popup-fit.log` confirms three of three native account connections. This is not a fresh SSO or download/install run.

## 2026-09-14 — Direct row icon experiment rejected

Previous goal turn was progress: External Files lifecycle fix and live stale-result checks. Returned to the unresolved responsiveness requirement. Inspected current first-switch phases: snapshot apply 71.5 ms, workspace canvas replacement 166.2 ms, sidebar retarget 37.0 ms, followed by deferred body work. Tested removing UnifiedIcon wrappers from the shared icon-button helper, activation check and plugin lock while preserving underlying Projektanker glyphs and sizes.

Build passed; rendered paired Skyrim panels are recorded in `direct-icons-screen.png`. Real evdev pointer injection (250 moves, no clicks) and round-trip diagnostic completed: `direct-icons-measurement.json` records cold/return/revisit max delivery gaps of 501.6/248.3/169.7 ms. Earlier retained-build measurement was 533.9/282.6/199.3 ms, but historical runs vary enough that this single experiment does not establish an improvement. Cold switching remains unacceptable. Reverted all three experiment files byte-for-byte, rebuilt successfully and restored the normal workspace. No icon change retained; no gameplay, authentication or transfer result claimed. The next responsiveness investigation should target workspace/panel styling and layout rather than this wrapper micro-optimization.

## 2026-09-14 — Mounted workspace experiment rejected

Previous turn was progress by measured negative evidence: direct icon wrappers were ruled out as a demonstrated solution. This turn tested retaining native WorkspaceViews in a host Grid and hiding inactive roots, rather than tearing down each workspace on navigation. Experiment was gated by MO2_KEEP_WORKSPACES_MOUNTED. Real evdev delivery measurement completed with the same isolated round-trip layout. `mounted-measurement.json` records cold/return/revisit gaps of 638.6/245.4/188.7 ms. Mod-model constructions doubled to 632 versus retained baseline 316, while visible row/view construction counts were unchanged. Inspection confirms hidden visual roots remain activated and subscribe to the shared profile snapshot, so hiding does not suspend expensive data work.

Rejected before broader functional testing because it worsens cold latency and duplicates inactive work. Restored Mo2LiveWorkspace.cs byte-for-byte. This was an experiment, not a shipped caching feature or a claim that hidden-page behavior passed. A useful retained-workspace approach would require suspending inactive presentations independently of visual-tree lifetime; naive visibility caching is insufficient.

## 2026-09-14 — Profile-bound Mods presentations

Previous goal turn was progress: mounted-root experiment established that hidden active subscriptions doubled mod-model construction, so naive retention was rejected. Implemented profile-bound filtered Mods streams: each subscription keeps a raw SourceCache for its original target, ignores selection-in-progress and different targets, and reconciles only changed/removed records when its own profile returns. ContentRevision skips unchanged status notifications. The adapter retains the first presentation target across reactivation. The native data provider and mutation paths remain authoritative.

Added a live retained subscription assertion to the profile-buffer verifier: switching to Skyrim must leave its FNV model count, references and notification count unchanged. Final Release build passed, and `target-presentation-final-check.log` passes that assertion plus same/rejected profile source/selection identity, FNV/Skyrim round trips, Plugins column identity, rapid filtering, empty/clear, both-list scrolling, recycled handles/toggles, red/green/linked highlight checks and unchanged native activation/order. Normal workspace restored after verification.

This is retained functional groundwork for inactive presentation suspension, not proof of fluid switching. Workspace mounting remains absent. Further retention work must account for Plugins and other view subscriptions as well as new latency measurements. Final combined end-to-end workflow remains open.

## 2026-09-14 — Plugins isolation and successful repeat-switch retention experiment

Previous turn was progress: Mods profile isolation implemented and verified. Added an equivalent raw per-subscription Plugins cache gated by original target, selection completion and ContentRevision. ScenarioOrderProvider uses it only for the live profile’s own order; unrelated varieties keep their original stream. Inline component updates and native mutations are unchanged. Extended the live verifier to hold a Plugins provider subscription across the other-game switch and require unchanged identities/count/notifications. `plugin-target-check.log` passes this plus the Mods assertion, provider movement bounds for 151/152/2 items and reversed direction, native round trips, filters/recycling/highlights and unchanged activation/order.

Retested mounted native WorkspaceViews with both streams isolated. `isolated-mounted-measurement.json` records 152 mod models (previous naive mounting: 632), 14 plugin rows (previous: 28), 11 mod rows (previous: 34), and cold/return/revisit native pointer gaps of 436.4/103.8/118.2 ms. Input injector cadence is 100 ms, so the repeated-switch gaps are close to that floor; one run is not a latency distribution. Cold switching remains visibly delayed. `isolated-mounted-check.log` passes the complete paired-list functional suite on this prototype.

Saved exact prototype patch, then restored the pre-experiment workspace source (which includes the retained provider wiring). Always-mounted workspaces are not ready for normal use: non-list pages such as External Files subscribe to game changes and refresh while hidden, and the prototype has no eviction policy. Next step can use bounded retention restricted to eligible list workspaces, or general inactive-page suspension; it must test navigation/disposal and repeat timing. Stream isolation remains in the normal build.

## 2026-09-14 — Bounded workspace retention enabled

Previous turn was progress: Plugins isolation and measured successful repeat-switch prototype. Implemented Mo2RetainedViews with reference-keyed identity, capacity three including the active view, least-recently-used eviction and explicit release. Live workspaces are eligible only while still registered, game-scoped, nonempty and all tab contents are Mods/Plugins. Other views release WorkspaceView.ViewModel and detach when left; closing the window clears the cache. This retains native NMA panels/dividers, while utility/file pages retain normal deactivation.

Release build passed (one existing telemetry advisory). `bounded-cache-check.log` passes attached-control cache identity/visibility/LRU/ineligible eviction/rebuild/exactly-once clear, both retained data subscriptions, complete FNV/Skyrim profile-buffer filtering/recycling/highlight/native-order suite, and native Tools → Home → Tools navigation proving original workspace detached/released and reopened with fresh controls. A verifier namespace compile error was corrected before this successful build/run.

`bounded-cache-measurement.json` records cold/return/revisit native pointer gaps of 356.6/127.5/126.0 ms with 100 ms evdev injection cadence, 152 mod models, 14 plugin rows, 11 mod rows, and one new Mods/Plugins view each during sampling. Cold switching remains delayed; one run is not a latency distribution. `bounded-cache-screen.png` shows the returned paired Skyrim layout. Normal user workspace restored with bounded retention enabled. Final combined login/download/install/FNV workflow and remaining keyboard/UI review remain open.

## 2026-09-14 — Cached workspace focus and physical search review

Previous turn was progress: bounded retention enabled after lifecycle/navigation and input-delivery verification. Added an attached text-box focus assertion to the cache verifier. `cache-focus-check.log` passes both cache lifecycle assertions and automatic focus removal when the owning cached view is hidden. No production focus fix was required.

Physical input review initially did not enter text reliably. A temporary opt-in trace logged only event types/targets, focus and text lengths, never typed content. Events reached SearchTextBox but produced no TextInput and typing the f in configuration closed search; cycling Control restored TextInput. A stuck modifier in the test input path is an inference, not a proven frontend defect. XQueryKeymap reported no physically held modifiers. Several preliminary screenshots show incomplete attempts and are not acceptance evidence.

After input recovery, `cache-keyboard-fnv-tribal.png` shows the tribal query and matching FNV row. Actual sidebar navigation visited Skyrim and returned to FNV, where `cache-keyboard-returned-tribal.png` confirms the query persisted. Clicking the search box, Escape, Ctrl+F and typing caravan produced `cache-keyboard-reopened-caravan.png` with the expected row. The Skyrim unmanaged-query attempt was not successful evidence and is not claimed as such. No mod toggle, ordering, installation or launch action was intentionally invoked.

Removed temporary input tracing and rebuilt; retained only the focus regression assertion and evidence. Normal workspace restored. Broader keyboard/UI review, cold first display and the combined end-to-end login/download/install/gameplay workflow remain open.

## 2026-09-14 — Cached workspace resizing and headers

Previous turn was progress: focus regression plus a verified physical cached Mods search round trip. Extended the opt-in profile-buffer suite with MO2_VERIFY_CACHED_RESIZE: resize while Skyrim is hidden, return at 900/640/1280 window widths, require current canvas dimensions and native panel bounds, preserved Mods body identity and reachable divider via hit testing. Reused the header verifier on the returned Skyrim workspace.

Final Release build passed; missing Avalonia extension namespaces in the initial verifier build were corrected. `cached-resize-check.log` passes the width checks, four actual 40-pixel scroll steps (header icons 43/38/33/28 from 48), unclipped visible descriptions, restoration at top, headers hidden with tabs retained at height 250 and restored at 750. The subsequent full live suite passes filtering, empty/clear, recycled handles/toggles, both conflict directions, linked plugins and unchanged native activation/order. Only verification code changed this turn; no new layout fix was required. Normal workspace restored. This is not exhaustive narrow-control or keyboard acceptance, and it does not close cold latency or the combined end-to-end workflow.

## 2026-09-14 — MO2 layout preset and Undo verified

Previous turn was progress: cached width/header behavior and native-state checks passed. Added an opt-in temporary-layout-only verifier invoking the toolbar preset/Undo MenuItem click callbacks. It verifies one Mods tab left and Plugins/Data/Archives/Saves/Downloads right, each right page visibly attaches when selected, persisted preset data equals the captured model, and Undo restores the original captured geometry/tabs/search/selection live and on disk. `layout-preset-check.log` passes; Release build and whitespace check pass. Only verifier/accessibility-for-verification code changed, not preset production behavior. Normal workspace restored.

Read the prior FNV launch procedure and current isolated inputs in preparation for the remaining gameplay workflow. The existing Windows key injection helper is available, current plugins.txt has 11 non-comment lines, and no controller-disable key was found in fallout.ini/falloutprefs.ini/falloutcustom.ini. Strict ConfigParser rejected a pre-existing warning footer in fallout.ini, so the targeted setting scan used line parsing instead; no INI was rewritten. No fresh launch was performed, and historical 14-plugin index expectations must not be assumed for the next run. Cold first-display lag and combined end-to-end verification remain open.

## 2026-09-14 — Fresh compact-sidebar FNV gameplay

Previous turn was progress: preset/Undo live verification and current launch preflight. Backed up all 12 isolated Frontend Test profile files and four save files, temporarily set bDisable360Controller=1 in falloutprefs.ini. Three initial verifier attempts never launched the game: stale adapter-readiness assumptions, Home sidebar lookup and collapsed-sidebar flyout lookup. Corrected the verification path to require the connected profile, select its workspace, open the compact tool flyout, choose NVSE and execute its existing sidebar command. No production launch behavior was changed.

The successful attempt loaded the test save, displayed xNVSE 6.4.8, reported GetNVSEVersion 6 and MCM GetModIndex 0A (matching current loadorder.txt). Windows key injection moved forward; the viewpoint changed and HUD displayed. Pause had no MCM entry. The current nvse.log was updated after the run backup and reports MCM.dll loaded correctly; the MCM ESP is absent physically from game Data, consistent with the MO2 virtual mapping. Author Examples is installed but disabled. This does not prove the reason for the missing pause entry; do not claim the MCM interface passed.

Console qqq exited normally; current-gameplay-compact.log reports NVSE exited (code 0). Restoration record shows only falloutprefs.ini differed among the 12 profile files, all original bytes restored, all saves unchanged and no new saves. A later hash check after normal frontend restart still confirms profile restoration. No Skyrim launch or mod-order mutation was requested. Normal app restored. Current gameplay is now direct evidence, while MCM menu behavior, cold input lag and the combined popup/account/NXM/install/gameplay flow remain open.

## 2026-09-14 — MCM menu absence explained by configuration evidence

Previous turn was progress: fresh current-build gameplay, xNVSE/MCM index, movement, normal exit and restoration. Investigated the absent pause entry before changing any mod state. Native readDataDirectory reports The Mod Configuration Menu as sole origin for menus/options/start_menu.xml, menus/prefabs/includes_StartMenu.xml and NVSE/Plugins/MCM.dll. Archive invalidation is already enabled. Extracted the installed ESP’s SCTX source read-only: MCMMainScript explicitly returns on ListGetCount MCMMods == 0 with the diagnostic Fault: No MCM Mods. Current active plugins are base/DLCs plus MCM; Author Examples is disabled. This matches the condition described in the earlier FNV acceptance investigation.

Saved current-mcm-menu-condition.json. The current configuration supports the inference that no registered consumers explain the absent menu; no fresh runtime form-list-count query or enabled-consumer menu-opening run was performed. Do not claim that interface was freshly exercised, or modify scripts/XML to force it visible. No source, mod, profile or game settings changed this turn; normal frontend remains running. Next priority remains the actual combined login/account/NXM/install workflow and cold first-display latency, rather than treating an empty MCM consumer setup as a proven frontend mapping defect.

Hidden StandardButton icon prototype (REVERTED): deferred assigning left/right
UnifiedIcon.Value while the icon slot was hidden; ShowIcon changes synchronized
current values before showing, preserving existing content on unchanged reshow.
The themed control check passed initial hidden values, all visibility modes,
null clearing, hidden value updates and template replacement:
button-icons-behavior-check.log. The paired profile/resize/header/filter/recycling/
conflict suite also passed in button-icons-panels-check.log, with native mod and
plugin state/order unchanged.

Uninstrumented native-pointer comparisons did not establish a repeatable gain:
candidate/baseline cold gaps 301.7/618.2ms, then reverse baseline/candidate
560.4/580.4ms. View/model/row counts matched. button-icons-latency-comparison.json
marks the prototype REVERTED. Restored StandardButton.cs byte-for-byte from the
pre-experiment copy, removed the opt-in check and Program hook; prototype saved
under /tmp/mo2-hidden-button-icons/prototype. Source restore writes a fresh
timestamp so the shared UI library is rebuilt. No theme or icon asset changes.

Avoid repeating this hidden-icon-only experiment as a cold responsiveness fix
without new evidence. Template realization diagnosis still identifies shared
button attachment/styling as expensive; reducing hidden icon creation alone did
not reliably reduce the complete cold input stall. The preceding goal turn was
progress through timings distinguishing construction from attachment.

## 2026-09-16 — Every MO2 tab widget driven; two dead controls found

Previous turn was progress: MO2's own row menus compared both ways round on all six
lists. This turn audited the widgets around those lists rather than the rows in
them. Baseline on the live FNV host: `MO2_VERIFY_QT_WIDGETS` and
`MO2_VERIFY_WIDGET_BEHAVIOUR` both passed before any change, so the gap was
coverage, not failure — eleven controls were drawn and never worked.

Two were doing nothing. Plugins' Sort/Restore/Save on MO2's espTab row were always
enabled regardless of what MO2 reported, and the actions behind them return
silently when MO2 cannot take them; they now follow `CanChangeOriginalUi` and
`CanSortPlugins` as the mod pane's equivalents and the toolbar's own Sort already
did, and Sort carries MO2's own unavailability reason. Downloads' Refresh only
recomputed the page's own control states where MO2's runs
`downloadManager()->refreshList()`; a new `refreshDownloads` bridge action presses
MO2's btnRefreshDownloads, waking its lazy download tab first, and the page applies
the returned snapshot.

The behaviour check gained: the active plugin count against MO2's own; both Save
buttons driven through to the backup files MO2 writes in its profile folder, with
the copies this check caused removed afterwards; both Restore buttons held to MO2's
enablement, their modal pickers deliberately not opened; Sort held to MO2's answer
without starting its LOOT run; the mod pane's And/Or category pair against MO2's own
category text both ways round; Data's Conflicts only counted against the rows that
are folders or served by more than one mod; Data's hidden files both directions;
Data's Refresh required to rebuild its tree, not merely leave rows in place; and
Downloads' Refresh required to reach MO2 and come back with MO2's own list.

Four controls had nothing on this host to exercise them and are reported by name as
not exercised rather than passing quietly: And/Or, Data hidden files, archive-served
Data rows and hidden downloads.

`MO2_VERIFY_PANEL_CHROME` and `MO2_VERIFY_COLUMN_TOGGLE` were failing and are not
app defects: both assert a tab toolbar that was deliberately removed, and MO2
offers a column chooser on its downloads header alone. Confirmed pre-existing by
rebuilding the unchanged sources and reproducing both. Both now assert the current
arrangement including its negative. Panel chrome had been failing on its first
panel and taking the other six with it.

Runs on the live FNV host after an MO2 restart for the plugin change: widget
behaviour PASS (33 statements), Qt widgets PASS, row menus PASS (33/9/13/7/1/3
entries), tab drag PASS, and the audit group PASS nine for nine including separator
colour and density. All 24 profile files hash identically to before the session and
no `modlist.txt.*` backup was left behind. Saved workspace layout is as it was
found. MO2 2.5.2 hung after closing its window and was terminated after it had
removed its bridge endpoint; its profile files were unchanged across the restart.

Open and unchanged by this turn: cold first-display latency, the combined
end-to-end workflow, and a column chooser on Downloads, which MO2 has and this
frontend does not.

## 2026-09-16 — Downloads column chooser and MO2's filter borders

Previous turn was progress: eleven untouched tab widgets driven and two dead
controls fixed. This turn closed the gap that turn found and two more beside it.

Downloads now carries MO2's column chooser on its heading strip
(`DownloadListView::onHeaderCustomContextMenu`) and starts with the four columns
MO2 hides. `Mo2ColumnToggle` gained a default hidden set — so Reset returns to
MO2's defaults rather than to everything shown — and a pinned set, so the name is
off the menu as MO2 leaves it off. The page's column widths are keyed by MO2's
column names instead of by position, which would have set the wrong column's width
once one could be hidden. Panels that name neither default nor pinned columns are
unchanged.

The mod list now shows that it is filtered, as MO2 does: `#f00` around the list and
the active count while a filter is on, `#337733` around the list while it is
grouped and not filtered. Drawn over the list rather than around it — the wrapped
version put the mod table 2px off the plugin table and `MO2_VERIFY_ROW_PADDING`
failed on it, which is recorded here because the fix is not obvious from the
result. MO2's ridge has no Avalonia equivalent; this is 2px solid in MO2's colours.

`MO2_VERIFY_WIDGET_BEHAVIOUR` gained: the column menu opened where MO2 puts it,
its entries compared against MO2's seven hideable columns, the four MO2 hides
required absent from the table, and Version chosen from the menu and read back off
the drawn columns both ways; the filter ring and the count ring; the green grouping
ring and the count deliberately not ringed by it; and `ModsClearFiltersButton` and
`ModsCurrentCategoryLabel` driven for the first time. The ring is verified by
counting the pixels it changes in the rendered window — 3,296 — not by reading the
brush off the control.

`src/mainwindow.ui` was parsed for every named widget under each tab rather than
compared against a hand-written list. Every widget it reports now has a counterpart
driven by this check, which passes 56 statements. Not yet compared:
`executablesListBox`/`startButton`/`linkButton` against the frontend's launch panel,
and `espFilterEdit`'s `MOBase::LineEditClear` clear affordance.

Live on the FNV host: widget behaviour PASS (56), Qt widgets PASS, and the audit
group PASS eleven for eleven including row padding, header fit, density and
separator colour. All 24 profile files hash identically to the start of the
session, no backup files left behind, and the saved workspace layout is unchanged.
Cold first-display latency and the combined end-to-end workflow remain open.
