/**
 * Generates the shared parity fixture corpus.
 *
 * Each fixture is a self-contained description of one resolver run: the files
 * the user has, what the server would return, and the display data the app
 * would hydrate. Both runners (Vortex TS, MO2 C++) consume the exact same
 * files, so any behavioural difference shows up as a JSON diff.
 *
 * The corpus is written to exercise every decision point in
 *   packages/file-dependency-resolver/src/checkFileLevelRequirements.ts
 *   src/renderer/.../fileRequirements/mapRequirementsReport.ts
 * including the paths that deliberately drop an issue.
 *
 * Usage: node generate.mjs
 */

import { writeFileSync, mkdirSync, readdirSync, unlinkSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const outDir = join(here, "cases");

// Vortex's own Nexus listing; the mapper treats a dependency on it as satisfied.
// Pinned from src/renderer/src/extensions/nexus_integration/util/UIDs.ts:112
const VORTEX_MOD_UID = "9856949944321";

const CATEGORY = {
  main: 1,
  update: 2,
  optional: 3,
  oldVersion: 4,
  miscellaneous: 5,
  removed: 6,
  archived: 7,
};

/** Shorthand for a candidate row. */
const row = ({
  source,
  def,
  chain,
  version,
  position,
  category = CATEGORY.main,
  status = "published",
  mod,
}) => ({
  sourceFileVersionUid: source,
  definitionId: def,
  modFileId: chain,
  fileVersionUid: version,
  position: String(position),
  category,
  modStatus: status,
  modUid: mod,
});

/** Shorthand for a file-version detail row. */
const detail = (uid, mod, chain, name, version) => ({
  fileVersionUid: uid,
  modUid: mod,
  modFileId: chain,
  name,
  version,
});

/** Shorthand for a mod detail row. */
const modDetail = (uid, name, extra = {}) => ({
  modUid: uid,
  name,
  summary: extra.summary,
  thumbnailUrl: extra.thumbnailUrl,
  adultContent: extra.adultContent ?? false,
});

/** Display data for an installed file. */
const installedHydration = (uid, { modId, modName, fileName, version, enabled }) => ({
  kind: "installed",
  file: {
    modId,
    fileUID: uid,
    modUID: `mod-of-${uid}`,
    modName,
    fileName,
    version,
    adultContent: false,
    enabled,
  },
});

/** Display data for a downloaded-but-not-installed file. */
const downloadedHydration = (uid, { downloadId, modName, fileName, version }) => ({
  kind: "downloaded",
  file: {
    downloadId,
    fileUID: uid,
    modUID: `mod-of-${uid}`,
    modName,
    fileName,
    version,
    adultContent: false,
  },
});

const fixtures = [];
const add = (fixture) => fixtures.push(fixture);

// ---------------------------------------------------------------------------
// 01 - missing: nothing owned on the only branch, a candidate is available.
// ---------------------------------------------------------------------------
add({
  name: "01-missing",
  description:
    "Single-branch dependency with nothing owned; resolver recommends the " +
    "highest-position active candidate and the mapper surfaces kind=missing.",
  gameId: "fallout4",
  installedFiles: [{ fileVersionUid: "src1", enabled: true }],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "c1", version: "v1", position: 1, mod: "m1" }),
    row({ source: "src1", def: "d1", chain: "c1", version: "v2", position: 5, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("v1", "m1", "c1", "Dep File 1.0", "1.0"),
    detail("v2", "m1", "c1", "Dep File 2.0", "2.0"),
  ],
  modDetails: [modDetail("m1", "Dependency Mod", { summary: "A dependency" })],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 02 - wrong-version-installed: a wrong version of the chain is enabled.
// ---------------------------------------------------------------------------
add({
  name: "02-wrong-version-installed",
  description:
    "The user has an out-of-range version of the required chain enabled and " +
    "owns no acceptable version, so a different version must be downloaded.",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "src1", enabled: true },
    { fileVersionUid: "old1", enabled: true },
  ],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "c1", version: "v2", position: 5, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    // old1 shares chain c1 with the candidate but is not itself acceptable.
    detail("old1", "m1", "c1", "Dep File 1.0", "1.0"),
    detail("v2", "m1", "c1", "Dep File 2.0", "2.0"),
  ],
  modDetails: [modDetail("m1", "Dependency Mod")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
    old1: installedHydration("old1", {
      modId: "DepMod",
      modName: "Dependency Mod",
      fileName: "Dep File 1.0",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 03 - wrong-version-enabled: correct version owned but disabled, wrong enabled.
// ---------------------------------------------------------------------------
add({
  name: "03-wrong-version-enabled",
  description:
    "Both the acceptable and an unacceptable version are installed, with the " +
    "unacceptable one enabled: the fix is a version switch, not a download.",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "src1", enabled: true },
    { fileVersionUid: "old1", enabled: true },
    { fileVersionUid: "v2", enabled: false },
  ],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "c1", version: "v2", position: 5, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("old1", "m1", "c1", "Dep File 1.0", "1.0"),
    detail("v2", "m1", "c1", "Dep File 2.0", "2.0"),
  ],
  modDetails: [modDetail("m1", "Dependency Mod")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
    old1: installedHydration("old1", {
      modId: "DepMod-old",
      modName: "Dependency Mod",
      fileName: "Dep File 1.0",
      version: "1.0",
      enabled: true,
    }),
    v2: installedHydration("v2", {
      modId: "DepMod-new",
      modName: "Dependency Mod",
      fileName: "Dep File 2.0",
      version: "2.0",
      enabled: false,
    }),
  },
});

// ---------------------------------------------------------------------------
// 04 - correct-version-uninstalled: acceptable version downloaded, not installed.
// ---------------------------------------------------------------------------
add({
  name: "04-correct-version-uninstalled",
  description:
    "The acceptable version is already in the downloads folder, so the fix is " +
    "an install rather than a download.",
  gameId: "fallout4",
  installedFiles: [{ fileVersionUid: "src1", enabled: true }],
  uninstalledFileVersionUids: ["v2"],
  candidates: [
    row({ source: "src1", def: "d1", chain: "c1", version: "v2", position: 5, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("v2", "m1", "c1", "Dep File 2.0", "2.0"),
  ],
  modDetails: [modDetail("m1", "Dependency Mod")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
    v2: downloadedHydration("v2", {
      downloadId: "12",
      modName: "Dependency Mod",
      fileName: "Dep File 2.0",
      version: "2.0",
    }),
  },
});

// ---------------------------------------------------------------------------
// 05 - correct-version-uninstalled with a wrong version enabled (a switch).
// ---------------------------------------------------------------------------
add({
  name: "05-uninstalled-with-wrong-enabled",
  description:
    "As 04, but an unacceptable version of the same chain is enabled, so the " +
    "requirement also carries the file that install would replace.",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "src1", enabled: true },
    { fileVersionUid: "old1", enabled: true },
  ],
  uninstalledFileVersionUids: ["v2"],
  candidates: [
    row({ source: "src1", def: "d1", chain: "c1", version: "v2", position: 5, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("old1", "m1", "c1", "Dep File 1.0", "1.0"),
    detail("v2", "m1", "c1", "Dep File 2.0", "2.0"),
  ],
  modDetails: [modDetail("m1", "Dependency Mod")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
    old1: installedHydration("old1", {
      modId: "DepMod-old",
      modName: "Dependency Mod",
      fileName: "Dep File 1.0",
      version: "1.0",
      enabled: true,
    }),
    v2: downloadedHydration("v2", {
      downloadId: "12",
      modName: "Dependency Mod",
      fileName: "Dep File 2.0",
      version: "2.0",
    }),
  },
});

// ---------------------------------------------------------------------------
// 06 - satisfied: an acceptable version is enabled. Nothing is surfaced.
// ---------------------------------------------------------------------------
add({
  name: "06-satisfied-enabled",
  description:
    "An acceptable version is installed and enabled, so the dependency is " +
    "dropped and the source contributes no entry at all.",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "src1", enabled: true },
    { fileVersionUid: "v2", enabled: true },
  ],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "c1", version: "v2", position: 5, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("v2", "m1", "c1", "Dep File 2.0", "2.0"),
  ],
  modDetails: [modDetail("m1", "Dependency Mod")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 07 - owned but deliberately disabled, nothing wrong enabled: dropped.
// ---------------------------------------------------------------------------
add({
  name: "07-disabled-deliberate",
  description:
    "The acceptable version is installed but disabled and nothing else on the " +
    "chain is enabled. Vortex reads that as a deliberate choice and drops it " +
    "(mapRequirementsReport.ts:261-263).",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "src1", enabled: true },
    { fileVersionUid: "v2", enabled: false },
  ],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "c1", version: "v2", position: 5, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("v2", "m1", "c1", "Dep File 2.0", "2.0"),
  ],
  modDetails: [modDetail("m1", "Dependency Mod")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
    v2: installedHydration("v2", {
      modId: "DepMod",
      modName: "Dependency Mod",
      fileName: "Dep File 2.0",
      version: "2.0",
      enabled: false,
    }),
  },
});

// ---------------------------------------------------------------------------
// 08 - OR where every alternative needs a download.
// ---------------------------------------------------------------------------
add({
  name: "08-or-download",
  description:
    "Two update groups satisfy one definition and neither is owned, so both " +
    "surface as download alternatives the user picks between.",
  gameId: "fallout4",
  installedFiles: [{ fileVersionUid: "src1", enabled: true }],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "cA", version: "vA", position: 3, mod: "mA" }),
    row({ source: "src1", def: "d1", chain: "cB", version: "vB", position: 4, mod: "mB" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("vA", "mA", "cA", "Alternative A", "1.0"),
    detail("vB", "mB", "cB", "Alternative B", "2.0"),
  ],
  modDetails: [modDetail("mA", "Alternative Mod A"), modDetail("mB", "Alternative Mod B")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 09 - OR satisfied by an enabled alternative: dropped, and no recommendations.
// ---------------------------------------------------------------------------
add({
  name: "09-or-satisfied-enabled",
  description:
    "One OR alternative is enabled, so the whole definition is satisfied. Also " +
    "pins checkFileLevelRequirements.ts:102-105: the *other* branch must have " +
    "its recommendation cleared in the raw report, not merely hidden by the mapper.",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "src1", enabled: true },
    { fileVersionUid: "vA", enabled: true },
  ],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "cA", version: "vA", position: 3, mod: "mA" }),
    row({ source: "src1", def: "d1", chain: "cB", version: "vB", position: 4, mod: "mB" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("vA", "mA", "cA", "Alternative A", "1.0"),
    detail("vB", "mB", "cB", "Alternative B", "2.0"),
  ],
  modDetails: [modDetail("mA", "Alternative Mod A"), modDetail("mB", "Alternative Mod B")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 10 - OR with a disabled alternative and a wrong version enabled: enable branch.
// ---------------------------------------------------------------------------
add({
  name: "10-or-enable-branch",
  description:
    "An OR alternative is owned-but-disabled while a wrong version of that same " +
    "chain is enabled, so the guard at mapRequirementsReport.ts:228 does not " +
    "fire and the branch surfaces as an enable/switch.",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "src1", enabled: true },
    { fileVersionUid: "vA", enabled: false },
    { fileVersionUid: "oldA", enabled: true },
  ],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "cA", version: "vA", position: 3, mod: "mA" }),
    row({ source: "src1", def: "d1", chain: "cB", version: "vB", position: 4, mod: "mB" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("vA", "mA", "cA", "Alternative A 2.0", "2.0"),
    detail("oldA", "mA", "cA", "Alternative A 1.0", "1.0"),
    detail("vB", "mB", "cB", "Alternative B", "2.0"),
  ],
  modDetails: [modDetail("mA", "Alternative Mod A"), modDetail("mB", "Alternative Mod B")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
    vA: installedHydration("vA", {
      modId: "AltA-new",
      modName: "Alternative Mod A",
      fileName: "Alternative A 2.0",
      version: "2.0",
      enabled: false,
    }),
    oldA: installedHydration("oldA", {
      modId: "AltA-old",
      modName: "Alternative Mod A",
      fileName: "Alternative A 1.0",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 11 - OR with a disabled alternative and nothing wrong enabled: dropped.
// ---------------------------------------------------------------------------
add({
  name: "11-or-disabled-deliberate",
  description:
    "Mirror of 10 without the wrong version enabled: the guard at " +
    "mapRequirementsReport.ts:228 treats the disabled alternative as a " +
    "deliberate choice and drops the whole OR.",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "src1", enabled: true },
    { fileVersionUid: "vA", enabled: false },
  ],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "cA", version: "vA", position: 3, mod: "mA" }),
    row({ source: "src1", def: "d1", chain: "cB", version: "vB", position: 4, mod: "mB" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("vA", "mA", "cA", "Alternative A", "2.0"),
    detail("vB", "mB", "cB", "Alternative B", "2.0"),
  ],
  modDetails: [modDetail("mA", "Alternative Mod A"), modDetail("mB", "Alternative Mod B")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
    vA: installedHydration("vA", {
      modId: "AltA",
      modName: "Alternative Mod A",
      fileName: "Alternative A",
      version: "2.0",
      enabled: false,
    }),
  },
});

// ---------------------------------------------------------------------------
// 12 - OR with a downloaded alternative: install branch.
// ---------------------------------------------------------------------------
add({
  name: "12-or-install-branch",
  description:
    "One OR alternative is downloaded but not installed, so that branch offers " +
    "an install while the other still offers a download.",
  gameId: "fallout4",
  installedFiles: [{ fileVersionUid: "src1", enabled: true }],
  uninstalledFileVersionUids: ["vA"],
  candidates: [
    row({ source: "src1", def: "d1", chain: "cA", version: "vA", position: 3, mod: "mA" }),
    row({ source: "src1", def: "d1", chain: "cB", version: "vB", position: 4, mod: "mB" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("vA", "mA", "cA", "Alternative A", "1.0"),
    detail("vB", "mB", "cB", "Alternative B", "2.0"),
  ],
  modDetails: [modDetail("mA", "Alternative Mod A"), modDetail("mB", "Alternative Mod B")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
    vA: downloadedHydration("vA", {
      downloadId: "44",
      modName: "Alternative Mod A",
      fileName: "Alternative A",
      version: "1.0",
    }),
  },
});

// ---------------------------------------------------------------------------
// 13 - dependency on Vortex itself: always satisfied.
// ---------------------------------------------------------------------------
add({
  name: "13-vortex-self-requirement",
  description:
    "A dependency whose recommended candidate is Vortex's own Nexus listing is " +
    "treated as satisfied (mapRequirementsReport.ts:211-214).",
  gameId: "fallout4",
  installedFiles: [{ fileVersionUid: "src1", enabled: true }],
  uninstalledFileVersionUids: [],
  candidates: [
    row({
      source: "src1",
      def: "d1",
      chain: "cV",
      version: "vV",
      position: 1,
      mod: VORTEX_MOD_UID,
    }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("vV", VORTEX_MOD_UID, "cV", "Vortex", "1.0"),
  ],
  modDetails: [modDetail(VORTEX_MOD_UID, "Vortex")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 14 - recommendation ranking: active categories win, then highest position.
// ---------------------------------------------------------------------------
add({
  name: "14-recommendation-ranking",
  description:
    "An archived candidate has the highest position, but archived is not an " +
    "'active' category, so the highest-position *active* candidate wins " +
    "(checkFileLevelRequirements.ts:218-223).",
  gameId: "fallout4",
  installedFiles: [{ fileVersionUid: "src1", enabled: true }],
  uninstalledFileVersionUids: [],
  candidates: [
    row({
      source: "src1",
      def: "d1",
      chain: "c1",
      version: "vArchived",
      position: 99,
      category: CATEGORY.archived,
      mod: "m1",
    }),
    row({
      source: "src1",
      def: "d1",
      chain: "c1",
      version: "vMain",
      position: 10,
      category: CATEGORY.main,
      mod: "m1",
    }),
    row({
      source: "src1",
      def: "d1",
      chain: "c1",
      version: "vOptional",
      position: 4,
      category: CATEGORY.optional,
      mod: "m1",
    }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("vArchived", "m1", "c1", "Archived Build", "9.9"),
    detail("vMain", "m1", "c1", "Main Build", "1.0"),
    detail("vOptional", "m1", "c1", "Optional Build", "0.4"),
  ],
  modDetails: [modDetail("m1", "Dependency Mod")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 15 - position ties resolve to the earliest row.
// ---------------------------------------------------------------------------
add({
  name: "15-position-tie",
  description:
    "Two active candidates share the highest position. The TS reduce only " +
    "replaces on a strictly greater value, so the earlier row wins " +
    "(checkFileLevelRequirements.ts:212-214).",
  gameId: "fallout4",
  installedFiles: [{ fileVersionUid: "src1", enabled: true }],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "c1", version: "vFirst", position: 7, mod: "m1" }),
    row({ source: "src1", def: "d1", chain: "c1", version: "vSecond", position: 7, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("vFirst", "m1", "c1", "First Build", "1.0"),
    detail("vSecond", "m1", "c1", "Second Build", "1.0"),
  ],
  modDetails: [modDetail("m1", "Dependency Mod")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 16 - no eligible candidate: nothing to recommend, so the issue is dropped.
// ---------------------------------------------------------------------------
add({
  name: "16-no-eligible-candidate",
  description:
    "Every candidate is either a removed category or an unpublished mod, so " +
    "selectRecommended returns nothing and the mapper drops the dependency.",
  gameId: "fallout4",
  installedFiles: [{ fileVersionUid: "src1", enabled: true }],
  uninstalledFileVersionUids: [],
  candidates: [
    row({
      source: "src1",
      def: "d1",
      chain: "c1",
      version: "vRemoved",
      position: 9,
      category: CATEGORY.removed,
      mod: "m1",
    }),
    row({
      source: "src1",
      def: "d1",
      chain: "c1",
      version: "vUnderModeration",
      position: 8,
      category: CATEGORY.main,
      status: "under_moderation",
      mod: "m1",
    }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("vRemoved", "m1", "c1", "Removed Build", "9.0"),
    detail("vUnderModeration", "m1", "c1", "Hidden Build", "8.0"),
  ],
  modDetails: [modDetail("m1", "Dependency Mod")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 17 - a hidden-status mod is still matchable and recommendable.
// ---------------------------------------------------------------------------
add({
  name: "17-hidden-status-allowed",
  description:
    "'hidden' is in availableStatuses (checkFileLevelRequirements.ts:38), so a " +
    "hidden mod's file is still recommended when nothing else qualifies.",
  gameId: "fallout4",
  installedFiles: [{ fileVersionUid: "src1", enabled: true }],
  uninstalledFileVersionUids: [],
  candidates: [
    row({
      source: "src1",
      def: "d1",
      chain: "c1",
      version: "vHidden",
      position: 3,
      status: "hidden",
      mod: "m1",
    }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("vHidden", "m1", "c1", "Hidden Mod File", "1.0"),
  ],
  modDetails: [modDetail("m1", "Hidden Mod")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 18 - collection-managed source emits nothing but still satisfies others.
// ---------------------------------------------------------------------------
add({
  name: "18-collection-managed-source",
  description:
    "src2 is collection-managed (emitRequirements false): it contributes no " +
    "requirements of its own, but it still counts as installed when deciding " +
    "whether src1's dependency is satisfied.",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "src1", enabled: true },
    { fileVersionUid: "src2", enabled: true, emitRequirements: false },
  ],
  uninstalledFileVersionUids: [],
  candidates: [
    // src1 needs src2, which is installed and enabled -> satisfied.
    row({ source: "src1", def: "d1", chain: "c2", version: "src2", position: 1, mod: "m2" }),
    // src2 would have needed something, but it emits nothing.
    row({ source: "src2", def: "d2", chain: "c3", version: "v3", position: 1, mod: "m3" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("src2", "m2", "c2", "Collection Mod File", "1.0"),
    detail("v3", "m3", "c3", "Never Requested", "1.0"),
  ],
  modDetails: [modDetail("m2", "Collection Mod"), modDetail("m3", "Unreached Mod")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
    src2: installedHydration("src2", {
      modId: "CollectionMod",
      modName: "Collection Mod",
      fileName: "Collection Mod File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 19 - a disabled source emits no requirements.
// ---------------------------------------------------------------------------
add({
  name: "19-disabled-source",
  description:
    "Only enabled files are sources (checkFileLevelRequirements.ts:45), so a " +
    "disabled mod's unmet dependency is not reported.",
  gameId: "fallout4",
  installedFiles: [{ fileVersionUid: "src1", enabled: false }],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "c1", version: "v1", position: 1, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("v1", "m1", "c1", "Dep File", "1.0"),
  ],
  modDetails: [modDetail("m1", "Dependency Mod")],
  hydration: {},
});

// ---------------------------------------------------------------------------
// 20 - hydration miss on the wrong-enabled file drops the requirement.
// ---------------------------------------------------------------------------
add({
  name: "20-hydration-miss-drops",
  description:
    "A wrong version is enabled but its display data cannot be resolved, so " +
    "the mapper returns undefined rather than a half-rendered card " +
    "(mapRequirementsReport.ts:288-291).",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "src1", enabled: true },
    { fileVersionUid: "old1", enabled: true },
  ],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "c1", version: "v2", position: 5, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("old1", "m1", "c1", "Dep File 1.0", "1.0"),
    detail("v2", "m1", "c1", "Dep File 2.0", "2.0"),
  ],
  modDetails: [modDetail("m1", "Dependency Mod")],
  // old1 deliberately absent from hydration.
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 21 - no candidates at all: empty report.
// ---------------------------------------------------------------------------
add({
  name: "21-no-candidates",
  description:
    "The server returns no dependency rows, so the resolver short-circuits to " +
    "an empty report (checkFileLevelRequirements.ts:70).",
  gameId: "fallout4",
  installedFiles: [{ fileVersionUid: "src1", enabled: true }],
  uninstalledFileVersionUids: [],
  candidates: [],
  fileVersionDetails: [detail("src1", "msrc", "csrc", "Source File", "1.0")],
  modDetails: [],
  hydration: {},
});

// ---------------------------------------------------------------------------
// 22 - several sources, several dependencies, mixed outcomes.
// ---------------------------------------------------------------------------
add({
  name: "22-multi-source-mixed",
  description:
    "Three enabled sources with a mix of satisfied, missing, switchable and OR " +
    "dependencies, exercising grouping and per-source aggregation together.",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "srcA", enabled: true },
    { fileVersionUid: "srcB", enabled: true },
    { fileVersionUid: "srcC", enabled: true },
    { fileVersionUid: "depEnabled", enabled: true },
    { fileVersionUid: "depOld", enabled: true },
    { fileVersionUid: "depNew", enabled: false },
  ],
  uninstalledFileVersionUids: ["depDownloaded"],
  candidates: [
    // srcA: one satisfied, one missing.
    row({
      source: "srcA",
      def: "dA1",
      chain: "cEnabled",
      version: "depEnabled",
      position: 1,
      mod: "mEnabled",
    }),
    row({
      source: "srcA",
      def: "dA2",
      chain: "cMissing",
      version: "depMissing",
      position: 2,
      mod: "mMissing",
    }),
    // srcB: a version switch.
    row({
      source: "srcB",
      def: "dB1",
      chain: "cSwitch",
      version: "depNew",
      position: 5,
      mod: "mSwitch",
    }),
    // srcC: an OR, one side downloaded.
    row({
      source: "srcC",
      def: "dC1",
      chain: "cAlt1",
      version: "depDownloaded",
      position: 3,
      mod: "mAlt1",
    }),
    row({
      source: "srcC",
      def: "dC1",
      chain: "cAlt2",
      version: "depAlt2",
      position: 4,
      mod: "mAlt2",
    }),
  ],
  fileVersionDetails: [
    detail("srcA", "mSrcA", "cSrcA", "Source A", "1.0"),
    detail("srcB", "mSrcB", "cSrcB", "Source B", "1.0"),
    detail("srcC", "mSrcC", "cSrcC", "Source C", "1.0"),
    detail("depEnabled", "mEnabled", "cEnabled", "Enabled Dep", "1.0"),
    detail("depMissing", "mMissing", "cMissing", "Missing Dep", "1.0"),
    detail("depOld", "mSwitch", "cSwitch", "Switch Dep 1.0", "1.0"),
    detail("depNew", "mSwitch", "cSwitch", "Switch Dep 2.0", "2.0"),
    detail("depDownloaded", "mAlt1", "cAlt1", "Alt 1", "1.0"),
    detail("depAlt2", "mAlt2", "cAlt2", "Alt 2", "1.0"),
  ],
  modDetails: [
    modDetail("mEnabled", "Enabled Mod"),
    modDetail("mMissing", "Missing Mod", { summary: "Not installed" }),
    modDetail("mSwitch", "Switchable Mod"),
    modDetail("mAlt1", "Alternative One"),
    modDetail("mAlt2", "Alternative Two"),
  ],
  hydration: {
    srcA: installedHydration("srcA", {
      modId: "SourceA",
      modName: "Source A",
      fileName: "Source A",
      version: "1.0",
      enabled: true,
    }),
    srcB: installedHydration("srcB", {
      modId: "SourceB",
      modName: "Source B",
      fileName: "Source B",
      version: "1.0",
      enabled: true,
    }),
    srcC: installedHydration("srcC", {
      modId: "SourceC",
      modName: "Source C",
      fileName: "Source C",
      version: "1.0",
      enabled: true,
    }),
    depOld: installedHydration("depOld", {
      modId: "SwitchOld",
      modName: "Switchable Mod",
      fileName: "Switch Dep 1.0",
      version: "1.0",
      enabled: true,
    }),
    depNew: installedHydration("depNew", {
      modId: "SwitchNew",
      modName: "Switchable Mod",
      fileName: "Switch Dep 2.0",
      version: "2.0",
      enabled: false,
    }),
    depDownloaded: downloadedHydration("depDownloaded", {
      downloadId: "77",
      modName: "Alternative One",
      fileName: "Alt 1",
      version: "1.0",
    }),
  },
});

// ---------------------------------------------------------------------------
// 23 - the same dependency declared by two different sources.
// ---------------------------------------------------------------------------
add({
  name: "23-shared-dependency",
  description:
    "Two sources need the same missing file. Each source gets its own entry; " +
    "the resolver does not deduplicate across sources.",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "srcA", enabled: true },
    { fileVersionUid: "srcB", enabled: true },
  ],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "srcA", def: "dA", chain: "c1", version: "v1", position: 1, mod: "m1" }),
    row({ source: "srcB", def: "dB", chain: "c1", version: "v1", position: 1, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("srcA", "mSrcA", "cSrcA", "Source A", "1.0"),
    detail("srcB", "mSrcB", "cSrcB", "Source B", "1.0"),
    detail("v1", "m1", "c1", "Shared Dep", "1.0"),
  ],
  modDetails: [modDetail("m1", "Shared Dependency")],
  hydration: {
    srcA: installedHydration("srcA", {
      modId: "SourceA",
      modName: "Source A",
      fileName: "Source A",
      version: "1.0",
      enabled: true,
    }),
    srcB: installedHydration("srcB", {
      modId: "SourceB",
      modName: "Source B",
      fileName: "Source B",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 24 - a source file with no version detail (unknown chain).
// ---------------------------------------------------------------------------
add({
  name: "24-unknown-chain",
  description:
    "An installed file the details endpoint does not know about is skipped when " +
    "building installedByChain (checkFileLevelRequirements.ts:61-62), so it " +
    "never counts as a wrong version.",
  gameId: "fallout4",
  installedFiles: [
    { fileVersionUid: "src1", enabled: true },
    { fileVersionUid: "ghost", enabled: true },
  ],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "c1", version: "v1", position: 1, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("v1", "m1", "c1", "Dep File", "1.0"),
    // "ghost" deliberately absent.
  ],
  modDetails: [modDetail("m1", "Dependency Mod")],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// ---------------------------------------------------------------------------
// 25 - adult-content and summary flow through to the candidate.
// ---------------------------------------------------------------------------
add({
  name: "25-adult-content-passthrough",
  description:
    "Mod-level display fields (summary, thumbnail, adult flag) are copied onto " +
    "the hydrated candidate (checkFileLevelRequirements.ts:225-243).",
  gameId: "fallout4",
  installedFiles: [{ fileVersionUid: "src1", enabled: true }],
  uninstalledFileVersionUids: [],
  candidates: [
    row({ source: "src1", def: "d1", chain: "c1", version: "v1", position: 1, mod: "m1" }),
  ],
  fileVersionDetails: [
    detail("src1", "msrc", "csrc", "Source File", "1.0"),
    detail("v1", "m1", "c1", "Adult Dep File", "1.0"),
  ],
  modDetails: [
    modDetail("m1", "Adult Dependency", {
      summary: "An adult mod",
      thumbnailUrl: "https://example.invalid/thumb.png",
      adultContent: true,
    }),
  ],
  hydration: {
    src1: installedHydration("src1", {
      modId: "SourceMod",
      modName: "Source Mod",
      fileName: "Source File",
      version: "1.0",
      enabled: true,
    }),
  },
});

// --- write -----------------------------------------------------------------

mkdirSync(outDir, { recursive: true });
for (const file of readdirSync(outDir)) {
  if (file.endsWith(".json")) {
    unlinkSync(join(outDir, file));
  }
}

for (const fixture of fixtures) {
  writeFileSync(
    join(outDir, `${fixture.name}.json`),
    JSON.stringify(fixture, null, 2) + "\n",
    "utf8",
  );
}

console.log(`wrote ${fixtures.length} fixtures to ${outDir}`);
