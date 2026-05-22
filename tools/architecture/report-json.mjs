#!/usr/bin/env node
import path from 'node:path';
import { runAllChecks } from './run-all.mjs';
import { calculateArchitectureScore } from './calculate-score.mjs';
import { toJsonReport, writeJsonReport } from './shared/report-utils.mjs';
import { ARCH_DIR } from './shared/fs-utils.mjs';

const results = runAllChecks({ silent: true });
const score = calculateArchitectureScore(results);
const report = toJsonReport(results, score);

const outArg = process.argv.indexOf('--out');
const outPath =
  outArg >= 0 && process.argv[outArg + 1]
    ? path.resolve(process.argv[outArg + 1])
    : path.join(ARCH_DIR, 'architecture-report.json');

writeJsonReport(report, outPath);
console.log(
  `Wrote ${outPath} — score ${report.architectureScore}/100 (${report.status}), ${report.summary.violations} violation(s), ${report.summary.warnings} warning(s).`,
);
process.exit(report.passed ? 0 : 1);
