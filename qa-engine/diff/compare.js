import fs from 'node:fs';
import path from 'node:path';
import { diffBehaviorSnapshots } from '../core/behavior.js';

/**
 * Compare previous and current test runs (status + behavioral V2).
 * @param {string} previousPath
 * @param {string} previousBehaviorPath
 * @param {Array<{ id: string; status: string; suite?: string; error?: string; behavior?: Record<string, unknown> }>} currentTests
 * @param {import('../core/behavior.js').BehaviorCollector} behaviorCollector
 * @param {string} reportsDir
 */
export function computeRegressionDiff(
  previousPath,
  previousBehaviorPath,
  currentTests,
  behaviorCollector,
  reportsDir,
) {
  /** @type {Array<{ id: string; suite?: string; error?: string }>} */
  const newFailures = [];
  /** @type {string[]} */
  const fixed = [];
  /** @type {Array<{ test_id: string; change_type: string; field: string; old: unknown; new: unknown; severity: string }>} */
  const behaviorChanges = [];

  let hasBaseline = false;
  /** @type {Record<string, { status?: string; behavior?: Record<string, unknown> }>} */
  let prevMap = new Map();

  if (fs.existsSync(previousPath)) {
    try {
      const previous = JSON.parse(fs.readFileSync(previousPath, 'utf8'));
      prevMap = new Map(
        (previous.tests ?? previous.results ?? []).map((t) => [t.id ?? t.name, t]),
      );
      hasBaseline = true;
    } catch {
      hasBaseline = false;
    }
  }

  /** @type {Record<string, Record<string, unknown>>} */
  let prevBehaviorByTest = {};
  if (fs.existsSync(previousBehaviorPath)) {
    try {
      const prevBehavior = JSON.parse(fs.readFileSync(previousBehaviorPath, 'utf8'));
      prevBehaviorByTest = prevBehavior.byTestId ?? prevBehavior;
      hasBaseline = true;
    } catch {
      // ignore
    }
  }

  for (const cur of currentTests) {
    const key = cur.id;
    const prev = prevMap.get(key);

    if (cur.status === 'FAIL' && (!prev || prev.status === 'PASS')) {
      newFailures.push({ id: key, suite: cur.suite, error: cur.error });
    }
    if (cur.status === 'PASS' && prev?.status === 'FAIL') {
      fixed.push(key);
    }

    const prevSnap = prevBehaviorByTest[key] ?? prev?.behavior;
    const curSnap = cur.behavior ?? behaviorCollector.byTestId[key];
    behaviorChanges.push(...diffBehaviorSnapshots(prevSnap, curSnap, key));
  }

  const criticalBehaviorChanges = behaviorChanges.filter((c) => c.severity === 'CRITICAL');

  /** @type {string[]} */
  const recurringFailures = [];
  for (const cur of currentTests) {
    const prev = prevMap.get(cur.id);
    if (cur.status === 'FAIL' && prev?.status === 'FAIL') {
      recurringFailures.push(cur.id);
    }
  }

  const currentFailRate = currentTests.length
    ? currentTests.filter((t) => t.status === 'FAIL').length / currentTests.length
    : 0;
  const prevFailRate = prevMap.size
    ? [...prevMap.values()].filter((t) => t.status === 'FAIL').length / prevMap.size
    : 0;

  const diffPayload = {
    timestamp: new Date().toISOString(),
    version: 2,
    hasBaseline,
    newFailures,
    fixed,
    behaviorChanges,
    criticalBehaviorChanges: criticalBehaviorChanges.length,
    recurringFailures,
    trendHints: {
      failure_rate_delta: Math.round((currentFailRate - prevFailRate) * 1000) / 1000,
      behavior_change_count: behaviorChanges.length,
      degrading: hasBaseline && currentFailRate > prevFailRate + 0.05,
      improving: hasBaseline && currentFailRate < prevFailRate - 0.05,
    },
    summary: {
      newFailureCount: newFailures.length,
      fixedCount: fixed.length,
      behaviorChangeCount: behaviorChanges.length,
      criticalBehaviorChangeCount: criticalBehaviorChanges.length,
      recurringFailureCount: recurringFailures.length,
    },
  };

  fs.mkdirSync(reportsDir, { recursive: true });
  fs.writeFileSync(
    path.join(reportsDir, 'regression-diff.json'),
    `${JSON.stringify(diffPayload, null, 2)}\n`,
  );

  return diffPayload;
}
