/**
 * Harness self-test.
 *
 * A parity suite that passes is only meaningful if it is capable of failing.
 * This applies a set of deliberate, surgical mutations to the C++ port, rebuilds
 * it, re-runs the corpus, and asserts that the fixtures which *should* break
 * actually do - and, just as importantly, that the ones which shouldn't don't.
 *
 * Every mutation is reverted afterwards, including on failure.
 *
 * Usage: node mutation-test.mjs
 * Exit code 0 when every mutation is caught exactly as expected.
 */

import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync, readdirSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..");
const healthcheckDir = join(repoRoot, "src", "healthcheck");
const runnerBuildDir = join(here, "mo2-runner", "build");
const runnerExe = join(runnerBuildDir, "Release", "healthcheck-parity-runner.exe");
const fixturesDir = join(here, "fixtures", "cases");
const mo2OutDir = join(here, "out", "mo2");
const qtBin = process.env.HEALTHCHECK_QT_BIN ?? "J:/mo2 test/Qt/6.11.1/msvc2022_64/bin";

/**
 * Each mutation names a file, an exact string to replace, its replacement, and
 * the fixtures expected to start failing. `expectFail` is exhaustive: if a
 * mutation breaks more or fewer fixtures than listed, the self-test fails,
 * because that means the corpus is either under- or over-constraining.
 */
const MUTATIONS = [
  {
    name: "tie-break: prefer the later row instead of the earlier one",
    file: "filedependencyresolver.cpp",
    from: "if (rows.at(index).position.toDouble() > rows.at(best).position.toDouble()) {",
    to: "if (rows.at(index).position.toDouble() >= rows.at(best).position.toDouble()) {",
    expectFail: ["15-position-tie"],
  },
  {
    name: "recommendation: ignore the active-category preference",
    file: "filedependencyresolver.cpp",
    from: "return highestPosition(rows, active.isEmpty() ? available : active);",
    to: "return highestPosition(rows, available);",
    expectFail: ["14-recommendation-ranking"],
  },
  {
    name: "OR: do not clear sibling recommendations when a branch is satisfied",
    file: "filedependencyresolver.cpp",
    from: "      if (anySatisfyingEnabled) {\n        for (BranchPlan& branch : defPlan.branches) {\n          branch.recRow = -1;\n        }\n      }",
    to: "      if (false && anySatisfyingEnabled) {\n        for (BranchPlan& branch : defPlan.branches) {\n          branch.recRow = -1;\n        }\n      }",
    expectFail: ["09-or-satisfied-enabled"],
  },
  {
    name: "sources: let disabled files emit their own requirements",
    file: "filedependencyresolver.cpp",
    from: "if (file.enabled && file.emitRequirements) {",
    to: "if (file.emitRequirements) {",
    expectFail: ["19-disabled-source"],
  },
  {
    name: "sources: ignore the collection-managed exclusion",
    file: "filedependencyresolver.cpp",
    from: "    if (file.enabled && file.emitRequirements) {\n      sourceUids.append(file.fileVersionUid);",
    to: "    if (file.enabled) {\n      sourceUids.append(file.fileVersionUid);",
    expectFail: ["18-collection-managed-source"],
  },
  {
    name: "availability: treat 'hidden' mods as unavailable",
    file: "filedependencyresolver.cpp",
    from: 'return status == QLatin1String("published") || status == QLatin1String("hidden");',
    to: 'return status == QLatin1String("published");',
    expectFail: ["17-hidden-status-allowed"],
  },
  {
    name: "mapper: surface deliberately disabled versions instead of dropping them",
    file: "requirementsreportmapper.cpp",
    from: "      // Owned-but-disabled with nothing wrong enabled is a deliberate choice.\n      return std::nullopt;",
    to: "      // Owned-but-disabled with nothing wrong enabled is a deliberate choice.\n      FileRequirement forced;\n      forced.kind             = RequirementKind::Missing;\n      forced.requirementDefId = dependency.definitionId;\n      return forced;",
    expectFail: ["07-disabled-deliberate"],
  },
  {
    name: "mapper: drop the Vortex self-requirement suppression",
    file: "requirementsreportmapper.cpp",
    from: "        if (modUid == VORTEX_MOD_UID) {\n          return std::nullopt;\n        }",
    to: "        if (false && modUid == VORTEX_MOD_UID) {\n          return std::nullopt;\n        }",
    expectFail: ["13-vortex-self-requirement"],
  },
  {
    name: "mapper: ignore the OR deliberately-disabled guard",
    file: "requirementsreportmapper.cpp",
    from: "        if (!branch.satisfyingDisabled.isEmpty() && branch.wrongEnabled.isEmpty()) {\n          return std::nullopt;\n        }",
    to: "        if (false) {\n          return std::nullopt;\n        }",
    expectFail: ["11-or-disabled-deliberate"],
  },
];

const run = (cmd, args, opts = {}) =>
  execFileSync(cmd, args, { encoding: "utf8", stdio: "pipe", ...opts });

function rebuild() {
  run("cmake", ["--build", runnerBuildDir, "--config", "Release", "--parallel", "16"]);
}

function runCorpus() {
  mkdirSync(mo2OutDir, { recursive: true });
  const env = { ...process.env, PATH: `${qtBin};${process.env.PATH}` };
  for (const file of readdirSync(fixturesDir).filter((f) => f.endsWith(".json"))) {
    const name = file.replace(/\.json$/, "");
    const out = run(runnerExe, [join(fixturesDir, file)], { env });
    writeFileSync(join(mo2OutDir, `${name}.json`), out, "utf8");
  }
}

/** Fixture names that currently differ between the two implementations. */
function failingFixtures() {
  try {
    run("node", [join(here, "compare.mjs")]);
    return [];
  } catch (err) {
    const output = `${err.stdout ?? ""}`;
    return [...output.matchAll(/^FAIL (\S+)/gm)].map((m) => m[1]);
  }
}

const arrayEq = (a, b) =>
  a.length === b.length && [...a].sort().every((v, i) => v === [...b].sort()[i]);

let problems = 0;

console.log("Establishing clean baseline...");
rebuild();
runCorpus();
const baseline = failingFixtures();
if (baseline.length > 0) {
  console.error(
    `Baseline is not clean - ${baseline.length} fixture(s) already differ: ${baseline.join(", ")}`,
  );
  console.error("Fix parity before running the mutation self-test.");
  process.exit(2);
}
console.log("Baseline clean: all fixtures match.\n");

for (const mutation of MUTATIONS) {
  const path = join(healthcheckDir, mutation.file);
  const original = readFileSync(path, "utf8");

  if (!original.includes(mutation.from)) {
    console.log(`SKIP  ${mutation.name}`);
    console.log(`        anchor text not found in ${mutation.file}; update the mutation`);
    problems += 1;
    continue;
  }

  writeFileSync(path, original.replace(mutation.from, mutation.to), "utf8");

  try {
    rebuild();
    runCorpus();
    const failing = failingFixtures();

    if (arrayEq(failing, mutation.expectFail)) {
      console.log(`CAUGHT ${mutation.name}`);
      console.log(`        detected by: ${failing.join(", ")}`);
    } else {
      problems += 1;
      console.log(`MISSED ${mutation.name}`);
      console.log(`        expected failures: ${mutation.expectFail.join(", ") || "(none)"}`);
      console.log(`        actual failures:   ${failing.join(", ") || "(none)"}`);
    }
  } catch (err) {
    problems += 1;
    console.log(`ERROR  ${mutation.name}`);
    console.log(`        ${err.message.split("\n")[0]}`);
  } finally {
    writeFileSync(path, original, "utf8");
  }
}

console.log("\nRestoring clean build...");
rebuild();
runCorpus();
const restored = failingFixtures();
if (restored.length > 0) {
  console.error(`Restore failed - fixtures still differ: ${restored.join(", ")}`);
  process.exit(2);
}

if (problems === 0) {
  console.log(
    `\nAll ${MUTATIONS.length} mutations were caught by exactly the expected fixtures.`,
  );
  process.exit(0);
}

console.log(`\n${problems} mutation(s) were not caught as expected.`);
process.exit(1);
