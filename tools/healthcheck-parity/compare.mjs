/**
 * Parity comparison.
 *
 * Deep-compares the Vortex runner's output against the MO2 runner's, per
 * fixture, and reports the first difference at each path. Object key order is
 * ignored (it carries no meaning in either implementation); everything else -
 * values, array order, and the presence or absence of a property - is strict.
 *
 * Array order matters and is enforced: the resolver's grouping order is part of
 * its observable behaviour, and both implementations claim to reproduce
 * JavaScript Map insertion order.
 *
 * Usage: node compare.mjs [--vortex-dir out/vortex] [--mo2-dir out/mo2]
 * Exit code 0 when every fixture matches, 1 otherwise.
 */

import { readFileSync, readdirSync, existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));

const argValue = (flag, fallback) => {
  const i = process.argv.indexOf(flag);
  return i !== -1 && process.argv[i + 1] ? process.argv[i + 1] : fallback;
};

const vortexDir = join(here, argValue("--vortex-dir", "out/vortex"));
const mo2Dir = join(here, argValue("--mo2-dir", "out/mo2"));

/** Collect every structural difference between two parsed JSON values. */
function diff(a, b, path = "$", out = []) {
  if (a === b) {
    return out;
  }

  const typeOf = (v) => (v === null ? "null" : Array.isArray(v) ? "array" : typeof v);
  const ta = typeOf(a);
  const tb = typeOf(b);

  if (ta !== tb) {
    out.push(`${path}: type ${ta} (vortex) vs ${tb} (mo2)`);
    return out;
  }

  if (ta === "array") {
    if (a.length !== b.length) {
      out.push(`${path}: array length ${a.length} (vortex) vs ${b.length} (mo2)`);
    }
    const n = Math.min(a.length, b.length);
    for (let i = 0; i < n; i += 1) {
      diff(a[i], b[i], `${path}[${i}]`, out);
    }
    return out;
  }

  if (ta === "object") {
    const keys = new Set([...Object.keys(a), ...Object.keys(b)]);
    for (const key of [...keys].sort()) {
      const inA = Object.hasOwn(a, key);
      const inB = Object.hasOwn(b, key);
      if (inA && !inB) {
        out.push(`${path}.${key}: present in vortex (${JSON.stringify(a[key])}), absent in mo2`);
      } else if (!inA && inB) {
        out.push(`${path}.${key}: absent in vortex, present in mo2 (${JSON.stringify(b[key])})`);
      } else {
        diff(a[key], b[key], `${path}.${key}`, out);
      }
    }
    return out;
  }

  out.push(`${path}: ${JSON.stringify(a)} (vortex) vs ${JSON.stringify(b)} (mo2)`);
  return out;
}

const names = readdirSync(vortexDir)
  .filter((f) => f.endsWith(".json"))
  .map((f) => f.replace(/\.json$/, ""))
  .sort();

if (names.length === 0) {
  console.error(`No Vortex outputs found in ${vortexDir}. Run the runners first.`);
  process.exit(2);
}

let failed = 0;
let compared = 0;
const MAX_SHOWN = 12;

for (const name of names) {
  const mo2Path = join(mo2Dir, `${name}.json`);
  if (!existsSync(mo2Path)) {
    console.log(`FAIL ${name}\n       no MO2 output at ${mo2Path}`);
    failed += 1;
    continue;
  }

  const vortex = JSON.parse(readFileSync(join(vortexDir, `${name}.json`), "utf8"));
  const mo2 = JSON.parse(readFileSync(mo2Path, "utf8"));

  const differences = diff(vortex, mo2);
  compared += 1;

  if (differences.length === 0) {
    console.log(`PASS ${name}`);
  } else {
    failed += 1;
    console.log(`FAIL ${name}  (${differences.length} difference(s))`);
    for (const d of differences.slice(0, MAX_SHOWN)) {
      console.log(`       ${d}`);
    }
    if (differences.length > MAX_SHOWN) {
      console.log(`       ... and ${differences.length - MAX_SHOWN} more`);
    }
  }
}

console.log(
  `\n${compared - failed}/${compared} fixtures identical between Vortex and MO2.`,
);
process.exit(failed === 0 ? 0 : 1);
