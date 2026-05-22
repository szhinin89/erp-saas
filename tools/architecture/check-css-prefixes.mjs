import path from 'node:path';
import {
  REPO_ROOT,
  loadConfig,
  toRepoRel,
  walkFiles,
  readText,
  matchGlob,
} from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';
import { stripComments } from './shared/rule-utils.mjs';

export const CHECK_NAME = 'css-prefixes';

/** @param {string} relPath @param {{ pathPrefixMap: { glob: string, prefixes: string[] }[] }} cfg */
function expectedPrefixesForFile(relPath, cfg) {
  const norm = relPath.replace(/\\/g, '/');
  for (const entry of cfg.pathPrefixMap) {
    if (matchGlob(entry.glob, norm) || matchGlob(`**/${entry.glob.replace(/^\*\*\//, '')}`, norm)) {
      return entry.prefixes;
    }
  }
  return [];
}

/** @param {string} className @param {string[]} prefixes @param {string[]} globalPrefixes @param {string[]} globalExact */
function isAllowedClass(className, prefixes, globalPrefixes, globalExact) {
  if (globalExact.includes(className)) return true;
  if (globalPrefixes.some((p) => className.startsWith(p) || className === p.replace(/-$/, ''))) return true;
  if (prefixes.some((p) => className.startsWith(p))) return true;
  return false;
}

/** @param {string} content */
function extractClassSelectors(content) {
  const stripped = stripComments(content);
  /** @type {string[]} */
  const classes = [];
  const re = /\.([a-zA-Z][\w-]*)\s*(?=[,{:\s>+~])/g;
  let m;
  while ((m = re.exec(stripped)) !== null) {
    classes.push(m[1]);
  }
  return classes;
}

export function runCheckCssPrefixes() {
  const cfg = loadConfig('css-prefixes.json');
  const rules = loadConfig('architecture-rules.json');
  const skip = new Set(cfg.skipFiles ?? []);
  const exemptions = new Set(rules.exemptions?.cssPrefixes ?? []);
  const result = createCheckResult(CHECK_NAME);

  const cssFiles = walkFiles(path.join(REPO_ROOT, 'frontend/src'), { extensions: ['.css'] });

  for (const abs of cssFiles) {
    const rel = toRepoRel(abs);
    if (skip.has(rel) || exemptions.has(rel)) continue;
    if (!cfg.scanGlobs.some((g) => matchGlob(g, rel))) continue;

    const prefixes = expectedPrefixesForFile(rel, cfg);
    const content = readText(rel);
    const classes = extractClassSelectors(content);

    for (const className of classes) {
      if (cfg.ambiguousClassNames.includes(className)) {
        addViolation(result, {
          rule: 'F-css-ambiguous',
          file: rel,
          message: `ambiguous global class ".${className}" — use page prefix (${prefixes.join(', ') || 'see css-prefixes.json'})`,
        });
        continue;
      }

      if (prefixes.length === 0) continue;

      if (!isAllowedClass(className, prefixes, cfg.globalAllowedPrefixes, cfg.globalAllowedExactClasses)) {
        addViolation(result, {
          rule: 'F-css-prefix',
          file: rel,
          message: `class ".${className}" must use prefix ${prefixes.map((p) => `"${p}"`).join(' or ')} or global zh-ui/pg-* token`,
        });
      }
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-css-prefixes.mjs')) {
  const result = runCheckCssPrefixes();
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(result) ? 0 : 1);
}
