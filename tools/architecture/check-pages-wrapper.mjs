import path from 'node:path';
import {
  ARCH_DIR,
  REPO_ROOT,
  loadConfig,
  loadGrandfather,
  isGrandfathered,
  toRepoRel,
  walkFiles,
  lineCount,
  readText,
} from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';
import { extractImportSources, findHookUsage, findPatternViolations } from './shared/rule-utils.mjs';

export const CHECK_NAME = 'pages-wrapper';

/** @param {{ onlyChanged?: boolean, diffBase?: string }} [options] */
export function runCheckPagesWrapper(options = {}) {
  const rules = loadConfig('architecture-rules.json');
  const grandfather = loadGrandfather();
  const cfg = rules.pageWrapper;
  const result = createCheckResult(CHECK_NAME);

  const pagesRoot = path.join(REPO_ROOT, 'frontend/src/pages');
  const files = walkFiles(pagesRoot, { extensions: ['.tsx'] });

  for (const abs of files) {
    const rel = toRepoRel(abs);
    if (isGrandfathered(grandfather, 'tsxPageWrapperMaxLines15', rel)) continue;
    if (rules.exemptions?.pagesWrapper?.includes(rel)) continue;

    const lines = lineCount(rel);
    if (lines > cfg.maxLines) {
      addViolation(result, {
        rule: 'PR-6',
        file: rel,
        message: `page wrapper has ${lines} lines (max ${cfg.maxLines}); move logic to modules/*/pages/`,
      });
    }

    const content = readText(rel);
    const hooks = findHookUsage(content);
    for (const h of hooks) {
      if (cfg.forbiddenHookPatterns.some((p) => h.name.startsWith(p) || h.name === p)) {
        addViolation(result, {
          rule: 'PR-6',
          file: rel,
          message: `forbidden React hook "${h.name}" in page wrapper (${h.kind})`,
        });
      }
    }

    for (const { source } of extractImportSources(content)) {
      for (const pattern of cfg.forbiddenImportPatterns) {
        if (source.includes(pattern) || new RegExp(pattern).test(source)) {
          addViolation(result, {
            rule: 'PR-6',
            file: rel,
            message: `forbidden import "${source}" (pattern: ${pattern})`,
          });
        }
      }
    }

    const codePatterns = cfg.forbiddenCodePatterns.map((p) => new RegExp(p));
    for (const hit of findPatternViolations(content, codePatterns)) {
      addViolation(result, {
        rule: 'PR-6',
        file: rel,
        line: hit.line,
        message: `forbidden code in page wrapper: ${hit.snippet}`,
      });
    }
  }

  return result;
}

if (import.meta.url === `file://${process.argv[1]?.replace(/\\/g, '/')}` || process.argv[1]?.endsWith('check-pages-wrapper.mjs')) {
  const result = runCheckPagesWrapper();
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(result) ? 0 : 1);
}
