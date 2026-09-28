/** @param {{ name: string, violations: object[], warnings?: object[] }} result */
export function formatConsoleCheck(result) {
  const errors = result.violations.length;
  const warns = result.warnings?.length ?? 0;
  let heading = `[PASS] ${result.name}`;
  if (errors > 0) heading = `[FAIL] ${result.name}`;
  else if (warns > 0) heading = `[WARN] ${result.name}`;
  return [
    heading,
    ...formatFindingLines(result.violations, '-'),
    ...formatFindingLines(result.warnings ?? [], '~'),
  ];
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

function formatFindingLines(findings, marker) {
  return findings.flatMap((item) => {
    const line = item.line ?? item.lines?.[0];
    const location = line == null ? '' : `:${line}`;
    const count = item.count > 1 ? ` (x${item.count})` : '';
    return [`  ${marker} ${item.file}${location}${count}`, `    ${item.rule}: ${item.message}`];
  });
}

function formatRatchetHeading(result, counts) {
  if (counts.new > 0) return `[FAIL] ${result.name} (${counts.historical} historical, ${counts.new} new)`;
  if (counts.historical > 0) return `[BASELINE] ${result.name} (${counts.historical} historical)`;
  if (result.warnings?.length > 0) return `[WARN] ${result.name}`;
  return `[PASS] ${result.name}`;
}

/** @param {object} result @param {object} ratchet */
export function formatRatchetCheck(result, ratchet) {
  const counts = ratchet.checks[result.name] ?? { historical: 0, new: 0 };
  return [
    formatRatchetHeading(result, counts),
    ...formatFindingLines(ratchet.historicalFindings.filter((item) => item.check === result.name), '='),
    ...formatFindingLines(ratchet.newViolations.filter((item) => item.check === result.name), '+'),
    ...formatFindingLines(result.warnings ?? [], '~'),
  ];
}

/** @param {object} ratchet @param {object} [scoreReport] */
export function formatRatchetSummary(ratchet, scoreReport) {
  const status = ratchet.passed
    ? `Architecture gate PASS: ${ratchet.newViolationCount} new violations.`
    : `Architecture gate FAILED: ${ratchet.newViolationCount} new violation(s).`;
  const score = scoreReport
    ? `Architecture score: ${scoreReport.architectureScore}/100 (${scoreReport.status}, drift: ${scoreReport.driftRisk})`
    : null;
  return [
    '',
    status,
    `Historical baseline: ${ratchet.baselineViolations}`,
    `Historical violations remaining: ${ratchet.historicalViolations}`,
    `New violations: ${ratchet.newViolationCount}`,
    `Resolved violations: ${ratchet.resolvedViolationCount}`,
    ...formatFindingLines(ratchet.resolvedViolations.map((item) => ({ ...item, file: `${item.check} ${item.file}` })), '-'),
    ...(score ? [score] : []),
  ];
}
