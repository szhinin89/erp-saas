/**
 * @param {import('../core/runner.js').QaTestResult[]} results
 * @param {object|null} previousRun
 * @param {object} diff
 * @param {object|null} previousBehavior
 */
export function analyzeTrends(results, previousRun, diff, previousBehavior) {
  const prevTests = previousRun?.tests ?? previousRun?.results ?? [];
  const currentFailed = results.filter((r) => r.status === 'FAIL').length;
  const prevFailed = prevTests.filter((t) => t.status === 'FAIL').length;
  const currentTotal = results.length;
  const prevTotal = prevTests.length || currentTotal;

  const currentFailureRate = currentTotal ? currentFailed / currentTotal : 0;
  const prevFailureRate = prevTotal ? prevFailed / prevTotal : 0;
  const change_rate = Math.round(Math.abs(currentFailureRate - prevFailureRate) * 1000) / 1000;

  let trend = 'STABLE';
  if (!diff.hasBaseline) {
    trend = 'NO_BASELINE';
  } else if (currentFailureRate > prevFailureRate + 0.05) {
    trend = 'DEGRADING';
  } else if (currentFailureRate < prevFailureRate - 0.05) {
    trend = 'IMPROVING';
  }

  const recurringFailures = results
    .filter((r) => r.status === 'FAIL')
    .filter((r) => prevTests.some((p) => (p.id ?? p.name) === r.id && p.status === 'FAIL'))
    .map((r) => r.id);

  const behaviorChangeRate = diff.summary?.behaviorChangeCount ?? 0;
  const prevBehaviorCount = previousBehavior
    ? Object.keys(previousBehavior.byTestId ?? previousBehavior).length
    : 0;
  const behaviorTrend = behaviorChangeRate > prevBehaviorCount * 0.2 ? 'DEGRADING' : 'STABLE';

  const componentHints = groupByComponent(diff.behaviorChanges ?? []);

  /** @type {Array<{ trend: string; component: string; change_rate: number; detail: string }>} */
  const componentTrends = componentHints.map((c) => ({
    trend: c.count >= 2 ? 'DEGRADING' : 'STABLE',
    component: c.component,
    change_rate: Math.round(c.count / Math.max(1, behaviorChangeRate) * 100) / 100,
    detail: `${c.count} behavioral change(s) affecting ${c.component}`,
  }));

  return {
    trend,
    change_rate,
    failure_rate: {
      current: Math.round(currentFailureRate * 1000) / 1000,
      previous: Math.round(prevFailureRate * 1000) / 1000,
    },
    behavior_trend: behaviorTrend,
    recurring_failures: recurringFailures,
    fixed_count: diff.fixed?.length ?? 0,
    new_failure_count: diff.newFailures?.length ?? 0,
    component_trends: componentTrends,
    impact: trend === 'DEGRADING' ? 'Quality regression detected vs previous run' : null,
  };
}

/** @param {Array<{ field: string; test_id: string }>} changes */
function groupByComponent(changes) {
  /** @type {Record<string, number>} */
  const counts = {};
  for (const c of changes) {
    let component = 'general';
    if (c.field.includes('permission')) component = 'permissions-cache';
    else if (c.field.includes('tenant') || c.field.includes('isolation')) component = 'tenant-isolation';
    else if (c.field.includes('auth')) component = 'auth-jwt';
    else if (c.field.includes('health') || c.field.includes('frontend')) component = 'infra';
    counts[component] = (counts[component] ?? 0) + 1;
  }
  return Object.entries(counts)
    .map(([component, count]) => ({ component, count }))
    .sort((a, b) => b.count - a.count);
}
