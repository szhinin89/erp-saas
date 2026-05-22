#!/usr/bin/env node
/**
 * ERP QA Engine — black-box regression runner (CI/dev only).
 * Orchestrates: readiness → deterministic reset/seed → suites → regression diff → reports.
 */

import { BehaviorCollector } from './core/behavior.js';
import { loadConfig } from './core/config.js';
import { createRunCorrelationId } from './core/correlation.js';
import { QaLogger } from './core/logger.js';
import { waitForApiReady, waitForFrontendReady } from './core/readiness.js';
import { QaRunner } from './core/runner.js';
import { buildCoverageMap } from './coverage/map.js';
import { computeRegressionDiff } from './diff/compare.js';
import { writeReports } from './reporting/writer.js';
import { runSeedEngine } from './seed/seedEngine.js';
import { runAuthSuite } from './suites/auth/authSuite.js';
import { runCacheSuite } from './suites/cache/cacheSuite.js';
import { runEndpointsSuite } from './suites/endpoints/endpointsSuite.js';
import { runInfraSuite } from './suites/infra/infraSuite.js';
import { runPermissionsSuite } from './suites/permissions/permissionsSuite.js';
import { runSecuritySuite } from './suites/security/securitySuite.js';
import { runTenantSuite } from './suites/tenant/tenantSuite.js';

/** @param {ReturnType<typeof loadConfig>} config */
export async function runQaEngine(config) {
  const runCorrelationId = createRunCorrelationId();
  const logger = new QaLogger(runCorrelationId);
  const behavior = new BehaviorCollector();
  /** @type {Array<{ phase: string; at: string; detail?: object }>} */
  const timeline = [];

  const mark = (phase, detail) => {
    timeline.push({ phase, at: new Date().toISOString(), detail });
    logger.info(phase, detail);
  };

  mark('engine.start', { api: config.api, frontend: config.frontend, reportsDir: config.reportsDir });

  mark('readiness.api.start');
  const apiReady = await waitForApiReady(config.api, { timeoutMs: config.readinessTimeoutMs });
  mark('readiness.api.ready', apiReady);

  mark('readiness.frontend.start');
  const frontReady = await waitForFrontendReady(config.frontend, { timeoutMs: config.readinessTimeoutMs });
  mark('readiness.frontend.ready', frontReady);

  mark('seed.start');
  const seed = await runSeedEngine(config.api, {
    skipDbReset: config.skipDbReset,
    skipRedisFlush: config.skipRedisFlush,
    logger: logger.child('seed'),
  });
  mark('seed.complete', {
    tenantACompanyId: seed.tenantACompanyId,
    tenantBCompanyId: seed.tenantBCompanyId,
  });

  const runner = new QaRunner({
    runCorrelationId,
    logger,
    behavior,
    infraRetryCount: config.infraRetryCount,
    testTimeoutMs: config.suiteTimeoutMs,
  });

  const runSuite = async (name, fn) => {
    mark(`suite.${name}.start`);
    await fn();
    mark(`suite.${name}.complete`, {
      passed: runner.results.filter((r) => r.suite === name && r.status === 'PASS').length,
      failed: runner.results.filter((r) => r.suite === name && r.status === 'FAIL').length,
    });
  };

  await runSuite('infra', () => runInfraSuite(runner, config.api, config.frontend, seed));
  await runSuite('auth', () => runAuthSuite(runner, config.api, seed));
  await runSuite('tenant', () => runTenantSuite(runner, config.api, seed));
  await runSuite('permissions', () => runPermissionsSuite(runner, config.api, seed));
  await runSuite('cache', () => runCacheSuite(runner, config.api, seed));
  await runSuite('security', () => runSecuritySuite(runner, config.api, seed));
  await runSuite('endpoints', () => runEndpointsSuite(runner, config.api, seed));

  mark('regression.diff.start');
  const diff = computeRegressionDiff(
    config.previousRunPath,
    config.previousBehaviorPath,
    runner.results,
    behavior,
    config.reportsDir,
  );
  mark('regression.diff.complete', diff.summary);

  const coverage = buildCoverageMap(runner.results);
  mark('coverage.map.complete', coverage.summary);

  const report = writeReports(runner.results, {
    correlationId: runCorrelationId,
    seed: 'manifest-v1',
    diff,
    coverage,
    behavior: behavior.toJSON(),
    environment: { api: config.api, frontend: config.frontend },
    timeline,
    logs: logger.entries,
  }, config.reportsDir);

  mark('engine.complete', { status: report.status, summary: report.summary });

  if (diff.behaviorChanges?.length) {
    logger.warn('Behavioral changes detected', { count: diff.behaviorChanges.length });
  }

  return report;
}

async function main() {
  const config = loadConfig();
  const report = await runQaEngine(config);
  if (report.status === 'FAILED') {
    process.exit(1);
  }
}

const isDirectRun = process.argv[1]?.endsWith('run.js');
if (isDirectRun) {
  main().catch((err) => {
    console.error(JSON.stringify({ level: 'error', message: err instanceof Error ? err.message : String(err) }));
    process.exit(1);
  });
}
