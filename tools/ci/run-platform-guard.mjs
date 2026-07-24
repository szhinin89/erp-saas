#!/usr/bin/env node
/**
 * Platform Control Plane CI hard-gates — single orchestrator (fail-fast).
 * Generates docs/ci/PLATFORM_GUARD_REPORT.md
 */
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { REPO_ROOT, writeGuardReport } from './platform-guard-lib.mjs';
import { runForbiddenPatternScan } from './scan-forbidden-patterns.mjs';
import { runValidatePlatformImports } from './validate-platform-imports.mjs';
import { runValidateFrontendRoutes } from './validate-frontend-routes.mjs';
import { runValidateApiEndpoints } from './validate-api-endpoints.mjs';
import { runValidateSubscriberApiSurface } from './validate-subscriber-api-surface.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPORT_PATH = path.join(REPO_ROOT, 'docs/ci/PLATFORM_GUARD_REPORT.md');

const CHECKS = [
  runForbiddenPatternScan,
  runValidatePlatformImports,
  runValidateFrontendRoutes,
  runValidateApiEndpoints,
  runValidateSubscriberApiSurface,
];

export function runPlatformGuard() {
  /** @type {ReturnType<typeof runForbiddenPatternScan>[]} */
  const results = CHECKS.map((run) => run());
  const passed = results.every((r) => r.passed);
  const endpointResult = results.find((r) => r.name === 'api-endpoints');

  writeGuardReport(REPORT_PATH, {
    status: passed ? 'PASS' : 'FAIL',
    checks: results,
    endpoints: endpointResult?.endpoints ?? null,
  });

  return { passed, results, reportPath: REPORT_PATH };
}

const isMain =
  process.argv[1] &&
  path.resolve(fileURLToPath(import.meta.url)) === path.resolve(process.argv[1]);

if (isMain) {
  const { passed, results, reportPath } = runPlatformGuard();

  console.log(`Platform guard report: ${path.relative(REPO_ROOT, reportPath)}`);
  for (const r of results) {
    const tag = r.passed ? 'PASS' : 'FAIL';
    console.log(`  [${tag}] ${r.name} (${r.violations.length} violations) — ${r.detail ?? ''}`);
  }

  if (!passed) {
    console.error('\nPlatform Control Plane guard FAILED (fail-fast).');
    for (const r of results.filter((x) => !x.passed)) {
      for (const v of r.violations.slice(0, 20)) {
        const loc = v.line != null ? `${v.file}:${v.line}` : v.file;
        console.error(`  [${r.name}/${v.rule}] ${loc} — ${v.message}`);
      }
      if (r.violations.length > 20) {
        console.error(`  … +${r.violations.length - 20} more (${r.name})`);
      }
    }
    process.exit(1);
  }

  console.log('\nPlatform Control Plane guard OK');
}
