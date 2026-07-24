import fs from 'node:fs';
import { classifyFailure } from './failureClassifier.js';
import { explainFailure } from './explainer.js';
import { detectFlakiness } from './flakinessDetector.js';
import { inferRootCause } from './rootCauseEngine.js';
import { scoreRisk, summarizeRisk } from './riskScorer.js';
import { analyzeTrends } from './trendAnalyzer.js';

/**
 * Run full diagnostics pipeline on QA results.
 * @param {object} input
 * @param {import('../core/runner.js').QaTestResult[]} input.results
 * @param {object} input.diff
 * @param {string} input.previousRunPath
 * @param {string} input.previousBehaviorPath
 * @param {object} [input.trendsFromDiff]
 */
export function runDiagnostics(input) {
  const { results, diff, previousRunPath, previousBehaviorPath } = input;

  let previousRun = null;
  let previousBehavior = null;

  if (fs.existsSync(previousRunPath)) {
    try {
      previousRun = JSON.parse(fs.readFileSync(previousRunPath, 'utf8'));
    } catch {
      previousRun = null;
    }
  }

  if (fs.existsSync(previousBehaviorPath)) {
    try {
      previousBehavior = JSON.parse(fs.readFileSync(previousBehaviorPath, 'utf8'));
    } catch {
      previousBehavior = null;
    }
  }

  const trends = analyzeTrends(results, previousRun, diff, previousBehavior);
  const flakiness = detectFlakiness(results, previousRun, diff);

  const failedTests = results.filter((r) => r.status === 'FAIL');
  /** @type {Array<object>} */
  const enrichedFailures = [];

  for (const test of failedTests) {
    const { failure_type, signals } = classifyFailure(test, diff.behaviorChanges ?? []);
    const rootCause = inferRootCause(test, failure_type, { diff });
    const risk = scoreRisk(failure_type, test, { behaviorChanges: diff.behaviorChanges });
    const suiteFlake = flakiness.bySuite[test.suite];
    const testFlake = flakiness.perTest.find((t) => t.test_id === test.id);

    const trendImpact =
      trends.recurring_failures.includes(test.id)
        ? 'Recurring failure across runs — not a one-off regression'
        : trends.trend === 'DEGRADING'
          ? 'Part of degrading quality trend vs previous run'
          : null;

    const diagnostic = {
      test: test.id,
      name: test.name,
      suite: test.suite,
      status: test.status,
      failure_type,
      signals,
      correlation_id: test.correlation_id,
      root_cause: rootCause.root_cause,
      confidence: rootCause.confidence,
      related_component: rootCause.related_component,
      root_cause_evidence: rootCause.evidence,
      risk_score: risk.risk_score,
      production_risk: risk.production_risk,
      flakiness_score: testFlake?.flakiness_score ?? suiteFlake?.flakiness_score ?? 0,
      instability: testFlake?.instability ?? suiteFlake?.instability ?? 'STABLE',
      trend_impact: trendImpact,
      explanation: '',
    };

    diagnostic.explanation = explainFailure(diagnostic, test);
    enrichedFailures.push(diagnostic);
  }

  const riskSummary = summarizeRisk(enrichedFailures);

  const testsWithDiagnostics = results.map((test) => {
    const diag = enrichedFailures.find((d) => d.test === test.id);
    if (!diag) return test;
    return {
      ...test,
      failure_type: diag.failure_type,
      root_cause: diag.root_cause,
      root_cause_confidence: diag.confidence,
      related_component: diag.related_component,
      risk_score: diag.risk_score,
      production_risk: diag.production_risk,
      flakiness_score: diag.flakiness_score,
      instability: diag.instability,
      explanation: diag.explanation,
      trend_impact: diag.trend_impact,
    };
  });

  return {
    failures: enrichedFailures,
    tests: testsWithDiagnostics,
    risk: riskSummary,
    trends,
    flakiness,
    summary: {
      failed_count: enrichedFailures.length,
      by_type: countBy(enrichedFailures, (f) => f.failure_type),
      block_ci: riskSummary.block_ci,
    },
  };
}

/** @template T @param {T[]} arr @param {(item: T) => string} fn */
function countBy(arr, fn) {
  return arr.reduce((acc, item) => {
    const key = fn(item);
    acc[key] = (acc[key] ?? 0) + 1;
    return acc;
  }, /** @type {Record<string, number>} */ ({}));
}
