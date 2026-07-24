import path from 'node:path';
import {
  REPO_ROOT,
  loadConfig,
  loadGrandfather,
  isGrandfathered,
  toRepoRel,
  walkFiles,
  readText,
} from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';
import { extractImportSources, countRelativeDepth } from './shared/rule-utils.mjs';

export const CHECK_NAME = 'import-boundaries';

/**
 * @param {string} source
 * @param {string[]} patterns string includes or regex strings
 */
function matchesForbidden(source, patterns) {
  for (const pattern of patterns) {
    if (pattern.startsWith('/') && pattern.endsWith('/')) {
      if (new RegExp(pattern.slice(1, -1)).test(source)) return pattern;
    } else if (pattern.includes('[^/]+')) {
      if (new RegExp(pattern).test(source)) return pattern;
    } else if (source.includes(pattern)) {
      return pattern;
    }
  }
  return null;
}

/** @param {{ scope: 'pages' | 'modules', rel: string, content: string, patterns: string[], maxDepth: number, result: ReturnType<typeof createCheckResult>, ruleId: string, exemptions: string[] }} ctx */
function scanFile(ctx) {
  const { rel, content, patterns, maxDepth, result, ruleId, exemptions } = ctx;
  if (exemptions.includes(rel)) return;

  for (const { source } of extractImportSources(content)) {
    const depth = countRelativeDepth(source);
    if (depth > maxDepth) {
      addViolation(result, {
        rule: ruleId,
        file: rel,
        message: `import "${source}" exceeds max relative depth (${depth} > ${maxDepth})`,
      });
    }

    const hit = matchesForbidden(source, patterns);
    if (hit) {
      addViolation(result, {
        rule: ruleId,
        file: rel,
        message: `forbidden import "${source}" (pattern: ${hit})`,
      });
    }
  }
}

export function runCheckImportBoundaries() {
  const rules = loadConfig('architecture-rules.json');
  const grandfather = loadGrandfather();
  const result = createCheckResult(CHECK_NAME);
  const exemptions = rules.exemptions?.importBoundaries ?? [];

  const pagesRoot = path.join(REPO_ROOT, 'frontend/src/pages');
  for (const abs of walkFiles(pagesRoot, { extensions: ['.ts', '.tsx'] })) {
    const rel = toRepoRel(abs);
    if (isGrandfathered(grandfather, 'modulesLegacyRootServiceImports', rel)) continue;
    scanFile({
      scope: 'pages',
      rel,
      content: readText(rel),
      patterns: rules.importBoundaries.pages.forbiddenImportPatterns,
      maxDepth: rules.importBoundaries.pages.maxRelativeDepth,
      result,
      ruleId: 'F-import-pages',
      exemptions,
    });
  }

  const modulesRoot = path.join(REPO_ROOT, 'frontend/src/modules');
  for (const abs of walkFiles(modulesRoot, { extensions: ['.ts', '.tsx'] })) {
    const rel = toRepoRel(abs);
    if (isGrandfathered(grandfather, 'modulesLegacyRootServiceImports', rel)) continue;
    scanFile({
      scope: 'modules',
      rel,
      content: readText(rel),
      patterns: rules.importBoundaries.modules.forbiddenImportPatterns,
      maxDepth: rules.importBoundaries.modules.maxRelativeDepth,
      result,
      ruleId: 'F-import-modules',
      exemptions,
    });
  }

  return result;
}

if (process.argv[1]?.endsWith('check-import-boundaries.mjs')) {
  const result = runCheckImportBoundaries();
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(result) ? 0 : 1);
}
