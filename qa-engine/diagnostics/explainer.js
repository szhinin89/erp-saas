/** @typedef {import('./types.js').FailureType} FailureType */

/**
 * @param {object} diagnostic
 * @param {import('../core/runner.js').QaTestResult} test
 */
export function explainFailure(diagnostic, test) {
  const lines = [
    'FAILURE EXPLANATION:',
    '',
    summarizeWhatHappened(diagnostic.failure_type, test),
    '',
    'Root cause:',
    ...diagnostic.root_cause_evidence.map((e) => `- ${e}`),
    '',
    'Impact:',
    ...impactLines(diagnostic.failure_type),
    '',
    'Severity:',
    `- ${diagnostic.production_risk} (risk score ${diagnostic.risk_score}, confidence ${Math.round(diagnostic.confidence * 100)}%)`,
  ];

  if (diagnostic.trend_impact) {
    lines.push('', 'Trend:', `- ${diagnostic.trend_impact}`);
  }

  return lines.join('\n');
}

/** @param {FailureType} type @param {import('../core/runner.js').QaTestResult} test */
function summarizeWhatHappened(type, test) {
  const templates = {
    AUTH_BYPASS: `Authentication or authorization boundary failed in test "${test.name}". ${test.error ?? ''}`.trim(),
    TENANT_ISOLATION_LEAK: `Multi-tenant isolation failed in test "${test.name}". ${test.error ?? ''}`.trim(),
    PERMISSION_DRIFT: `The permission system returned unexpected results in test "${test.name}". ${test.error ?? ''}`.trim(),
    CACHE_INCONSISTENCY: `Cache layer inconsistency detected in test "${test.name}". ${test.error ?? ''}`.trim(),
    API_BREAKING_CHANGE: `API contract or availability regression in test "${test.name}". ${test.error ?? ''}`.trim(),
    VALIDATION_ERROR: `Unexpected validation rejection in test "${test.name}". ${test.error ?? ''}`.trim(),
    INFRA_FAILURE: `Infrastructure readiness or health failure in test "${test.name}". ${test.error ?? ''}`.trim(),
    UNKNOWN: `Unclassified failure in test "${test.name}". ${test.error ?? ''}`.trim(),
  };
  return templates[type] ?? templates.UNKNOWN;
}

/** @param {FailureType} type */
function impactLines(type) {
  const impacts = {
    AUTH_BYPASS: [
      '- Attackers may access protected APIs without valid session credentials',
      '- Session and bootstrap token boundaries may be broken',
    ],
    TENANT_ISOLATION_LEAK: [
      '- Tenant A users may read or mutate Tenant B data',
      '- SaaS data isolation guarantee is violated',
    ],
    PERMISSION_DRIFT: [
      '- Users may retain outdated permissions after role or profile changes',
      '- UI and runtime authorization may diverge',
    ],
    CACHE_INCONSISTENCY: [
      '- Stale cached permissions or metrics may cause incorrect authorization decisions',
      '- Non-deterministic behavior under load or after mutations',
    ],
    API_BREAKING_CHANGE: [
      '- Clients relying on current API contract may break in production',
      '- Critical endpoints may be unavailable (5xx)',
    ],
    VALIDATION_ERROR: [
      '- Legitimate QA payloads rejected — contract drift in validators',
    ],
    INFRA_FAILURE: [
      '- CI environment unstable — may not reflect product defect',
      '- Re-run recommended before blocking release',
    ],
    UNKNOWN: [
      '- Impact requires manual triage from execution-trace.json',
    ],
  };
  return impacts[type] ?? impacts.UNKNOWN;
}

/**
 * @param {Array<object>} diagnostics
 */
export function buildDiagnosticsSummaryMarkdown(diagnostics, riskSummary, trends, flakiness) {
  const lines = [
    '## Diagnostics summary',
    '',
    `**Max production risk:** ${riskSummary.production_risk} (score ${riskSummary.max_risk_score})`,
    `**Overall trend:** ${trends.trend}`,
    `**Unstable tests:** ${flakiness.summary.unstable_tests}`,
    '',
  ];

  if (riskSummary.critical_types.length) {
    lines.push('### Critical failure types', '');
    for (const t of riskSummary.critical_types) {
      lines.push(`- **${t}** — CI block triggered`);
    }
    lines.push('');
  }

  const byType = groupBy(diagnostics, (d) => d.failure_type);
  const typeOrder = ['AUTH_BYPASS', 'TENANT_ISOLATION_LEAK', 'PERMISSION_DRIFT', 'API_BREAKING_CHANGE', 'CACHE_INCONSISTENCY', 'INFRA_FAILURE', 'VALIDATION_ERROR', 'UNKNOWN'];

  lines.push('### Failures by type', '');
  for (const type of typeOrder) {
    const items = byType[type];
    if (!items?.length) continue;
    lines.push(`#### ${type}`, '');
    for (const d of items.sort((a, b) => b.risk_score - a.risk_score)) {
      lines.push(`- **${d.test}** (${d.suite}) — risk ${d.risk_score} (${d.production_risk})`);
      lines.push(`  - ${d.root_cause}`);
    }
    lines.push('');
  }

  if (trends.recurring_failures?.length) {
    lines.push('### Recurring failures', '');
    for (const id of trends.recurring_failures) {
      lines.push(`- ${id}`);
    }
    lines.push('');
  }

  return lines.join('\n');
}

/** @template T @param {T[]} arr @param {(item: T) => string} fn */
function groupBy(arr, fn) {
  return arr.reduce((acc, item) => {
    const key = fn(item);
    (acc[key] ??= []).push(item);
    return acc;
  }, /** @type {Record<string, T[]>} */ ({}));
}
