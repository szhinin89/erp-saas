import { instabilityLabel } from './types.js';

/**
 * @param {import('../core/runner.js').QaTestResult[]} results
 * @param {object|null} previousRun
 * @param {object} diff
 */
export function detectFlakiness(results, previousRun, diff) {
  const prevTests = previousRun?.tests ?? previousRun?.results ?? [];
  const prevById = new Map(prevTests.map((t) => [t.id ?? t.name, t]));

  /** @type {Array<{ test_id: string; suite: string; flakiness_score: number; instability: string; signals: string[] }>} */
  const perTest = [];

  for (const test of results) {
    const signals = [];
    let score = 0;

    if ((test.attempts ?? 1) > 1) {
      signals.push(`required ${test.attempts} attempts`);
      score += test.status === 'PASS' ? 0.25 : 0.15;
    }

    const prev = prevById.get(test.id);
    if (prev && prev.status !== test.status) {
      signals.push(`status flip ${prev.status} → ${test.status}`);
      score += 0.45;
    }

    if (test.durationMs > 10_000) {
      signals.push(`slow execution ${test.durationMs}ms`);
      score += 0.1;
    }

    if (signals.length > 0) {
      perTest.push({
        test_id: test.id,
        suite: test.suite,
        flakiness_score: Math.min(1, Math.round(score * 100) / 100),
        instability: instabilityLabel(score),
        signals,
      });
    }
  }

  const suites = [...new Set(results.map((r) => r.suite))];
  /** @type {Record<string, { flakiness_score: number; instability: string; retry_rate: number; flip_count: number }>} */
  const bySuite = {};

  for (const suite of suites) {
    const suiteTests = results.filter((r) => r.suite === suite);
    const retries = suiteTests.filter((t) => (t.attempts ?? 1) > 1).length;
    const flips = suiteTests.filter((t) => {
      const prev = prevById.get(t.id);
      return prev && prev.status !== t.status;
    }).length;

    const retryRate = suiteTests.length ? retries / suiteTests.length : 0;
    const flipRate = suiteTests.length ? flips / suiteTests.length : 0;
    const flakiness_score = Math.min(1, Math.round((retryRate * 0.4 + flipRate * 0.6) * 100) / 100);

    bySuite[suite] = {
      flakiness_score,
      instability: instabilityLabel(flakiness_score),
      retry_rate: Math.round(retryRate * 1000) / 1000,
      flip_count: flips,
    };
  }

  const recurringIntermittent = (diff.newFailures ?? []).filter((f) => {
    const prev = prevById.get(f.id);
    return prev?.status === 'FAIL';
  });

  return {
    perTest,
    bySuite,
    recurring_intermittent: recurringIntermittent,
    summary: {
      unstable_tests: perTest.filter((t) => t.flakiness_score >= 0.25).length,
      highest_risk_suite: Object.entries(bySuite).sort((a, b) => b[1].flakiness_score - a[1].flakiness_score)[0]?.[0] ?? null,
    },
  };
}
