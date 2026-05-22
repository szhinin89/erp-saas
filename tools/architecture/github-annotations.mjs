import { collectAllFindings } from './shared/report-utils.mjs';
import { formatGithubAnnotations } from './formatters/github-formatter.mjs';

/**
 * Emit GitHub Actions annotations to stdout.
 * @param {import('./shared/report-utils.mjs').CheckResult[]} results
 * @param {{ warnings?: boolean }} [opts]
 */
export function emitGithubAnnotations(results, opts = {}) {
  const { violations, warnings } = collectAllFindings(results);
  const lines = [...formatGithubAnnotations(violations, 'error')];
  if (opts.warnings !== false) {
    lines.push(...formatGithubAnnotations(warnings, 'warning'));
  }
  for (const line of lines) {
    console.log(line);
  }
  return lines.length;
}

if (process.argv[1]?.endsWith('github-annotations.mjs')) {
  const { runAllChecks } = await import('./run-all.mjs');
  const results = runAllChecks({ silent: true });
  emitGithubAnnotations(results);
  const failed = results.some((r) => r.violations.length > 0);
  process.exit(failed ? 1 : 0);
}
