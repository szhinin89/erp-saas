#!/usr/bin/env node
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { runCheckPagesWrapper } from './check-pages-wrapper.mjs';
import { runCheckImportBoundaries } from './check-import-boundaries.mjs';
import { runCheckModuleBoundaries } from './check-module-boundaries.mjs';
import { runCheckCssPrefixes } from './check-css-prefixes.mjs';
import { runCheckDesignSystem } from './check-design-system.mjs';
import { runCheckFrontendPrecision } from './check-frontend-precision.mjs';
import { runCheckFrontendDateTime } from './check-frontend-datetime.mjs';
import { runCheckNoCrossLayer } from './check-no-cross-layer.mjs';
import { runCheckPlatformLegacySurface } from './check-platform-legacy-surface.mjs';
import { runCheckFrontendSubscriberNaming } from './check-frontend-subscriber-naming.mjs';
import { runCheckBackendLayering } from './check-backend-layering.mjs';
import { runCheckBackendCleanArchitecture } from './check-backend-clean-architecture.mjs';
import { runCheckBackendControllerThin } from './check-backend-controller-thin.mjs';
import { runCheckBackendSubscriberRules } from './check-backend-subscriber-rules.mjs';
import { runCheckPermissionsAuthorizationRules } from './check-permissions-authorization-rules.mjs';
import { runCheckFrontendPermissionsRules } from './check-frontend-permissions-rules.mjs';
import { runCheckDuplicateServices } from './check-duplicate-services.mjs';
import { runCheckNamingConventions } from './check-naming-conventions.mjs';
import { runCheckI18nKeys } from './check-i18n-keys.mjs';
import { runCheckGrandfatherIntegrity } from './check-grandfather-integrity.mjs';
import { calculateArchitectureScore } from './calculate-score.mjs';
import { emitGithubAnnotations } from './github-annotations.mjs';
import { toJsonReport, writeJsonReport } from './shared/report-utils.mjs';
import { loadArchitectureBaseline, compareArchitectureBaseline } from './ratchet.mjs';
import {
  formatConsoleCheck,
  formatConsoleSummary,
  formatRatchetCheck,
  formatRatchetSummary,
} from './formatters/console-formatter.mjs';

export const CHECKS = [
  { name: 'pages-wrapper', run: runCheckPagesWrapper },
  { name: 'import-boundaries', run: runCheckImportBoundaries },
  { name: 'module-boundaries', run: runCheckModuleBoundaries },
  { name: 'css-prefixes', run: runCheckCssPrefixes },
  { name: 'design-system', run: runCheckDesignSystem },
  { name: 'frontend-precision', run: runCheckFrontendPrecision },
  { name: 'frontend-datetime', run: runCheckFrontendDateTime },
  { name: 'no-cross-layer', run: runCheckNoCrossLayer },
  { name: 'platform-legacy-surface', run: runCheckPlatformLegacySurface },
  { name: 'frontend-subscriber-naming', run: runCheckFrontendSubscriberNaming },
  { name: 'backend-layering', run: runCheckBackendLayering },
  { name: 'backend-clean-architecture', run: runCheckBackendCleanArchitecture },
  { name: 'backend-controller-thin', run: runCheckBackendControllerThin },
  { name: 'backend-subscriber-rules', run: runCheckBackendSubscriberRules },
  { name: 'permissions-authorization-rules', run: runCheckPermissionsAuthorizationRules },
  { name: 'frontend-permissions-rules', run: runCheckFrontendPermissionsRules },
  { name: 'duplicate-services', run: runCheckDuplicateServices },
  { name: 'naming-conventions', run: runCheckNamingConventions },
  { name: 'i18n-keys', run: runCheckI18nKeys },
  { name: 'grandfather-integrity', run: runCheckGrandfatherIntegrity },
];

/**
 * @param {{ only?: string, silent?: boolean }} [opts]
 */
export function runAllChecks(opts = {}) {
  /** @type {import('./shared/report-utils.mjs').CheckResult[]} */
  const results = [];
  for (const check of CHECKS) {
    if (opts.only && check.name !== opts.only) continue;
    const result = check.run();
    results.push(result);
    if (!opts.silent) {
      for (const line of formatConsoleCheck(result)) {
        console.log(line);
      }
    }
  }
  return results;
}

const isMain =
  process.argv[1] &&
  path.resolve(fileURLToPath(import.meta.url)) === path.resolve(process.argv[1]);

if (isMain) {
  const args = new Set(process.argv.slice(2));
  const jsonOut = args.has('--json');
  const annotate = args.has('--annotate') || process.env.GITHUB_ACTIONS === 'true';
  const only = args.has('--only') ? process.argv[process.argv.indexOf('--only') + 1] : null;

  const results = runAllChecks({ only, silent: true });
  const score = calculateArchitectureScore(results);
  let ratchet = null;
  if (!only) {
    try {
      ratchet = compareArchitectureBaseline(results, loadArchitectureBaseline());
    } catch (error) {
      console.error(`Architecture baseline invalid (fail closed): ${error.message}`);
      process.exit(1);
    }
  }
  const report = toJsonReport(results, score, ratchet);
  writeJsonReport(report);

  if (annotate) {
    emitGithubAnnotations(results, ratchet ? { newViolations: ratchet.newViolations } : {});
  }

  if (jsonOut) {
    console.log(JSON.stringify(report, null, 2));
    process.exit(report.passed ? 0 : 1);
  }

  const formatted = ratchet
    ? [...results.flatMap((result) => formatRatchetCheck(result, ratchet)), ...formatRatchetSummary(ratchet, score)]
    : [...results.flatMap(formatConsoleCheck), ...formatConsoleSummary(results, score)];
  for (const line of formatted) {
    console.log(line);
  }

  const shouldFail = ratchet
    ? !ratchet.passed
    : results.some((result) => result.violations.length > 0);
  process.exit(shouldFail ? 1 : 0);
}
