# Frontend handover — 2026-09-20

## Repositories and scope

- Parent: `doodlum/modorganizer`, branch `feat/mo2-faithful-tabs`.
- NMA: `doodlum/NexusMods.App`, branch `feat/nexus-alternate-frontend`, pinned
  at `00f42d892698732291547c95ba78e8089ee7c80c` by `frontend/upstream`.
- Both forks already exist. The submodule URL now points to the user's fork.
- Current implementation uses Avalonia and MO2's original Qt host under Proton.
  A requirement for a Qt frontend itself remains unresolved. Saves is deferred;
  previously implemented Saves source/checks are preserved, not newly accepted.
- This is a development checkpoint, **not a production-complete release**.

## Build and run

From a fresh clone of the parent feature branch, with the .NET 9 SDK installed:

```sh
git submodule update --init frontend/upstream
dotnet build frontend/MockHost/MockHost.csproj -c Release -m:1
./frontend/run.sh
```

Existing clones should run `git submodule sync -- frontend/upstream` before
updating. See [README.md](README.md) for publishing and desktop installation and
[mo2-plugin/README.md](mo2-plugin/README.md) for native bridge setup. Games,
profiles and authenticated native MO2 hosts must be configured locally.

On the current Deck, the desktop shortcut points to
`frontend/artifacts/linux-review-20260920-lazy-filters/MockHost.dll` and uses
`.tools/dotnet/dotnet`. Publishing/building source does not replace that package.
Its manifest records the earlier base commits plus dirty source; this checkpoint
commits that work. Artifacts, SDKs, credentials and host state are intentionally
excluded from Git. Preserve local evidence and launcher backups.

## Changes and validation

Shared rows, headers, toolbar search, responsive columns and aligned help/scroll
rails cover Mods and Plugins and the utility pages. Filters are populated lazily,
retain active criteria across refresh, and refresh correctly when switching games.
Panel dragging retains page identity and relocates history navigation. Data,
tools, notifications and other asynchronous pages guard against stale results.
Bridge additions support native filtering and Data actions; accompanying checks
and validation documents are included.

Pre-push Release build including project references passed: 0 errors, 1 existing
CS1690 warning. Earlier published-package evidence includes:

- `lazy-filters-published-ui`: responsive layouts, aligned rails/help, category
  controls and narrow search checks passed.
- `lazy-filters-published-roundtrip`: FNV → Skyrim → FNV, native filter counts,
  mods/plugins, Data and Tools passed; both hosts' compared native state unchanged.
- `isolated-keyboard-20260920`: ten real XTest keyboard/search phases passed on a
  fresh authenticated X11 display. The actual desktop's stuck modifier condition
  was not fixed; frontend D-Bus isolation was not established.
- Earlier panel package: physical split/join and command-driven Back/Forward
  retained page identity. Earlier mutation checks restored native profile state.
- Historical FNV gameplay/MCM acceptance is in [FNV_ACCEPTANCE.md](FNV_ACCEPTANCE.md).
  It is not a fresh gameplay run of the latest installed package.

Current candidate readable-list times were 4665/4727 ms versus paired baseline
7135/4708 ms. This does not establish consistent cold startup or improved latency.
Do not discard failed attempts or relabel earlier evidence as current acceptance.

## Next work

1. Build `src/processrunner.cpp` on Windows and validate the resulting binary in
   an isolated Proton host before replacing installed native hosts. The patch
   preserves arguments for unhooked launch and bypasses VFS preparation for that
   route. The extracted C++ harness passed; Windows compilation/runtime remain
   unverified. Follow [NATIVE_LAUNCH_VALIDATION.md](NATIVE_LAUNCH_VALIDATION.md).
   The existing CI triggers on master pushes and PRs, not these feature-branch
   pushes alone; pushing this checkpoint is not evidence of a Windows CI pass.
2. Resolve whether the frontend itself must use Qt before further substantial
   Avalonia redesign. Continue to preserve original MO2 extension APIs and state.
3. Finish scoped visual/interaction acceptance against the supplied redesign and
   investigate measured startup/input issues. Leave Saves deferred.

[PRODUCTION_READINESS.md](PRODUCTION_READINESS.md) is the requirement/evidence
matrix. [CURRENT_STATUS.md](CURRENT_STATUS.md) is chronological history; later
checkpoints supersede older claims. Never commit account tokens, raw snapshots,
native profile backups, or authentication cookies. Do not globally terminate
Wine or replace the user's running hosts to clear a test timeout.

## Publishing provenance

GitHub rejected private-email commits. Unpublished feature-branch history was
recreated with the account's verified GitHub no-reply address, preserving commit
trees, messages and timestamps. Historical artifact base hashes can therefore
refer to the old local history, retained under `refs/backup/pre-handover-email-fix`.
No existing remote feature branch was overwritten.
