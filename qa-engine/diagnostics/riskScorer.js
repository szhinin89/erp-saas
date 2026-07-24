import { BASE_RISK_SCORES, productionRiskLabel } from './types.js';

/** @typedef {import('./types.js').FailureType} FailureType */

/**
 * @param {FailureType} failureType
 * @param {import('../core/runner.js').QaTestResult} test
 * @param {{ behaviorChanges?: Array<{ test_id: string; severity: string }> }} [context]
 */
export function scoreRisk(failureType, test, context = {}) {
  let risk_score = BASE_RISK_SCORES[failureType] ?? BASE_RISK_SCORES.UNKNOWN;

  if (test.critical) {
    risk_score = Math.min(100, risk_score + 5);
  }

  const criticalBehavior = (context.behaviorChanges ?? []).filter(
    (c) => c.test_id === test.id && c.severity === 'CRITICAL',
  );
  if (criticalBehavior.length > 0) {
    risk_score = Math.min(100, risk_score + 10);
  }

  if (failureType === 'INFRA_FAILURE') {
    risk_score = Math.max(20, risk_score - 10);
  }

  return {
    risk_score,
    production_risk: productionRiskLabel(risk_score),
  };
}

/**
 * @param {Array<{ failure_type: FailureType; risk_score: number }>} enrichedFailures
 */
export function summarizeRisk(enrichedFailures) {
  if (enrichedFailures.length === 0) {
    return { max_risk_score: 0, production_risk: 'LOW', block_ci: false, critical_types: [] };
  }

  const max_risk_score = Math.max(...enrichedFailures.map((f) => f.risk_score));
  const critical_types = [...new Set(
    enrichedFailures
      .filter((f) => f.failure_type === 'AUTH_BYPASS' || f.failure_type === 'TENANT_ISOLATION_LEAK')
      .map((f) => f.failure_type),
  )];

  const block_ci =
    max_risk_score >= 90 ||
    critical_types.includes('AUTH_BYPASS') ||
    critical_types.includes('TENANT_ISOLATION_LEAK');

  return {
    max_risk_score,
    production_risk: productionRiskLabel(max_risk_score),
    block_ci,
    critical_types,
  };
}
