/**
 * GitHub Actions workflow commands
 * @see https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions
 */

/** @param {string} s */
function escapeAnnotation(s) {
  return String(s).replace(/%/g, '%25').replace(/\r/g, '%0D').replace(/\n/g, '%0A');
}

/**
 * @param {{ rule: string, file: string, message: string, line?: number }} item
 * @param {'error' | 'warning' | 'notice'} [level]
 */
export function formatGithubAnnotation(item, level = 'error') {
  const file = item.file.replace(/\\/g, '/');
  const line = item.line ?? 1;
  const title = escapeAnnotation(item.rule);
  const message = escapeAnnotation(`${item.rule}: ${item.message}`);
  return `::${level} file=${file},line=${line},title=${title}::${message}`;
}

/** @param {Array<{ rule: string, file: string, message: string, line?: number }>} items @param {'error'|'warning'} level */
export function formatGithubAnnotations(items, level = 'error') {
  return items.map((v) => formatGithubAnnotation(v, level));
}
