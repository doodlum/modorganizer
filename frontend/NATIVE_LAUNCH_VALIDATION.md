# Native launch regression

The ProcessRunner source change keeps unhooked executable launches with arguments
on `runBinary()` instead of passing only the executable filename to `shell::Open`.
This preserves the command line produced for batch and Java files. The normal
CreateProcess branch respects `hooked=false`; no-argument shell launches retain
their existing route. Unhooked `runBinary()` calls now go directly to the existing
spawn helper. They bypass profile lookup, VFS preparation, hooked-launch plugin
callbacks and virtualized path rewriting. Otherwise an ordinary batch launch
inside a mod could have its physical working directory rewritten into game Data.
Hooked launches retain their existing preparation, checks and cancellation path.

**The native change is not yet compiled or deployed.** Building MockHost does not
build ModOrganizer.exe. This repository's native build uses Windows, Visual
Studio 2022 and the MO2 dependencies, as defined by `CMakePresets.json` and
`.github/workflows/build.yml`. No Windows build of this change has run in this
Linux session. Validate a rebuilt native bundle in an isolated host before
replacing the user's host binaries.

## Repeatable runtime check

The probe creates a unique folder in the selected host's Overwrite directory,
with a command file and a text file. Its path includes spaces. The command checks
its working directory and whether the text file is visible through game Data.
It writes only its new temporary result file. It does not enable/disable mods or
plugins, change load order, or copy files into physical game Data.

Use a running isolated host with the current bridge, a populated mod list, and
at least one Nexus-associated mod (used to reach MO2's All Mods → Refresh menu).
Choose a new specification path for each run:

```sh
bridge=/path/to/mo2/plugins/data/frontend-bridge
spec=/tmp/mo2-launch-probe.json
python3 frontend/tools/data_launch_probe.py prepare \
  --bridge "$bridge" --spec "$spec" --game-data '/path/to/game/Data'

MO2_BRIDGE_DIRECTORY="$bridge" \
MO2_FRONTEND_LAYOUT=/tmp/mo2-launch-probe-layout.json \
MO2_VERIFY_DATA_ACTIVATION="$spec" \
.tools/dotnet/dotnet frontend/MockHost/bin/Release/net9.0/MockHost.dll
```

The default check calls the same activation method as Data's double-click handler,
then invokes its actual Execute with VFS menu entry. It requires ordinary
activation to write `plain`, followed by VFS execution writing `hooked`, while
the physical game Data path stays absent. It does not synthesize a mouse gesture
or establish Java runtime behaviour. Close the test frontend after the verdict.

A failed launch may still have a live native process; inspect it before rerunning
or cleaning up. The installed pre-fix host starts an idle cmd.exe during the
ordinary-activation phase. That failed run is documented in CURRENT_STATUS.md.
`MO2_VERIFY_VFS_ONLY=1` exercises the independent VFS phase and explicitly reports
ordinary activation unverified; it is not an acceptance substitute for the default
check after rebuilding the native host.

Once the test process has finished:

```sh
python3 frontend/tools/data_launch_probe.py cleanup --bridge "$bridge" --spec "$spec"
```

Cleanup checks the owning profile, Overwrite path and original file hashes before
removing probe files. It refreshes MO2 and confirms the folder disappeared from
native Data. Retain the specification and test log as evidence.

## Evidence so far

- The installed native host failed ordinary batch activation: it dropped the
  prepared arguments and started only cmd.exe.
- VFS execution succeeded, including the strengthened path-with-spaces and working-
  directory probe (`artifacts/data-activation-spaces-cwd.log`).
- The strengthened probe's cleanup succeeded; physical game Data was untouched.
- The source fix still requires a Windows build and the complete default runtime
  check. Java activation and the remaining production acceptance are also open.

## Focused routing regression (not native runtime acceptance)

`tools/check_native_launch_routing.py` compiles the actual current `run()`,
`shouldRunShell()` and `runBinary()` methods with simulated dependencies. It checks preservation of arguments and
physical paths, absence of profile/VFS work for unhooked execution, process-handle
and failure propagation, and hooked preparation order/cancellation. It does not
exercise Windows process creation, Qt or plugin binaries. Shell dispatch is
covered, but `runShell()` and file association lookup are simulated dependencies.

```sh
python3 frontend/tools/check_native_launch_routing.py
```

On this Steam Deck the available compiler is in the installed SDK:

```sh
python3 frontend/tools/check_native_launch_routing.py --cxx \
  'flatpak run --user --command=g++ --filesystem=/home/deck/mo2 org.freedesktop.Sdk//24.08'
```

The prior `runBinary()` body fails the unhooked case; the updated body passes
(`artifacts/native-launch-routing-before.log` and `native-launch-routing-after.log`).
These results do not establish that the pending native change builds on Windows.
Before deployment, also repeat ordinary and VFS activation from an isolated mod
directory, not only Overwrite, to cover physical versus virtualized working paths.

The expanded dispatch check passes on the current source and fails on the original
HEAD source because the argument-bearing launch takes the path-only shell route
(`artifacts/native-launch-dispatch-current.log` and
`native-launch-dispatch-before.log`). It also checks unchanged no-argument shell
routing, direct error propagation without postRun, hooked file-association command
lines and fallback when association lookup fails. This closes source-level
dispatch coverage; the native Windows build and runtime checks above remain open.
