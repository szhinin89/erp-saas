import { randomBytes } from 'node:crypto';

/** @returns {string} */
export function createRunCorrelationId() {
  const suffix = randomBytes(4).toString('hex');
  const ts = Date.now().toString(36);
  return `qa-${ts}-${suffix}`;
}

/** @param {string} runId @param {string} testId */
export function createTestCorrelationId(runId, testId) {
  return `${runId}:${testId}`;
}
