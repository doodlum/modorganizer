# Integration acceptance audit — 2026-09-13

The objective remains an MO2-owned frontend with NMA navigation/panels, usable
interchangeably with original MO2. This audit separates implemented workflows
with runtime evidence from gaps that still prevent a full completion claim.
It does not claim universal third-party extension compatibility.

## Requirement evidence

| Requirement | Current implementation and evidence | Audit result |
| --- | --- | --- |
| Use the FNV-enabled NMA fork as the reference | `reference-fnv` is commit `3b0244e7d21ea19afabcc3d81539c9e11ae8d642`; its FNV comparison screenshots remain in `artifacts/reference`. The native panel view/code, divider view/code and workspace model were compared byte-for-byte with the pinned UI sources and are identical. | Reference identity and panel source reuse verified. |
| My Games/My Loadouts represent MO2 instances/profiles | `Mo2InstanceCatalog`, `Mo2HomePages` and `Mo2ProfileFiles` read actual MO2 instances. Card operations call native profile management. `check_profile_reader.py` passed again with multiple instances, overridden paths, duplicate names and missing/unmanaged entries. Native card/create screenshots and runtime verifiers remain available. | Implemented; original profile actions and cross-game navigation have runtime evidence. |
| Mods left, plugins right, using one MO2 profile | `Mo2LiveWorkspace.CreateProfileWorkspace` creates the two native panels; `Mo2LiveProfile` applies host snapshots to both adapters. Native mod activation, plugin activation/order, pointer dragging and priority changes have passed. `external-profile.png` shows the current FNV arrangement with 12 mod rows and 14 plugins. | Implemented and exercised with FNV and Skyrim. |
| Original MO2 and the frontend share state | Independent native profile-selector, mod-priority and plugin-state requests are detected by ordinary polling. Workspace, spine, caption and both lists follow MO2; both profiles restore unchanged. Explicit original-UI show/hide reveals the same host and profile. | Verified by the external-profile and original-UI checks. |
| MO2 owns archives, transfers and installation | `Downloads` reads the MO2 directory/metadata and calls its downloader, transfer slots and `installMod`. Original MCM downloads/FOMOD installation and transfer-control evidence remain available. Current download-context checks reject stale picker/row targets and hide stale rows on unavailable snapshots. The three download contract checks pass. | Implemented; runtime evidence covers install, transfers and profile boundaries. |
| No independent NMA Library or deployment authority | Live pages use MO2 adapters. Library-style membership is the MO2 downloads folder. Collection/Apply controls are hidden or disabled; the native UI's database service is in-memory presentation infrastructure. Persistent frontend files contain registrations and workspace presentation only. | Source review supports the ownership boundary. |
| Sidebar, game icons and panel behavior match the reference | Live Home and profile sidebars use native views; Home items leave the game workspace. Spine/loadout art uses square game icons and My Games uses covers. Native divider dragging, tab/panel actions, history and per-profile layout restoration have runtime evidence. | Main requested paths verified; final page-state comparison remains below. |
| Health Check reports MO2 diagnostics | Native Notifications refreshes enabled diagnostic extensions; list/details show the host's reports. Unavailable, zero-report, populated, resolved/recurring, cross-game and restored-detail states have passed. `health-restart-read.png` remains available. | Implemented and exercised, including a real original-API diagnostic extension. |
| Preserve original extensions with minimal changes | The original MO2/Qt/Python host remains intact under Proton. Existing game, installer, tool and DDS-preview extensions work; original DDS settings survive restart and restore through MO2. `preview-settings-result.json` is `done`, with original RGBA restored. | INI mapping now has effective hooked-process evidence; native Run callback identity and Unlock now pass; enable/requirement-rule coverage remains incomplete. |
| Keep unsolicited MO2 UI hidden | Hidden-host startup suppresses main/splash/toast windows. Explicit original UI and native tool/preview dialogs remain usable. Hidden-versus-normal INI Editor reports agree on dialog structure. | Implemented with runtime evidence; temporary verifier plugins are removed after checks. |
| Successfully load FNV with mods | `FNV_ACCEPTANCE.md` records actual gameplay, MCM menus, enabled/disabled launches and all 14 in-game plugin indices. Current-sidebar PLAY evidence includes loaded gameplay, movement, pause menu and normal return to PLAY. The corresponding artifacts remain present. | Gameplay acceptance has passed; the updated original Run path also loaded the save with xNVSE/MCM, accepted movement and exited normally with code 0. |
| Do not lose actions during polling or cross profile boundaries | Thirteen controlled transport cases cover dialogs, PLAY and profile-card requests, duplicate suppression, stale/unavailable targets and missing card profiles. Real-host validation and native preview checks also passed. | Current command changes verified at transport and native integration boundaries. |

Evidence details and test limitations are in [MO2 integration](MO2_INTEGRATION.md),
[NMA fidelity](NMA_FIDELITY.md), [extension compatibility](EXTENSION_COMPATIBILITY.md)
and [FNV acceptance](FNV_ACCEPTANCE.md). The historical sections of those files
record intermediate states; their earlier “not implemented” statements are not
the current status of workflows subsequently verified there.

## Remaining acceptance work

1. **Original extension enable/master/requirement rules.** The original container
   retains these rules, but their runtime behavior with the alternate frontend
   needs a bounded original-extension check. Successful loading of the bridge
   and DDS preview alone does not establish these cases.
2. **Final page-state comparison.** Revisit the actual FNV fork and current live
   frontend for the remaining page states, recording intentional MO2 mappings
   separately from unexplained visual or behavioral differences. Main sidebar,
   mods, health and panel paths already have detailed comparison evidence; this
   is not a request to reimplement NMA Library/deployment semantics.

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
restored the profile INIs; see the extension compatibility and FNV records. Overall integration
acceptance remains open for the concrete gaps above.
