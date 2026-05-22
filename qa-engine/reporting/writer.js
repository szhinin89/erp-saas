import fs from 'node:fs';
import path from 'node:path';
import { buildCoverageMap } from '../coverage/map.js';
import { buildDiagnosticsSummaryMarkdown } from '../diagnostics/explainer.js';

/**
 * @param {import('../core/runner.js').QaTestResult[]} results
 * @param {object} meta
 * @param {string} reportsDir
 */
export function writeReports(results, meta, reportsDir) {
  fs.mkdirSync(reportsDir, { recursive: true });

  const tests = meta.diagnostics?.tests ?? results;
  const criticalFailures = tests.filter((r) => r.critical && r.status === 'FAIL');
  const criticalBehaviorChanges = meta.diff?.criticalBehaviorChanges ?? 0;
  const diagnostics = meta.diagnostics;
  const riskSummary = diagnostics?.risk;

  const hasCriticalRegression =
    criticalFailures.length > 0 ||
    criticalBehaviorChanges > 0 ||
    riskSummary?.block_ci === true;

  const payload = {
    status: hasCriticalRegression ? 'FAILED' : 'PASSED',
    timestamp: new Date().toISOString(),
    correlation_id: meta.correlationId,
    seed: meta.seed,
    environment: meta.environment ?? null,
    reset: meta.reset ?? null,
    summary: {
      total: tests.length,
      passed: tests.filter((r) => r.status === 'PASS').length,
      failed: tests.filter((r) => r.status === 'FAIL').length,
      criticalFailures: criticalFailures.length,
      criticalBehaviorChanges,
      max_risk_score: riskSummary?.max_risk_score ?? 0,
      production_risk: riskSummary?.production_risk ?? 'LOW',
      block_ci: riskSummary?.block_ci ?? false,
    },
    diagnostics: diagnostics
      ? {
          failures: diagnostics.failures,
          risk: diagnostics.risk,
          trends: diagnostics.trends,
          flakiness: diagnostics.flakiness,
          summary: diagnostics.summary,
        }
      : null,
    tests,
    regressionDiff: meta.diff,
    coverage: meta.coverage ?? null,
  };

  fs.writeFileSync(path.join(reportsDir, 'regression.json'), `${JSON.stringify(payload, null, 2)}\n`);

  const bySuite = tests.reduce((acc, t) => {
    (acc[t.suite] ??= []).push(t);
    return acc;
  }, /** @type {Record<string, typeof tests>} */ ({}));

  const md = [
    '# QA Engine Regression Report',
    '',
    `**Status:** ${payload.status}`,
    `**Correlation ID:** ${meta.correlationId ?? 'n/a'}`,
    `**Generated:** ${payload.timestamp}`,
    '',
    '## Summary',
    '',
    '| Total | Passed | Failed | Critical | Max risk | Production risk |',
    '|-------|--------|--------|----------|----------|-----------------|',
    `| ${payload.summary.total} | ${payload.summary.passed} | ${payload.summary.failed} | ${payload.summary.criticalFailures} | ${payload.summary.max_risk_score} | ${payload.summary.production_risk} |`,
    '',
  ];

  if (diagnostics) {
    md.push(
      buildDiagnosticsSummaryMarkdown(
        diagnostics.failures,
        diagnostics.risk,
        diagnostics.trends,
        diagnostics.flakiness,
      ),
    );
  }

  if (meta.coverage?.summary) {
    md.push(
      '## Coverage',
      '',
      `- Endpoints: ${meta.coverage.summary.endpoints.covered}/${meta.coverage.summary.endpoints.total} (${meta.coverage.summary.endpoints.coveragePct}%)`,
      `- Permissions: ${meta.coverage.summary.permissions.covered}/${meta.coverage.summary.permissions.total} (${meta.coverage.summary.permissions.coveragePct}%)`,
      `- Tenant flows: ${meta.coverage.summary.tenantFlows.covered}/${meta.coverage.summary.tenantFlows.total} (${meta.coverage.summary.tenantFlows.coveragePct}%)`,
      '',
    );
  }

  const failedWithDiag = (diagnostics?.failures ?? []).sort((a, b) => b.risk_score - a.risk_score);
  if (failedWithDiag.length) {
    md.push('## Critical failure explanations', '');
    for (const d of failedWithDiag) {
      md.push(`### ${d.test} — ${d.failure_type} (risk ${d.risk_score})`, '', '```', d.explanation, '```', '');
    }
  }

  if (meta.diff?.newFailures?.length) {
    md.push('## Regression diff (new failures)', '');
    for (const f of meta.diff.newFailures) {
      md.push(`- **${f.id}** (${f.suite}): ${f.error ?? 'failed'}`);
    }
    md.push('');
  }

  if (meta.diff?.behaviorChanges?.length) {
    md.push('## Behavioral changes', '');
    for (const c of meta.diff.behaviorChanges.slice(0, 20)) {
      md.push(`- **${c.test_id}** \`${c.field}\`: ${JSON.stringify(c.old)} → ${JSON.stringify(c.new)} (${c.severity})`);
    }
    if (meta.diff.behaviorChanges.length > 20) {
      md.push(`- … and ${meta.diff.behaviorChanges.length - 20} more (see regression-diff.json)`);
    }
    md.push('');
  }

  for (const [suite, suiteTests] of Object.entries(bySuite)) {
    md.push(`## Suite: ${suite}`, '');
    for (const t of suiteTests) {
      md.push(
        `- ${t.name}: **${t.status}** (${t.durationMs}ms)${t.failure_type ? ` [${t.failure_type}]` : ''}${t.error ? ` — ${t.error}` : ''}`,
      );
    }
    md.push('');
  }

  fs.writeFileSync(path.join(reportsDir, 'regression.md'), md.join('\n'));

  const executionTrace = {
    correlation_id: meta.correlationId,
    timestamp: payload.timestamp,
    timeline: meta.timeline ?? [],
    tests: tests.map((t) => ({
      test_id: t.id,
      suite_name: t.suite,
      name: t.name,
      correlation_id: t.correlation_id,
      start_time: t.start_time,
      end_time: t.end_time,
      duration_ms: t.durationMs,
      status: t.status,
      error: t.error ?? null,
      failure_type: t.failure_type ?? null,
      risk_score: t.risk_score ?? null,
      step_trace: t.step_trace,
      behavior: t.behavior ?? null,
      attempts: t.attempts ?? 1,
    })),
    logs: meta.logs ?? [],
  };

  fs.writeFileSync(
    path.join(reportsDir, 'execution-trace.json'),
    `${JSON.stringify(executionTrace, null, 2)}\n`,
  );

  if (diagnostics) {
    fs.writeFileSync(
      path.join(reportsDir, 'diagnostics-report.json'),
      `${JSON.stringify(diagnostics, null, 2)}\n`,
    );
  }

  if (meta.coverage) {
    fs.writeFileSync(
      path.join(reportsDir, 'coverage-map.json'),
      `${JSON.stringify(meta.coverage, null, 2)}\n`,
    );
  }

  fs.writeFileSync(
    path.join(reportsDir, 'previous_run.json'),
    `${JSON.stringify({ timestamp: payload.timestamp, tests }, null, 2)}\n`,
  );

  if (meta.behavior) {
    fs.writeFileSync(
      path.join(reportsDir, 'previous_behavior.json'),
      `${JSON.stringify(meta.behavior, null, 2)}\n`,
    );
  }

  return payload;
}

export { buildCoverageMap };
