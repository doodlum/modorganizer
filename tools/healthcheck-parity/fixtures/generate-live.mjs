/**
 * Generates parity fixtures from live Nexus Mods data.
 *
 * The synthetic corpus (generate.mjs) pins the decision logic. This one pins it
 * against what the API actually returns: real dependency ranges, real file
 * categories, real mod statuses, for a real Fallout 4 load order. Both runners
 * then consume the captured rows, so the comparison stays deterministic and
 * offline once the fixture exists.
 *
 * Requires a Nexus API key:
 *   NEXUS_API_KEY=... node generate-live.mjs
 *
 * Scenarios are built around "Visible Favorites - F4SE" (mod 108583), whose
 * latest file declares two file-level requirements - F4SE and Address Library -
 * which makes it a compact way to exercise satisfied, missing, wrong-version,
 * disabled and downloaded-not-installed outcomes against real data.
 */

import { writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const outDir = join(here, "cases");

const API_KEY = process.env.NEXUS_API_KEY;
if (!API_KEY) {
  console.error("NEXUS_API_KEY is not set; refusing to run.");
  process.exit(2);
}

const UA = "ModOrganizer2-HealthCheckParity/1.0";
const FALLOUT4_GAME_ID = 1151;

/** Composite UID: (gameId << 32) | id. Matches UIDs.ts:34-68. */
const uid = (id, game = FALLOUT4_GAME_ID) =>
  ((BigInt(game) << 32n) | BigInt(id)).toString();

async function v3(path, body) {
  const res = await fetch(`https://api.nexusmods.com/v3${path}`, {
    method: "POST",
    headers: {
      apikey: API_KEY,
      "User-Agent": UA,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
  });
  const text = await res.text();
  if (!res.ok) {
    throw new Error(`v3 ${path} -> ${res.status}: ${text.slice(0, 300)}`);
  }
  return JSON.parse(text);
}

/** Legacy numeric category codes. Vortex: fileDependencyPorts.ts:29-38 */
const CATEGORY_CODES = {
  main: 1,
  update: 2,
  optional: 3,
  old_version: 4,
  miscellaneous: 5,
  removed: 6,
  archived: 7,
  unknown: 0,
};

// Real Fallout 4 files, resolved from the v1 files listing.
const FILES = {
  visibleFavorites: { modId: 108583, fileId: 409875, name: "Visible Favorites - F4SE", version: "1.2.3" },
  f4seCurrent:      { modId: 42147,  fileId: 407709, name: "Fallout 4 Script Extender (F4SE)", version: "0.7.9" },
  f4seOld:          { modId: 42147,  fileId: 398404, name: "Fallout 4 Script Extender (F4SE)", version: "0.7.8" },
  addressCurrent:   { modId: 47327,  fileId: 408268, name: "Address Library for F4SE Plugins", version: "1.11.240" },
  addressOld:       { modId: 47327,  fileId: 398769, name: "Address Library for F4SE Plugins", version: "1.11.221" },
};

/** Fetch every candidate row for the given source file version uids, paged. */
async function fetchCandidates(sourceUids) {
  const rows = [];
  let page = 1;
  for (;;) {
    const res = await v3("/mod-file-versions/dependencies/ranges/materialized/batch", {
      version_ids: sourceUids,
      page,
      page_size: 5000,
    });
    const batch = res.data.candidates;
    rows.push(...batch);
    if (batch.length === 0 || rows.length >= res.meta.total_count) break;
    page += 1;
  }
  return rows.map((row) => ({
    sourceFileVersionUid: row.source_version_id,
    definitionId: row.definition_id,
    modFileId: row.mod_file_id,
    fileVersionUid: row.version_id,
    position: row.position,
    category: CATEGORY_CODES[row.category] ?? 0,
    modStatus: row.mod_status,
    modUid: row.mod_id,
  }));
}

async function fetchVersionDetails(versionUids) {
  if (versionUids.length === 0) return [];
  const res = await v3("/mod-file-versions/batch", { version_ids: versionUids });
  return res.data.versions.map((v) => ({
    fileVersionUid: v.id,
    modUid: v.mod_id,
    modFileId: v.mod_file_id,
    name: v.name,
    version: v.version,
  }));
}

async function fetchModDetails(modUids) {
  if (modUids.length === 0) return [];
  const res = await v3("/mods/batch", { mod_ids: modUids });
  return res.data.mods.map((m) => ({
    modUid: m.id,
    name: m.name,
    summary: m.summary,
    thumbnailUrl: m.thumbnail_url,
    adultContent: m.adult_content,
  }));
}

const installedHydration = (file, enabled) => ({
  kind: "installed",
  file: {
    modId: file.name,
    fileUID: uid(file.fileId),
    modUID: uid(file.modId),
    modName: file.name,
    fileName: `${file.name} ${file.version}`,
    version: file.version,
    adultContent: false,
    enabled,
  },
});

const downloadedHydration = (file, downloadId) => ({
  kind: "downloaded",
  file: {
    downloadId: String(downloadId),
    fileUID: uid(file.fileId),
    modUID: uid(file.modId),
    modName: file.name,
    fileName: `${file.name} ${file.version}`,
    version: file.version,
    adultContent: false,
  },
});

/**
 * Scenarios. `installed` entries become both resolver input and hydration
 * data; `downloaded` entries land in uninstalledFileVersionUids.
 */
const SCENARIOS = [
  {
    name: "live-01-both-requirements-missing",
    description:
      "Visible Favorites enabled on its own. Its two real declared file-level " +
      "requirements (F4SE and Address Library) are both unowned, so both should " +
      "surface as downloads.",
    installed: [{ file: FILES.visibleFavorites, enabled: true }],
    downloaded: [],
  },
  {
    name: "live-02-one-requirement-satisfied",
    description:
      "As 01 with the current F4SE also installed and enabled: that requirement " +
      "drops out and only Address Library remains.",
    installed: [
      { file: FILES.visibleFavorites, enabled: true },
      { file: FILES.f4seCurrent, enabled: true },
    ],
    downloaded: [],
  },
  {
    name: "live-03-all-satisfied",
    description:
      "Both real requirements installed and enabled; the source should surface " +
      "no issues at all.",
    installed: [
      { file: FILES.visibleFavorites, enabled: true },
      { file: FILES.f4seCurrent, enabled: true },
      { file: FILES.addressCurrent, enabled: true },
    ],
    downloaded: [],
  },
  {
    name: "live-04-older-dependency-enabled",
    description:
      "An older F4SE and an older Address Library are enabled. Whether these " +
      "satisfy the declared ranges is the server's call, not ours - the point is " +
      "that both implementations reach the same verdict from the same rows.",
    installed: [
      { file: FILES.visibleFavorites, enabled: true },
      { file: FILES.f4seOld, enabled: true },
      { file: FILES.addressOld, enabled: true },
    ],
    downloaded: [],
  },
  {
    name: "live-05-dependency-installed-but-disabled",
    description:
      "Both requirements are installed but disabled, with nothing else on their " +
      "chains enabled. Vortex reads that as deliberate and drops the issue " +
      "(mapRequirementsReport.ts:261-263).",
    installed: [
      { file: FILES.visibleFavorites, enabled: true },
      { file: FILES.f4seCurrent, enabled: false },
      { file: FILES.addressCurrent, enabled: false },
    ],
    downloaded: [],
  },
  {
    name: "live-06-dependency-downloaded-not-installed",
    description:
      "The requirements sit in the downloads folder rather than installed, so " +
      "they should surface as installs rather than downloads.",
    installed: [{ file: FILES.visibleFavorites, enabled: true }],
    downloaded: [
      { file: FILES.f4seCurrent, downloadId: 101 },
      { file: FILES.addressCurrent, downloadId: 102 },
    ],
  },
  {
    name: "live-07-wrong-version-enabled-correct-downloaded",
    description:
      "An older F4SE is enabled while the current one is only downloaded: the " +
      "two states interact, which is what separates the install and switch paths.",
    installed: [
      { file: FILES.visibleFavorites, enabled: true },
      { file: FILES.f4seOld, enabled: true },
    ],
    downloaded: [{ file: FILES.f4seCurrent, downloadId: 103 }],
  },
  {
    name: "live-08-wrong-version-enabled-correct-disabled",
    description:
      "An older F4SE is enabled while the current one is installed but disabled: " +
      "the version-switch case, against real declared ranges.",
    installed: [
      { file: FILES.visibleFavorites, enabled: true },
      { file: FILES.f4seOld, enabled: true },
      { file: FILES.f4seCurrent, enabled: false },
    ],
    downloaded: [],
  },
  {
    name: "live-09-source-disabled",
    description:
      "The source mod itself is disabled, so it emits nothing even though its " +
      "requirements are unmet.",
    installed: [{ file: FILES.visibleFavorites, enabled: false }],
    downloaded: [],
  },
];

// --- build -----------------------------------------------------------------

mkdirSync(outDir, { recursive: true });

// One candidate fetch covers every scenario: they share the same source file.
const allSourceUids = [
  ...new Set(
    SCENARIOS.flatMap((s) =>
      s.installed.filter((i) => i.enabled).map((i) => uid(i.file.fileId)),
    ),
  ),
];

console.log(`fetching candidates for ${allSourceUids.length} source file version(s)...`);
const candidates = await fetchCandidates(allSourceUids);
console.log(`  ${candidates.length} candidate row(s)`);

// Details for everything either implementation might ask about: the installed
// files (for their update-group ids) and every candidate version.
const allVersionUids = [
  ...new Set([
    ...SCENARIOS.flatMap((s) => [
      ...s.installed.map((i) => uid(i.file.fileId)),
      ...s.downloaded.map((d) => uid(d.file.fileId)),
    ]),
    ...candidates.map((c) => c.fileVersionUid),
  ]),
];
console.log(`fetching details for ${allVersionUids.length} file version(s)...`);
const fileVersionDetails = await fetchVersionDetails(allVersionUids);
console.log(`  ${fileVersionDetails.length} detail row(s)`);

const allModUids = [
  ...new Set([
    ...candidates.map((c) => c.modUid),
    ...fileVersionDetails.map((d) => d.modUid),
    ...Object.values(FILES).map((f) => uid(f.modId)),
  ]),
];
console.log(`fetching ${allModUids.length} mod detail(s)...`);
const modDetails = await fetchModDetails(allModUids);
console.log(`  ${modDetails.length} mod row(s)`);

let written = 0;
for (const scenario of SCENARIOS) {
  const hydration = {};
  for (const entry of scenario.installed) {
    hydration[uid(entry.file.fileId)] = installedHydration(entry.file, entry.enabled);
  }
  for (const entry of scenario.downloaded) {
    hydration[uid(entry.file.fileId)] = downloadedHydration(entry.file, entry.downloadId);
  }

  const fixture = {
    name: scenario.name,
    description: scenario.description,
    source: "Captured from the live Nexus Mods v3 API by generate-live.mjs",
    gameId: "fallout4",
    installedFiles: scenario.installed.map((entry) => ({
      fileVersionUid: uid(entry.file.fileId),
      enabled: entry.enabled,
    })),
    uninstalledFileVersionUids: scenario.downloaded.map((entry) => uid(entry.file.fileId)),
    candidates,
    fileVersionDetails,
    modDetails,
    hydration,
  };

  writeFileSync(
    join(outDir, `${scenario.name}.json`),
    JSON.stringify(fixture, null, 2) + "\n",
    "utf8",
  );
  written += 1;
}

console.log(`wrote ${written} live fixtures to ${outDir}`);
