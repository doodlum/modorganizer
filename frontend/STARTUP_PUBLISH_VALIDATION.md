# Linux ReadyToRun evaluation — 2026-09-20

The framework-dependent linux-x64 ReadyToRun build passed the existing startup
limits in two runs. A regular → ReadyToRun → ReadyToRun → regular comparison used
the same FNV host, copied paired Mods/Plugins layouts and local .NET 9.0.318 SDK.

| Build | Window | Readable rows | Background turn | Result |
| --- | ---: | ---: | ---: | --- |
| Regular 1 | 3830 ms | 7226 ms | 7247 ms | Fail |
| ReadyToRun 1 | 2265 ms | 4638 ms | 4643 ms | Pass |
| ReadyToRun 2 | 2479 ms | 4896 ms | 4901 ms | Pass |
| Regular 2 | 3460 ms | 7124 ms | 7128 ms | Fail |

Limits remain 3200/6000/8000 ms respectively. The final marker measures a queued
background dispatcher turn after readable layout; it does not measure physical
input or compositor latency. Evidence is in
`artifacts/startup-r2r-comparison.json` and its individual logs/screenshots.

The evaluated 245 MB output is `artifacts/r2r-startup-evaluation`. PE headers
confirmed ReadyToRun code in both MockHost.dll and NexusMods.App.UI.dll. Its
publish used `-p:BuildProjectReferences=false` to reuse the same referenced
assemblies as the regular comparison. It is not evidence of a clean build of
all current dependency sources.

`MockHost/Properties/PublishProfiles/LinuxReadyToRun.pubxml` records the publish
settings. A full source publish, without the comparison's reference-reuse flag,
can be run from the repository root:

```sh
DOTNET_PROCESSOR_COUNT=2 .tools/dotnet/dotnet publish frontend/MockHost/MockHost.csproj -p:PublishProfile=LinuxReadyToRun -m:1 -o frontend/artifacts/linux-ready-to-run
```

The full publish has now completed; see the follow-up below. The normal desktop
launcher has not been redirected to either published artifact.

The experimental build also passed sidebar FNV → current Skyrim → FNV switching:
Mods/native filters, 11/150 plugin identities respectively, Data, and Tools
(FNV: 3 programs/4 extensions; Skyrim: 4 programs/7 extensions). The original
FNV profile was restored. Before/after comparisons for both hosts confirmed
unchanged profiles, mod/plugin states and priorities, pins, executable lists and
profile INI hashes. Evidence: `artifacts/game-tools-roundtrip-r2r/frontend.log`
and `state-check.json`. The final 1280×750 Tools/Data screenshot was inspected.

Saves work remains paused at the user's request. These results do not complete
the broader production, native launch or physical input acceptance gates.

## Full dependency-source publish

The publish-profile command above completed successfully, producing a 245 MB
output. The log is `/tmp/mo2-r2r-full-publish.log`; it contains six repeated NuGet
vulnerability warnings for System.Private.Uri 4.3.0, one CS8785 source-generator
failure warning, one CS1690 warning and eight frontend compiler warnings. No
build errors were reported. These warnings remain unresolved.

Startup on this exact output was inconsistent: the first run opened in 2554 ms
but readable rows took 6970 ms (FAIL); the second opened in 2180 ms with readable
rows at 4574 ms (PASS). Both results remain in
`artifacts/startup-r2r-full.json`. The earlier two passing evaluation runs do not
establish reliable startup acceptance for this new output.

The desktop installer now accepts an explicit `--app` DLL path and validates its
runtimeconfig/deps companions before modifying the shortcut. An isolated XDG
installation verified the selected target and shell syntax; a rejected missing
DLL left the generated launcher unchanged. The real shortcut remains unchanged.

The full-source output also passed the same FNV → Skyrim → FNV Mods, Plugins,
Data and Tools check. Both hosts retained profiles, mod/plugin states and
priorities, pins, executable lists and profile INI hashes. Evidence is in
`artifacts/game-tools-roundtrip-r2r-full/frontend.log` and `state-check.json`.

## Dependency and generator fixes — 2026-09-20

Removed the redundant System.Linq 4.3.0 package reference from the net9.0
Loadouts.Synchronizers project. Restored assets no longer contain
System.Private.Uri; the build reports no NU190x audit warnings. System.Linq.Async
is retained. The user's existing Directory.Packages.props edits were untouched.

Weave 2.1.0 is brought in by MnemonicDB.SourceGenerator. Its build targets are
excluded by that dependency, so CompilerGeneratedFilesOutputPath was not exposed
to its analyzer. The generator then passed a missing option to Path.Combine,
even without templates. Exposing that compiler-visible property in upstream
Directory.Build.props fixes CS8785 without suppressing diagnostics or disabling
generators. The installed package targets and the exact package source explain
the required option:
https://github.com/otac0n/Weave/blob/393043ba0890ef5dfdf2f3a50a392a4195307b18/Weave/GenerateWeaveSources.cs

The full Release linux-x64 dependency build completed with zero errors, zero
CS8785 warnings and zero NU190x warnings (`/tmp/mo2-weave-config-build.log`). It
recompiled dependent projects and reported 63 other compiler warnings; this is
not a warning-free build. The earlier ReadyToRun artifacts predate these fixes
and must be republished before deployment. The normal launcher is unchanged.

The resulting build passed FNV → Skyrim → FNV Mods/native filters, Plugins, Data
and Tools checks; all before/after host state checks passed. Evidence:
`artifacts/game-tools-roundtrip-dependencies/frontend.log` and `state-check.json`.

## Updated publish and reproducible startup runner

Published all current sources to `artifacts/linux-ready-to-run-fixed`. The
publish completed with no CS8785 or NU190x warnings. Its deps manifest contains
no System.Private.Uri package and the output has no corresponding DLL.
Other compiler warnings remain (`/tmp/mo2-r2r-fixed-publish.log`).

Three preplanned fresh-process runs produced readable-row times of 7173, 4932
and 4579 ms; the first failed the unchanged 6000 ms limit. Window times were
2629, 2321 and 2179 ms. All results remain in `artifacts/startup-r2r-fixed.json`.
The first-run regression is reproducible immediately after publishing; the
cause is not established. These measurements do not prove cold-cache or
physical-input performance. The installed launcher is unchanged.

Added `tools/check_startup.py` for repeatable artifact checks. It copies the
supplied paired layout, preserves all logs/screenshots, cleans up only its own
frontend processes, refuses existing output directories, and requires a PASS
verdict as well as process exit zero. The old temporary comparison script
returned zero despite a printed FAIL; its preserved individual verdicts remain
authoritative. Parser verification against the actual three-run evidence yields
FAIL/PASS/PASS and rejects empty, crashed and contradictory results.

The new runner was exercised on the exact published output and correctly exited
1 for a 7071 ms readable-row failure, while retaining window timing (2559 ms),
screenshot and results JSON in `artifacts/startup-runner-validation`. This later
slow run also shows the issue is not confined to the first launch after publish.

## Managed startup profiling — 2026-09-20

Installed local dotnet-trace 9.0.661903 in `.tools/diagnostics`. The first capture
of `linux-ready-to-run-fixed` reported readable rows at 8209 ms under profiling,
then exited 134 during screenshot-driven shutdown. The exception is
TaskCanceledException from AvaloniaSynchronizationContext.Send during
Tmds.DBus.Protocol observer disposal. The truncated trace cannot be used for
method-level conclusions; its failed conversion/rundown evidence remains in
`artifacts/startup-profile-r2r`. This shutdown failure is unresolved and has not
been established as a normal interactive-exit failure.

A second capture stopped collection at five seconds, before shutdown, and
completed with exit zero. Its valid trace and 4394-frame Speedscope conversion
are in `artifacts/startup-profile-timed`. A main-thread stack summary records
about 741 ms under MediaContext.SyncWaitCompositorBatch, versus about 20 ms
under database bootstrap waits. Other sampled managed work is distributed across
reactive publication, property dictionaries, reflection, style attachment and
native interop. This suggests investigating rendering synchronization alongside
control construction; it does not establish a single cause of the total delay.

These are sampled thread-time estimates, including waiting, not CPU-time
measurements. Inclusive frame times overlap. The nearest-managed-frame report
skips synthetic UNMANAGED_CODE_TIME markers; it cannot attribute native execution
below those managed frames. The trace covers only the first five seconds of an
older precompiled artifact and predates the divider/reflow/launcher changes.
It is diagnostic evidence, not a new performance acceptance result.

Tool reference: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace

## Shutdown investigation — 2026-09-20

Source review of the installed Tmds.DBus.Protocol 0.21.3 commit
8cb04f66c330b64244e996ff68f685f27f7381a0 confirms that Observer disposal emits
connection errors through SynchronizationContext.Send. This matches the failed
profile trace's stack but does not identify which observer or connection caused
that disconnect. Avalonia 11.3.5 has several desktop/input-method observers;
its platform-settings watcher is one candidate, not a proven origin. No observers,
portal features or input methods were disabled, and no exception suppression was
added.

Three ordinary launches of the current Release build used independent copied
layouts and no screenshot/profiling flags. After twelve seconds, xdotool
windowquit sent the window manager's _NET_CLOSE_WINDOW request to the sole visible
window owned by that exact test PID. All three processes exited 0 without an
unhandled exception. Evidence: `artifacts/wm-delete-close-check/results.json` and
individual logs. This narrows current observations; it does not prove the race
cannot occur during normal use.

The initial test mistakenly used xdotool windowclose, which calls XDestroyWindow
rather than requesting normal app closure. That attempt timed out and its owned
frontend was terminated. It is excluded from exit acceptance; evidence remains
in `artifacts/interactive-close-check`. xdotool windowkill was never used.

Source references:
- https://github.com/tmds/Tmds.DBus/blob/8cb04f66c330b64244e996ff68f685f27f7381a0/src/Tmds.DBus.Protocol/DBusConnection.cs
- https://github.com/AvaloniaUI/Avalonia/blob/11.3.5/src/Avalonia.FreeDesktop/DBusPlatformSettings.cs
- https://github.com/jordansissel/xdotool/blob/master/xdo.c
