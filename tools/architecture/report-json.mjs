#!/usr/bin/env node
import path from 'node:path';
import { runAllChecks } from './run-all.mjs';
import { calculateArchitectureScore } from './calculate-score.mjs';
import { toJsonReport, writeJsonReport } from './shared/report-utils.mjs';
import { ARCH_DIR } from './shared/fs-utils.mjs';
import { loadArchitectureBaseline, compareArchitectureBaseline } from './ratchet.mjs';

const results = runAllChecks({ silent: true });
const score = calculateArchitectureScore(results);
let ratchet;
try {
  ratchet = compareArchitectureBaseline(results, loadArchitectureBaseline());
} catch (error) {
  console.error(`Architecture baseline invalid (fail closed): ${error.message}`);
  process.exit(1);
}
const report = toJsonReport(results, score, ratchet);

const outArg = process.argv.indexOf('--out');
const outPath =
  outArg >= 0 && process.argv[outArg + 1]
    ? path.resolve(process.argv[outArg + 1])
    : path.join(ARCH_DIR, 'architecture-report.json');

writeJsonReport(report, outPath);
console.log(
  `Wrote ${outPath} — ${ratchet.newViolationCount} new, ${ratchet.resolvedViolationCount} resolved, ${ratchet.baselineViolations} baseline violation(s); score ${report.architectureScore}/100 (${report.status}).`,
);
process.exit(ratchet.passed ? 0 : 1);
