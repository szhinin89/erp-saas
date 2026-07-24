/** @param {{ name: string, violations: object[], warnings?: object[] }} result */
export function formatConsoleCheck(result) {
  const lines = [];
  const errors = result.violations.length;
  const warns = result.warnings?.length ?? 0;
  if (errors === 0 && warns === 0) {
    lines.push(`[PASS] ${result.name}`);
    return lines;
  }
  if (errors === 0) {
    lines.push(`[WARN] ${result.name}`);
  } else {
    lines.push(`[FAIL] ${result.name}`);
  }
  for (const v of result.violations) {
    const loc = v.line != null ? `:${v.line}` : '';
    lines.push(`  - ${v.file}${loc}`);
    lines.push(`    ${v.rule}: ${v.message}`);
  }
  for (const w of result.warnings ?? []) {
    const loc = w.line != null ? `:${w.line}` : '';
    lines.push(`  ~ ${w.file}${loc}`);
    lines.push(`    ${w.rule}: ${w.message}`);
  }
  return lines;
}

/** @param {object[]} results @param {object} [scoreReport] */
export function formatConsoleSummary(results, scoreReport) {
  const lines = [];
  const failed = results.filter((r) => r.violations.length > 0);
  const totalViolations = failed.reduce((n, r) => n + r.violations.length, 0);
  const totalWarnings = results.reduce((n, r) => n + (r.warnings?.length ?? 0), 0);

  lines.push('');
  if (failed.length === 0) {
    lines.push(`Architecture checks OK (${results.length}/${results.length} passed).`);
  } else {
    lines.push(`Architecture checks FAILED: ${failed.length} check(s), ${totalViolations} violation(s).`);
    for (const r of failed) {
      lines.push(`  ✗ ${r.name} (${r.violations.length})`);
    }
  }
  if (totalWarnings > 0) {
    lines.push(`Warnings: ${totalWarnings} (score impact only).`);
  }
  if (scoreReport) {
    lines.push(
      `Architecture score: ${scoreReport.architectureScore}/100 (${scoreReport.status}, drift: ${scoreReport.driftRisk})`,
    );
  }
  return lines;
}
