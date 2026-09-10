/**
 * Runs the whole health check parity suite.
 *
 *   node run.mjs                 build both runners, run every fixture, compare
 *   node run.mjs --mutations     also run the harness self-test
 *   node run.mjs --live          regenerate the live fixtures first (needs a key)
 *   node run.mjs --vortex <path> point at a Vortex checkout elsewhere
 *
 * Exit code 0 only when every fixture matches.
 */

import { execFileSync, spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, writeFileSync, rmSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const has = (flag) => args.includes(flag);
const valueOf = (flag, fallback) => {
  const i = args.indexOf(flag);
  return i !== -1 && args[i + 1] ? args[i + 1] : fallback;
};

const vortexRoot = resolve(valueOf("--vortex", join(here, "..", "..", "..", "vortex")));
const qtBin = process.env.HEALTHCHECK_QT_BIN ?? "J:/mo2 test/Qt/6.11.1/msvc2022_64/bin";
const fixturesDir = join(here, "fixtures", "cases");
const outDir = join(here, "out");
const runnerBuild = join(here, "mo2-runner", "build");
const runnerExe = join(runnerBuild, "Release", "healthcheck-parity-runner.exe");

const step = (msg) => console.log(`\n=== ${msg} ===`);
const run = (cmd, argv, opts = {}) =>
  execFileSync(cmd, argv, { encoding: "utf8", stdio: "inherit", ...opts });

// --- 1. optional: refresh the live fixtures --------------------------------

if (has("--live")) {
  step("Regenerating live fixtures from the Nexus API");
  if (!process.env.NEXUS_API_KEY) {
    console.error("NEXUS_API_KEY is not set; cannot regenerate live fixtures.");
    process.exit(2);
  }
  run("node", [join(here, "fixtures", "generate-live.mjs")]);
}

// --- 2. build the Vortex-side runner ---------------------------------------

step("Building the Vortex runner (imports Vortex's own algorithm files)");
if (!existsSync(join(here, "vortex-runner", "node_modules"))) {
  run("npm", ["install", "--silent"], { cwd: join(here, "vortex-runner"), shell: true });
}
run("node", [join(here, "vortex-runner", "build.mjs"), "--vortex", vortexRoot]);

// --- 3. build the MO2-side runner ------------------------------------------

step("Building the MO2 runner (links src/healthcheck)");
if (!existsSync(join(runnerBuild, "CMakeCache.txt"))) {
  run("cmake", [
    "-S", join(here, "mo2-runner"),
    "-B", runnerBuild,
    "-G", "Visual Studio 18 2026",
    "-A", "x64",
    `-DCMAKE_PREFIX_PATH=${resolve(qtBin, "..")}`,
  ]);
}
run("cmake", ["--build", runnerBuild, "--config", "Release", "--parallel", "16"]);

// --- 4. run every fixture through both ------------------------------------

step("Running the corpus through both implementations");
// Clear previous results file-by-file rather than removing the directories:
// on Windows a shell sitting in one of them makes rmdir fail with EBUSY.
for (const side of ["vortex", "mo2"]) {
  const dir = join(outDir, side);
  mkdirSync(dir, { recursive: true });
  for (const file of readdirSync(dir)) {
    rmSync(join(dir, file), { force: true });
  }
}

const env = { ...process.env, PATH: `${qtBin};${process.env.PATH}` };
const fixtures = readdirSync(fixturesDir).filter((f) => f.endsWith(".json")).sort();

let runFailures = 0;
for (const file of fixtures) {
  const name = file.replace(/\.json$/, "");
  const fixture = join(fixturesDir, file);

  const vortex = spawnSync("node", [join(here, "vortex-runner", "dist", "runner.mjs"), fixture],
                           { encoding: "utf8" });
  const mo2 = spawnSync(runnerExe, [fixture], { encoding: "utf8", env });

  if (vortex.status !== 0) {
    console.error(`  vortex runner failed on ${name}: ${vortex.stderr?.split("\n")[0]}`);
    runFailures += 1;
    continue;
  }
  if (mo2.status !== 0) {
    console.error(`  mo2 runner failed on ${name}: ${mo2.stderr?.split("\n")[0]}`);
    runFailures += 1;
    continue;
  }

  writeFileSync(join(outDir, "vortex", `${name}.json`), vortex.stdout, "utf8");
  writeFileSync(join(outDir, "mo2", `${name}.json`), mo2.stdout, "utf8");
}
console.log(`ran ${fixtures.length} fixture(s), ${runFailures} runner failure(s)`);

// --- 5. compare -------------------------------------------------------------

step("Comparing");
const compare = spawnSync("node", [join(here, "compare.mjs")], { stdio: "inherit" });

let failed = runFailures > 0 || compare.status !== 0;

// --- 6. optional: harness self-test ---------------------------------------

if (has("--mutations")) {
  step("Harness self-test (deliberate mutations must be caught)");
  const mutation = spawnSync("node", [join(here, "mutation-test.mjs")], { stdio: "inherit" });
  failed = failed || mutation.status !== 0;
}

process.exit(failed ? 1 : 0);
