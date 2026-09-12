# Existing MO2 extensions

The alternate frontend must preserve existing MO2 extensions with minimal
changes. The current Avalonia fixture host does not load MO2 extensions and must
not be presented as an extension-compatible runtime.

## Boundary to preserve

Keep `PluginContainer`, `OrganizerProxy`, `MOBase` interfaces and the Python
proxy as the extension host. Native plugins continue to load through
`QPluginLoader`; Python plugins continue to receive the existing `mobase` API.
Do not require extension authors to implement a new .NET plugin API or rewrite
plugins around Nexus application models.

The future frontend bridge should expose MO2 operations and state to Avalonia
while extension lifecycle, callbacks, settings, requirements, game features and
file mapping remain owned by the MO2 host. The bridge must marshal requests onto
the host's Qt thread and preserve callback ordering, errors and plugin identity.
The fixtures currently model frontend interactions only; they are not that bridge.

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

Neither track is implemented or runtime-verified by this frontend branch yet.
Choose platform adaptations in shared host/dependency layers before requesting
changes from individual plugin authors. Preserve existing Windows builds.

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
