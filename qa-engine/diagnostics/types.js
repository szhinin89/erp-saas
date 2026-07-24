/** @typedef {'AUTH_BYPASS'|'TENANT_ISOLATION_LEAK'|'PERMISSION_DRIFT'|'CACHE_INCONSISTENCY'|'API_BREAKING_CHANGE'|'VALIDATION_ERROR'|'INFRA_FAILURE'|'UNKNOWN'} FailureType */

export const FAILURE_TYPES = [
  'AUTH_BYPASS',
  'TENANT_ISOLATION_LEAK',
  'PERMISSION_DRIFT',
  'CACHE_INCONSISTENCY',
  'API_BREAKING_CHANGE',
  'VALIDATION_ERROR',
  'INFRA_FAILURE',
  'UNKNOWN',
];

/** @type {Record<FailureType, number>} */
export const BASE_RISK_SCORES = {
  AUTH_BYPASS: 100,
  TENANT_ISOLATION_LEAK: 100,
  PERMISSION_DRIFT: 80,
  CACHE_INCONSISTENCY: 60,
  API_BREAKING_CHANGE: 70,
  VALIDATION_ERROR: 40,
  INFRA_FAILURE: 30,
  UNKNOWN: 50,
};

/** @param {number} score */
export function productionRiskLabel(score) {
  if (score >= 90) return 'CRITICAL';
  if (score >= 70) return 'HIGH';
  if (score >= 45) return 'MEDIUM';
  return 'LOW';
}

/** @param {number} score */
export function instabilityLabel(score) {
  if (score >= 0.5) return 'HIGH';
  if (score >= 0.25) return 'MEDIUM';
  if (score > 0) return 'LOW';
  return 'STABLE';
}

/** @param {string} haystack @param {string[]} needles */
export function matchesAny(haystack, needles) {
  const lower = haystack.toLowerCase();
  return needles.some((n) => lower.includes(n.toLowerCase()));
}
