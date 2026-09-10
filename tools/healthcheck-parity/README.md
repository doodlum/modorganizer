# Health check parity suite

Proves that MO2's health check reaches the same verdict as Vortex's, by running
both implementations over identical inputs and diffing the results.

The Vortex side is not a reimplementation. It imports Vortex's own algorithm
files out of a Vortex checkout and runs them directly.

## Running it

```bash
node run.mjs                  # build both runners, run the corpus, compare
node run.mjs --mutations      # also verify the suite is capable of failing
node run.mjs --live           # re-capture the live fixtures first (needs a key)
node run.mjs --vortex <path>  # Vortex checkout is somewhere other than ../../../vortex
```

Exit code 0 only when every fixture matches. `--live` needs `NEXUS_API_KEY`.

Prerequisites: Node 20+, CMake, a Qt 6 build (set `HEALTHCHECK_QT_BIN` if it is
not at `J:/mo2 test/Qt/6.11.1/msvc2022_64/bin`), and a Vortex checkout.

## How it works

```
fixtures/cases/*.json ──┬──► vortex-runner ──► out/vortex/*.json ──┐
                        │    (Vortex's own TS)                     ├──► compare.mjs
                        └──► mo2-runner    ──► out/mo2/*.json    ──┘    (deep diff)
                             (src/healthcheck C++)
```

A fixture describes one resolver run: the files the user has installed and
downloaded, the rows the server would return, and the display data the app would
hydrate. Both runners emit two artefacts per fixture — the raw resolver report
and the mapped metadata — so a divergence localises to one stage rather than
just "the output differs".

`compare.mjs` ignores object key order (meaningless in both implementations) and
is strict about everything else: values, array order, and whether a property is
present at all. Array order matters and is enforced, because the resolver's
grouping order is observable behaviour that both sides claim to reproduce.

### The one substitution

`mapRequirementsReport.ts` imports `VORTEX_MOD_UID` from Vortex's
`nexus_integration/util/UIDs.ts`; importing that module for real would drag in
Vortex's whole renderer graph. `vortex-runner/build.mjs` generates a stub
exporting just that constant, and reads the literal out of the real source to
generate it — so the stub cannot drift from Vortex without the build failing.
Everything else is Vortex's code, unmodified.

### Hydration is passed through, not rebuilt

Vortex's mapper returns the hydrator's object untouched, so the MO2 runner
echoes the fixture's hydration blob rather than re-serialising its own structs.
*Which* file each requirement points at is still fully compared — only the
untouched display payload is passed through.

## The corpus

`fixtures/cases/` holds 34 fixtures: 25 synthetic and 9 captured from the live
Nexus Mods API.

The synthetic set (`fixtures/generate.mjs`) pins each decision point,
including the ones that deliberately drop an issue:

| Fixture | Pins |
| --- | --- |
| 01–05 | each surfaced requirement kind |
| 06, 07 | satisfied, and owned-but-deliberately-disabled |
| 08–12 | OR handling: download, enable and install branches, and both guards |
| 13 | a dependency on Vortex's own listing is always satisfied |
| 14–17 | recommendation ranking: active categories, position ties, availability |
| 18, 19 | collection-managed and disabled sources emit nothing |
| 20 | a hydration miss drops the requirement rather than half-rendering it |
| 21–25 | empty input, multi-source aggregation, and display-field passthrough |

The live set (`fixtures/generate-live.mjs`) captures real dependency ranges,
file categories and mod statuses for a Fallout 4 load order built around
*Visible Favorites - F4SE* (mod 108583), whose latest file declares two real
file-level requirements. Between them the nine scenarios exercise every
requirement kind against data nobody wrote for the test:

```
live-01  both requirements missing            -> 2x missing
live-02  one satisfied                        -> 1x missing
live-03  all satisfied                        -> no issues
live-04  older versions enabled               -> 2x wrong-version-installed
live-05  requirements installed but disabled  -> no issues (deliberate)
live-06  requirements downloaded not installed-> 2x correct-version-uninstalled
live-07  wrong enabled, correct downloaded    -> missing + correct-version-uninstalled
live-08  wrong enabled, correct disabled      -> missing + wrong-version-enabled
live-09  source disabled                      -> no issues
```

## Why the suite is not vacuous

A suite that passes on the first run is worth little on its own.
`mutation-test.mjs` applies nine surgical mutations to the C++ port, rebuilds,
re-runs the corpus, and asserts each is caught by *exactly* the expected
fixtures — no more, no less, so the corpus is neither under- nor
over-constraining. Every mutation is reverted afterwards, including on failure.

The mutations cover: the position tie-break, the active-category preference,
the OR sibling-recommendation clearing, the enabled-source and
collection-managed exclusions, the `hidden` mod status, the
deliberately-disabled drop, the Vortex self-requirement suppression, and the OR
disabled guard.

## Where the logic came from

Every ported file names its Vortex source inline. The map:

| MO2 | Vortex |
| --- | --- |
| `src/healthcheck/filedependencyresolver.cpp` | `packages/file-dependency-resolver/src/checkFileLevelRequirements.ts` |
| `src/healthcheck/healthchecktypes.h` | `packages/file-dependency-resolver/src/types.ts`, `src/renderer/src/types/IHealthCheck.ts` |
| `src/healthcheck/requirementsreportmapper.cpp` | `src/renderer/src/extensions/health_check/utils/fileRequirements/mapRequirementsReport.ts` |
| `src/healthcheck/filerequirementscheck.cpp` | `.../fileRequirements/runFileLevelRequirements.ts`, `.../fileRequirements/installedFiles.ts`, `.../checks/fileRequirementsCheck.ts` |
| `src/healthcheck/healthcheckentries.cpp` | `.../views/content/FileRequirementsContent.tsx`, `.../views/content/fileRequirementEntries.ts`, `.../utils/fileRequirements/fileRequirementReport.ts`, `.../hooks/useReportCopy.ts` |
| `src/healthcheck/healthcheckmanager.cpp` | `.../core/HealthCheckRegistry.ts`, `.../api/triggers.ts` |
| `src/healthcheck/nexusv3client.cpp` | `packages/nexus-api-v3/src/client.ts`, `.../utils/fileRequirements/fileDependencyPorts.ts` |
| `src/healthcheck/nexusuid.cpp` | `src/renderer/src/extensions/nexus_integration/util/UIDs.ts` |
| `src/healthcheck/healthcheckpanel.cpp` | `.../views/HealthCheckPage.tsx`, `.../components/file_requirement/ListingRow.tsx`, `.../utils/shared/severityStyles.ts` |
| toolbar badge (`mainwindow.cpp`) | `.../components/menu_badge/HealthCheckMenuBadge.tsx` |

## Scope

The suite covers the file-level requirements check, which is the whole of MO2's
health check today. Vortex's second check — Nexus *mod*-level requirements
(`checks/modRequirementsCheck.ts`) — is not ported, so it is not covered here;
see the feature notes in the branch description.
