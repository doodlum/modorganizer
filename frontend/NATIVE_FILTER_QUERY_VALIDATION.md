# Native filter query — 2026-09-20

`readModFilterMatches` reads positive matching mod identities for explicit native
criterion `(type, id)` pairs. MO2's own filter controls evaluate special, content
and category predicates. The frontend can compose the returned membership sets
with its include/invert And/Or controls.

The current [public mod interface](https://github.com/ModOrganizer2/modorganizer-uibase/blob/master/include/uibase/imodinterface.h)
exposes metadata rather than the complete ModListSortProxy predicates. This query
uses the native filtering path instead of interpreting translated status labels.

## State and request handling

The bridge checks profile ownership and host availability, rejects missing,
duplicate or malformed criteria, and limits one request to 128 criteria. It
resolves internal mod names through the complete native proxy chain. The query
neutralizes search and separator options while evaluating each positive criterion.
It suppresses painting and selection signals during the synchronous operation.

Both success and failure restore criterion states, And/Or and separator modes,
text/cursor/selection direction, current filter item, selected/current mod rows,
and scroll offsets. Restoration is checked before returning. No global Qt event
loop pump is used; the native filter is updated through its own Space-event path.

`Mo2LiveProfile.ReadModFilterMatches` serializes the request with other native
commands, checks the captured target before and after awaiting the response, and
requires complete, unique returned criterion identities and a restoration result.
The visible filter tree now uses these sets for category, status and content
criteria. Selection is keyed by `(type, id)`, so renaming and duplicate labels do
not redirect a selected criterion. Include/invert and And/Or compose native sets.

Each Mods view coalesces reads and caches them by profile target and filter
revision (native mod metadata plus criterion structure). An unchanged snapshot or
resize reuses the result. Reads run only for a visible filter pane or an active
filter selection. Late results from hidden/detached views or obsolete targets are
discarded. Requests exceeding 128 criteria are batched. Unavailable counts show
an ellipsis with disabled controls; failures expose a Retry action without looping.

The live test found that separators incorrectly bypassed active criteria in
Filter mode. They now use the same predicate as ordinary mods. Following
`ModListSortProxy::filterMatchesMod`, Show/Hide overrides only apply when a filter
is active. Grouping behavior remains separate.

## Evidence

- `artifacts/native-filter-query-first.json`: all 24 special/content criteria
  evaluated on the real FNV host; original profile, mod/plugin state and priority
  matched the pre-restart baseline.
- `artifacts/native-filter-query-controlled.json`: non-default inverted criterion,
  Or mode, separator mode, backward text selection, two selected mod rows and
  current/scroll state restored after success and an injected mapping failure.
  Missing, duplicated and malformed criteria left the seeded state unchanged.
- The native fixture restored its own pre-test state. Its code is retained at
  `tools/fixtures/filter_query.py`, restricted to the isolated `mo2-fnv-host`.
  The temporary installed copy was removed and the host restarted afterward.
  For a repeat run, move aside the prior report, install that fixture in the
  isolated host's plugins directory, restart and require its new passing report;
  then remove the installed fixture and restart again.
- `artifacts/native-filter-clean.log`: the frontend's typed reader obtained all
  24 criteria, checked exact identities/known mod names, and rejected a stale
  profile target on the clean host.
- `artifacts/native-filter-query-final.json`: original 14 mod and 11 plugin names,
  activation states and priorities remained unchanged. The temporary plugin was
  absent; the installed FNV bridge matched source. The clean native query took
  145 ms for all 24 criteria and 12 ms for one criterion. These are native execution
  durations for this small instance, not end-to-end input or large-library latency.

## Visible frontend evidence

- `artifacts/visible-native-filters-accepted.log`: live FNV panel exposes all 24
  status/content criteria with native counts. Actual row identities match include,
  invert, mixed And/Or, separator Filter/Show/Hide, no-filter separator behavior,
  summary/Clear and unchanged-snapshot/resize cache reuse. Run with
  `MO2_VERIFY_VISIBLE_NATIVE_FILTERS=1` against the isolated FNV host.
- `artifacts/native-filter-cache-ready.log`: controlled coalescing, revision and
  target changes, hide/show, stale completion/failure rejection, explicit retry
  and 260 criteria batched as 128/128/4. Flag: `MO2_VERIFY_NATIVE_FILTER_CACHE=1`.
- `artifacts/category-tree-native-visible-ready.log`: controlled rename retention,
  equal IDs across types, duplicate labels, loading counts, nesting and three-state
  input. Existing category component flag: `MO2_VERIFY_CATEGORY_TREE=1`.
- `artifacts/native-filter-visible-restoration.json`: original profile, 14 mod and
  11 plugin identities/states/priorities unchanged; temporary fixture absent and
  installed FNV bridge matches source. No native test mods were created this pass.

Final Release build: eight warnings, zero errors. Earlier native-query validation
also passed forty bridge contracts and Python compilation. These UI checks drive
Avalonia controls over the real native bridge, not physical Wayland input.
`native-filter-visible.png` shows the filter pane, but its offscreen bitmap omits
table text and is not sufficient for full-page visual acceptance. A later desktop
capture confirmed an actual plugin rendering defect; it is corrected and covered
by [visible plugin-row validation](PLUGIN_RENDER_VALIDATION.md). Source identity
checks alone did not prove readable rows.

The two-game check below now covers Skyrim deployment. Broader performance and
full production acceptance remain open. The query is not embedded in recurring snapshots, but relevant
snapshot changes trigger it when filters are in use.

## Stable presentation during refresh — 2026-09-20

`For()` exposes only current, successfully accepted results. `ForPresentation()`
can retain the last accepted map for the same profile and the same criterion
identities while a new revision is loading. This prevents an active filter from
temporarily emptying the mod list. Counts and checkbox identities/selection stay
in place; controls are disabled and the status says “Updating MO2 filters…”. A
failed refresh retains the previous presentation with an explicit out-of-date
message and Retry. A profile or criterion identity change clears that fallback.

- `artifacts/native-filter-cache-continuity.log`: pending refresh, failure/retry,
  replacement and incompatible-target/criterion invalidation, along with the
  existing coalescing, detach and batching checks.
- `artifacts/category-tree-continuity.log`: retained row identity, selection and
  count while disabled for refresh, followed by the new count and enabled controls.
- `artifacts/visible-native-filters-continuity-native-oracle.log`: the live FNV
  panel enables and disables a disposable plugin-free mod. Layout observations
  retain the ordinary rows through the pending interval, controls are guarded,
  and final row sets match fresh native queries after each mutation. MO2 changes
  a separator's active predicate when a child changes; the test compares that
  result to MO2 rather than assuming separator membership stays constant.
- `artifacts/filter-refresh-live-probe.restoration.json`: the fixture was removed;
  the original profile, 14 mods and 11 plugins retain names, states and priorities.

To repeat, prepare a fresh specification with
`python frontend/tools/filter_refresh_probe.py prepare --spec <new-spec.json>`.
Run the visible-filter flag above with `MO2_FILTER_REFRESH_MOD` set to the returned
fixture name. Always clean up afterward using the helper's `cleanup` operation
with the same specification. The helper requires the isolated FNV mods directory,
checks ownership and file contents before removal, and verifies restored state.

Final build: eight warnings, zero errors. Python compilation and diff hygiene
pass. This is real native mutation plus Avalonia control/layout observation;
physical-input, full-frame visual and large-library latency acceptance remain
separate requirements.

## Current bridge on FNV and Skyrim — 2026-09-20

The populated Skyrim installation at
`/home/deck/Games/mod-organizer-2-skyrimspecialedition/modorganizer2` now runs the
current bridge package. Its host was closed normally, the three changed Python
files were installed with a backup, and a new host/session was verified. FNV did
not require a restart. Both installed packages match source.

The expanded `MO2_VERIFY_GAME_SWITCH=1` check verifies every unfiltered mod
identity, native criteria/counts/include/invert/And/Or, plugin table identities and
Data entries during FNV → Skyrim → FNV. It exposed a sidebar default that picked
the first registered Skyrim installation and launched an older host. The default
now prefers a running host; remembered instance/profile choices remain primary
and avoid additional process scans. The test requires the exact expected Skyrim
endpoint, so a same-game installation cannot accidentally satisfy it.

- `artifacts/game-switch-running-host-fixed.log`: complete round trip, 24 FNV and
  25 Skyrim status/content criteria, 11 FNV and 150 Skyrim plugin identities.
- `artifacts/skyrim-native-filter-current-query.json`: 25 native criteria over
  151 mods in 212 ms, known mod identities and confirmed native view restoration.
  This is one native execution duration, not end-to-end interaction latency.
- `artifacts/two-game-current-bridge-restoration.json`: original profile and
  mod/plugin names, activation states and priorities preserved for both hosts
  (FNV 14/11, Skyrim 151/150); both installed bridge packages match source.

Release build: eight warnings, zero errors; diff hygiene passes. The older Skyrim
host launched during diagnosis was observed stopped before the passing run.
Physical input, full-frame visuals, populated Saves and broader production gates
remain separate acceptance work.
