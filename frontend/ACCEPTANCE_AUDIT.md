# Integration acceptance audit — 2026-09-13

Follow-up navigation and Tools changes are documented in [NMA_FIDELITY.md](NMA_FIDELITY.md). They replace the earlier per-profile game icons and Rules tab with one icon per game, Profiles, Plugins under Installed, and Tools under Utilities. Downloads now uses NMA's native DownloadsPageView. The new live checks cover mod priority restoration, filters, original tools, downloads, and FNV/Skyrim switching. Earlier evidence below remains historical evidence for unchanged backend functionality.


The requested local integration is verified: an MO2-owned frontend with NMA
navigation/panels, usable interchangeably with original MO2 for the registered
FNV and Skyrim instances. The requirement evidence below includes functional
checks, original extension behavior and the final fork comparison. It does not
claim universal third-party extension compatibility or a native Linux MO2 port.

## Requirement evidence

| Requirement | Current implementation and evidence | Audit result |
| --- | --- | --- |
| Use the FNV-enabled NMA fork as the reference | `reference-fnv` is commit `3b0244e7d21ea19afabcc3d81539c9e11ae8d642`; its FNV comparison screenshots remain in `artifacts/reference`. The native panel view/code, divider view/code and workspace model were compared byte-for-byte with the pinned UI sources and are identical. | Reference identity and panel source reuse verified. |
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
