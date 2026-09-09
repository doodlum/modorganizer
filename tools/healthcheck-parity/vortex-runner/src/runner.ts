/**
 * Parity runner — Vortex side.
 *
 * Feeds a shared fixture through *Vortex's own* health-check code and prints a
 * canonical JSON report. The MO2 runner (tools/healthcheck-parity/mo2-runner)
 * prints the same shape from the C++ port; compare.mjs diffs the two.
 *
 * The two algorithm files are imported verbatim from a Vortex checkout:
 *
 *   packages/file-dependency-resolver/src/checkFileLevelRequirements.ts
 *     -> the resolver (classification, recommendation, OR handling)
 *   src/renderer/src/extensions/health_check/utils/fileRequirements/
 *     mapRequirementsReport.ts
 *     -> the mapper (requirement kinds surfaced to the UI)
 *
 * Nothing here reimplements either. The only substitution is a leaf stub for
 * `@/extensions/nexus_integration/util/UIDs`, whose sole runtime export used by
 * the mapper is the VORTEX_MOD_UID constant; the stub pins the same literal and
 * asserts it against the real source at build time (see build.mjs).
 */

import { checkFileLevelRequirements } from "@nexusmods/file-dependency-resolver";
import type {
  CandidateRow,
  FileVersionDetail,
  ModDetail,
} from "@nexusmods/file-dependency-resolver";

import { mapRequirementsReport } from "@vortex-health-check/mapRequirementsReport";
import type {
  HydratedFile,
  IFileRequirementsCheckMetadata,
} from "@vortex-health-check/mapRequirementsReport";

import { readFileSync } from "node:fs";

/** The on-disk fixture shape, shared byte-for-byte with the MO2 runner. */
interface Fixture {
  name: string;
  description?: string;
  gameId: string;
  installedFiles: Array<{
    fileVersionUid: string;
    enabled: boolean;
    emitRequirements?: boolean;
  }>;
  uninstalledFileVersionUids: string[];
  /** Rows the fetchCandidates port returns, keyed by nothing — filtered here. */
  candidates: CandidateRow[];
  fileVersionDetails: FileVersionDetail[];
  modDetails: ModDetail[];
  /** fileUID -> display data, standing in for Vortex's store hydration. */
  hydration: Record<
    string,
    | { kind: "installed"; file: Record<string, unknown> }
    | { kind: "downloaded"; file: Record<string, unknown> }
  >;
}

const fixturePath = process.argv[2];
if (!fixturePath) {
  console.error("usage: runner <fixture.json>");
  process.exit(2);
}

const fixture: Fixture = JSON.parse(readFileSync(fixturePath, "utf8"));

// Ports read straight from the fixture. Filtering mirrors what the real v3
// endpoints do (only rows for the requested ids come back), so the resolver
// sees the same inputs it would in the app.
const ports = {
  async fetchCandidates(fileVersionUids: string[]): Promise<CandidateRow[]> {
    const wanted = new Set(fileVersionUids);
    return fixture.candidates.filter((row) => wanted.has(row.sourceFileVersionUid));
  },
  async fetchFileVersionDetails(fileVersionUids: string[]): Promise<FileVersionDetail[]> {
    const wanted = new Set(fileVersionUids);
    return fixture.fileVersionDetails.filter((detail) => wanted.has(detail.fileVersionUid));
  },
  async fetchModDetails(modUids: string[]): Promise<ModDetail[]> {
    const wanted = new Set(modUids);
    return fixture.modDetails.filter((mod) => wanted.has(mod.modUid));
  },
};

const hydrate = (fileUID: string): HydratedFile | undefined => {
  const entry = fixture.hydration[fileUID];
  if (!entry) {
    return undefined;
  }
  return entry as unknown as HydratedFile;
};

async function main(): Promise<void> {
  const report = await checkFileLevelRequirements({
    installedFiles: fixture.installedFiles,
    uninstalledFileVersionUids: new Set(fixture.uninstalledFileVersionUids),
    ports,
  });

  const metadata: IFileRequirementsCheckMetadata = mapRequirementsReport(report, hydrate, {
    gameId: fixture.gameId,
    modsChecked: fixture.installedFiles.length,
    errors: [],
  });

  // Two artefacts per fixture: the raw resolver report and the mapped
  // metadata. Comparing both localises any divergence to one stage.
  process.stdout.write(
    JSON.stringify(
      {
        fixture: fixture.name,
        report,
        metadata,
      },
      null,
      2,
    ) + "\n",
  );
}

void main().catch((err: unknown) => {
  console.error(err);
  process.exit(1);
});
