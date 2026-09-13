# Existing MO2 extensions

The alternate frontend must preserve existing MO2 extensions with minimal
changes. The live Avalonia frontend connects to the original Windows MO2 2.5.2 host
under Proton. That host loads extensions; Avalonia does not load PE binaries or
replace MOBase. The separate fixture mode is only frontend test scaffolding.

## Boundary to preserve

Keep `PluginContainer`, `OrganizerProxy`, `MOBase` interfaces and the Python
proxy as the extension host. Native plugins continue to load through
`QPluginLoader`; Python plugins continue to receive the existing `mobase` API.
Do not require extension authors to implement a new .NET plugin API or rewrite
plugins around Nexus application models.

The bridge is an ordinary Python tool extension. Its QTimer handles requests
on the original Qt thread after onUserInterfaceInitialized. MO2 retains extension
lifecycle, callbacks, settings, requirements, game features and file mapping.
No extension API was changed. This architecture preserves the host boundary;
runtime evidence for specific extension behavior is listed below.

Extensions that create Qt widgets need the Qt UI runtime too. Preserve their
existing dialogs and tool windows in a companion host, including focus and
ownership behaviour. Do not silently drop preview widgets, tool actions or
installer UI because they cannot be represented as simple serialized data.

## Linux constraints found in this checkout

- `src/plugincontainer.cpp` loads native Qt plugins and registers proxied Python
  interfaces, including the single-init and multi-interface identity rules.
- `src/organizerproxy.h` exposes Windows `HANDLE` values for process launch/wait.
- `src/usvfsconnector.h` exposes USVFS process handles.
- `src/CMakeLists.txt` requires USVFS and links Shlwapi, Bcrypt, Version and Dbghelp.
  It also requires external MO2 dependencies, including uibase and archive tools.

An existing Windows PE plugin binary cannot be loaded into a native Linux Qt
process. Two runtime tracks therefore need separate validation:

1. A Windows MO2 companion under Wine, retaining Windows plugin binaries and the
   existing Python/Qt distribution, with a Linux frontend communicating with it.
2. A native Linux MO2 companion, requiring ports/builds of uibase, the Python
   proxy, dependencies, process launching and virtual-filesystem integration.
   Native extensions need Linux builds. Python source should stay unchanged
   where its dependencies and API use are portable; Windows-specific behaviour
   needs targeted adaptations.

The Proton track is implemented and has runtime evidence below. The native
Linux backend track is not implemented and is not a prerequisite: the user
accepted Proton. Native-port adaptations should remain in shared host/dependency
layers before requesting changes from individual plugin authors.

## Inventory and verification

Run the read-only inventory without importing or executing extensions:

```sh
python3 frontend/tools/audit_plugins.py /path/to/MO2/plugins --output /tmp/plugins.json
```

The installed tree examined in this session contains 269 Python source files and
41 PE binaries outside bundled `libs` directories. Six Python files contain
explicit Windows import/call indicators. These are file counts, not counts of
independent extensions or compatibility results. Dynamic imports, bundled
libraries, filesystem assumptions and indirect Windows dependencies require
further checks. Machine-specific inventory output belongs in ignored artifacts.

Runtime acceptance must exercise actual unmodified examples of game, tool,
installer, preview, diagnose, file-mapper and proxy extensions; extension
settings and persistence; lifecycle and enable/requirement rules; process
launch/wait callbacks; Qt widget ownership; and reload/shutdown. Compare behaviour
with the original MO2 host. Static inventory or successful frontend rendering is
not evidence that extensions work.


## Current runtime evidence

- The original FNV game and installer extensions discover the game, install MCM
  and its author examples, and supply the live mod/plugin models. The game runs
  through MO2/USVFS with xNVSE and MCM; disabling MCM in the frontend removes it
  on the next launch. See [FNV acceptance](FNV_ACCEPTANCE.md).
- The registered Skyrim host supplies its original game/plugin model, conflicts
  and executable list. Switching FNV → Skyrim → FNV preserves each profile and
  workspace. Skyrim gameplay has not been tested.
- The original Python proxy loads the bridge and an ordinary IPluginDiagnose
  test extension. MO2's Notifications action refreshes the diagnostic extension;
  NMA Health Check follows new, resolved and recurring reports, including across
  a frontend restart. See [fidelity evidence](NMA_FIDELITY.md).
- Original MO2 mod details, profile manager and Nexus settings dialogs have
  opened and returned through the frontend. Explicit original-window switching
  also shows the same FNV profile, enabled mods and fourteen plugins, then hides
  it without changing their state. Background host UI remains suppressed.

This establishes specific integrations, not universal third-party plugin
compatibility. The inventory above is still only an inventory. Broader archive-preview,
file-mapper, plugin settings/lifecycle and requirement-rule acceptance remains
unverified until exercised against original extensions.


### Original INI Editor: hidden host versus normal launch

The unchanged installed `inieditor.dll` was exercised through MO2's registered
INI Editor QAction, after native tool-menu initialization. A temporary ordinary
Python tool extension supplied the verifier; it did not replace INI Editor or
read/edit/save editor contents.

Both runs opened `MOBase::TextViewer` visibly, with five text editors and one tab
widget. In frontend-host mode, the main window retained WA_DontShowOnScreen and
X11 reported no visible original main window. A normal launch without
MO2_FRONTEND_HOST showed the main UI and the same native editor dialog structure.
The verifier dismissed the editor with reject(), without saving changes.

The fixture source is [tools/fixtures/tool_dialog.py](tools/fixtures/tool_dialog.py).
It only initializes in the isolated `mo2-fnv-host` instance. With that host stopped,
copy it to `plugins/frontend_tool_dialog.py`, then start the host in each mode
separately. It opens INI Editor two seconds after UI initialization, reports
structure/visibility to `frontend-tool-dialog-result.json` and dismisses the
editor after ten seconds. Preserve each report before the next run. Stop MO2 and
remove the installed fixture afterward. These operations deliberately open a
native tool dialog and should only run in the isolated verification instance.

Recorded reports: `artifacts/tool-dialog-hidden.json` and
`artifacts/tool-dialog-normal.json`. They agree on dialog class and editor counts;
only the main-window suppression flag differs. Profile-file hashes were checked
before and after both runs. This verifies the original tool's Qt dialog path and
normal-versus-hidden ownership behavior, not tool editing, persistence or every
third-party tool extension.


### Original Python DDS preview

The live frontend's mod-details action opened the native MO2 details dialog for
MCM. A temporary ordinary tool extension selected `textures/MCM/Check1.dds` in
its native Filetree and triggered MO2's existing Preview action. The unchanged
installed `DDSPreview.py` supplied the resulting widget through the original
Python proxy and IPluginPreview API. No replacement image decoder or frontend
preview implementation was used.

The native PreviewDialog was visible, contained one valid OpenGL `DDSWidget`,
and rendered the checkbox texture with transparency. The main MO2 window
retained WA_DontShowOnScreen. The preview and details dialogs returned normally,
and every mod/profile file retained its original hash. The screenshot was
inspected: [original DDS preview](artifacts/native-dds-preview.png).
Structured result: `artifacts/native-dds-preview-result.json`.
Original plugin SHA256:
`a5fc7f2959b2ec55ded0ab8164c2fd16531dbdc394c33c0adb0a4b7de169e253`.

The Images-tab DDS checkbox is unavailable in this host because it depends on
Qt's QImageReader formats. That does not determine DDS Preview Plugin support;
the native Filetree Preview action calls MO2's extension preview generator.

To reproduce, stop the isolated FNV host and install
[tools/fixtures/preview_dialog.py](tools/fixtures/preview_dialog.py) as
`plugins/frontend_preview_check.py`. Remove any old
`frontend-preview-result.json` from that host, then run the frontend with
`MO2_VERIFY_CATALOG=1 MO2_VERIFY_PREVIEW=1 MO2_SCREENSHOT=...`.
The fixture opens the known MCM texture only when the mod-details dialog opens,
restores the previous details tab, and closes both dialogs without edits.
After verification, stop MO2 and remove the installed fixture and result file.
The tracked fixture source remains; the installed copy was removed after this
successful check. Build passed with the existing NU1902 warning. The frontend
verifier exited successfully (`/tmp/mo2-preview.log`). This checks a loose DDS
preview, not archive preview or every supported file type.


### Original extension settings across host restarts

The unchanged DDS Preview plugin's native colour-channel selector now has a
three-host persistence check. In the first host, the verifier selected RGB in
place of the original RGBA. The original selector's callback wrote the value
through `IOrganizer.setPluginSetting`. The second fresh host opened the same
preview with RGB selected and confirmed the value through `pluginSetting`, then
restored RGBA using the same native selector. A third fresh host confirmed both
the restored setting and the restored selector value.

All three native previews remained visible with valid DDS OpenGL widgets while
the main window stayed suppressed. Each frontend exited successfully, and the
host was closed normally and confirmed stopped before the next launch. This
uses MO2's plugin settings, without reading or editing ModOrganizer.ini. The only
setting read was DDS Preview Plugin / channels. The extension source was not
modified. The original value remains restored after the final shutdown.

To enable the optional settings phases in the existing preview verifier, create
`artifacts/preview-settings-state.json` containing `{"phase":"write"}` before
the first host launch. Use the preview-check command above three times, with a
fresh host and removal of its previous `frontend-preview-result.json` between
runs. The marker advances through read, verify and done, retaining the original
value for restoration. Preserve the reports and remove the phase marker and
installed verifier when done. Each phase requires a new plugin initialization.
The normal preview check does not change channel settings when this marker is
absent.

Evidence: `artifacts/preview-settings-{write,read,verify}-result.json`,
`artifacts/preview-settings-result.json` and the three
`/tmp/mo2-preview-settings-{write,read,verify}.log` files. All mod/profile file
hashes remained unchanged across the full check. The temporary verifier was
removed after the final host exit. This verifies the original DDS setting's UI,
MO2 persistence and reload, not every third-party extension setting.


### Original file mapping and a completion-callback gap

A read-only Windows Python probe launched through the original organizer API in
the isolated FNV host loaded USVFS and read five game INI paths from the game's
Documents directory. Every hash matched its corresponding profile-local INI.
The unhooked files differed (two different files and three absent files), so this
is evidence of effective mapping rather than equal copies on disk. The probe
completed with exit code zero; all mod/profile hashes remained unchanged.

This matches [INI Bakery's mapping implementation](https://github.com/ModOrganizer2/modorganizer-tool_inibakery/blob/de678f72ea8bf13c72456644d81da5b93c82274d/src/inibakery.cpp),
which maps the managed game's INIs from the selected profile when local settings
are enabled. MO2's ordinary data-directory mod mappings do not account for these
Documents-directory paths. The original installed INI Bakery DLL was not modified;
its SHA256 is `41627799e639e5a19312a90cce5f75991be4d000cb41bea3aacb94854b5d3cc8`.
The original Gamebryo file mapper maps plugin/load-order lists separately.

The same probe exposed a distinct compatibility gap. onAboutToRun received the
probe binary, and onFinishedRun ran, but its binary argument was empty. In the
MO2 2.5.2 source, OrganizerProxy.startApplication starts one ProcessRunner and
returns its handle; waitForApplication attaches a new ProcessRunner to that
handle without its original spawn metadata. That runner calls afterRun with an
empty binary. The frontend's current Executables.launch uses this same pair.
Extensions that identify the completed executable can therefore behave differently
than under MO2's original Run button. That original slot keeps one configured
runner through execution and completion. Correcting the frontend launch path and
verifying callback identity is outstanding; a successful mapping result does not
close this separate issue.

The temporary verifier is [file_mapper.py](tools/fixtures/file_mapper.py), with
[mapped_ini_probe.py](tools/fixtures/mapped_ini_probe.py) as the child process.
With the isolated host stopped, install the verifier as
`plugins/frontend_file_mapper_check.py` and the child script as
`artifacts/mapped_ini_probe.py`. Remove old `mapped-ini-probe-result.json`,
`mapped-ini-request.json` and `native-file-mapper-result.json` before a new run.
The verifier launches after UI initialization and requires Frontend Test with
local settings. It reads only the reported INI paths and records hashes, injection
state and callback flags. It does not alter INIs or start a game. Stop the host
and remove the installed fixture and request/child files afterward.

Result: `artifacts/native-file-mapper-result.json` (mapping passed; completion
binary empty). The temporary fixture was removed after the host exited. This
checks native INI mapping, not every mapper or the plugin-disabled contrast case.
