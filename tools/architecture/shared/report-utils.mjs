import fs from 'node:fs';
import path from 'node:path';
import { ARCH_DIR, loadGrandfather, loadConfig } from './fs-utils.mjs';

/** @typedef {{ rule: string, file: string, message: string, line?: number }} Violation */

/** @typedef {{ name: string, violations: Violation[], warnings: Violation[] }} CheckResult */

/** @param {string} name @returns {CheckResult} */
export function createCheckResult(name) {
  return { name, violations: [], warnings: [] };
}

/** @param {CheckResult} result @param {Violation} v */
export function addViolation(result, v) {
  result.violations.push(v);
}

/** @param {CheckResult} result @param {Violation} v */
export function addWarning(result, v) {
  result.warnings.push(v);
}

/** @param {CheckResult} result */
export function printCheckResult(result) {
  const lines = [];
  if (result.violations.length === 0 && result.warnings.length === 0) {
    console.log(`[PASS] ${result.name}`);
    return true;
  }
  if (result.violations.length === 0) {
    console.log(`[WARN] ${result.name}`);
  } else {
    console.log(`[FAIL] ${result.name}`);
  }
  for (const v of result.violations) {
    const loc = v.line != null ? `:${v.line}` : '';
    console.log(`  - ${v.file}${loc}`);
    console.log(`    ${v.rule}: ${v.message}`);
  }
  for (const w of result.warnings) {
    const loc = w.line != null ? `:${w.line}` : '';
    console.log(`  ~ ${w.file}${loc}`);
    console.log(`    ${w.rule}: ${w.message}`);
  }
  return result.violations.length === 0;
}

/** @param {CheckResult[]} results */
export function printSummary(results) {
  const failed = results.filter((r) => r.violations.length > 0);
  const totalViolations = failed.reduce((n, r) => n + r.violations.length, 0);
  console.log('');
  if (failed.length === 0) {
    console.log(`Architecture checks OK (${results.length}/${results.length} passed).`);
    return 0;
  }
  console.log(`Architecture checks FAILED: ${failed.length} check(s), ${totalViolations} violation(s).`);
  for (const r of failed) {
    console.log(`  ✗ ${r.name} (${r.violations.length})`);
  }
  return 1;
}

/** @param {CheckResult[]} results @param {object} [score] */
export function toJsonReport(results, score) {
  const failed = results.filter((r) => r.violations.length > 0);
  const warnings = results.reduce((n, r) => n + r.warnings.length, 0);
  const report = {
    generatedAt: new Date().toISOString(),
    passed: failed.length === 0,
    architectureScore: score?.architectureScore ?? null,
    status: score?.status ?? (failed.length === 0 ? 'healthy' : 'critical'),
    driftRisk: score?.driftRisk ?? 'unknown',
    summary: {
      checks: results.length,
      checksPassed: results.length - failed.length,
      checksFailed: failed.length,
      violations: failed.reduce((n, r) => n + r.violations.length, 0),
      warnings,
    },
    checks: results.map((r) => ({
      name: r.name,
      passed: r.violations.length === 0,
      violations: r.violations,
      warnings: r.warnings,
    })),
    modules: score?.modules ?? {},
    adrs: score?.adrs ?? [],
  };
  return report;
}

/** @param {object} report @param {string} [outPath] */
export function writeJsonReport(report, outPath) {
  const target = outPath ?? path.join(ARCH_DIR, 'architecture-report.json');
  fs.writeFileSync(target, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
  return target;
}

/** @param {CheckResult[]} results */
export function collectAllFindings(results) {
  /** @type {Violation[]} */
  const violations = [];
  /** @type {Violation[]} */
  const warnings = [];
  for (const r of results) {
    violations.push(...r.violations);
    warnings.push(...r.warnings);
  }
  return { violations, warnings };
}
