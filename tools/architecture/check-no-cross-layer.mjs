import path from 'node:path';
import fs from 'node:fs';
import {
  REPO_ROOT,
  loadConfig,
  toRepoRel,
  walkFiles,
  readText,
} from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';
import { findPatternViolations } from './shared/rule-utils.mjs';

export const CHECK_NAME = 'no-cross-layer';

export function runCheckNoCrossLayer() {
  const rules = loadConfig('architecture-rules.json');
  const cfg = rules.crossLayer;
  const exemptions = new Set(rules.exemptions?.crossLayer ?? []);
  const result = createCheckResult(CHECK_NAME);

  const pagesRoot = path.join(REPO_ROOT, 'frontend/src/pages');
  for (const abs of walkFiles(pagesRoot, { extensions: ['.tsx'] })) {
    const rel = toRepoRel(abs);
    if (exemptions.has(rel)) continue;
    const patterns = cfg.pagesForbiddenPatterns.map((p) => new RegExp(p));
    for (const hit of findPatternViolations(readText(rel), patterns)) {
      addViolation(result, {
        rule: 'F-cross-layer-pages',
        file: rel,
        line: hit.line,
        message: hit.snippet,
      });
    }
  }

  // HTTP en páginas/hooks/componentes de módulo (antes F-cross-layer-module-page: solo fetch/axios
  // en pages/*.tsx, por texto): lo cubre por completo frontend-http-access (F-http-outside-api,
  // ZH-FRONTEND-HTTP-CLIENT-SSOT-01) — una sola regla, un solo guard.

  const storesRoot = path.join(REPO_ROOT, 'frontend/src/store');
  if (fs.existsSync(storesRoot)) {
    for (const abs of walkFiles(storesRoot, { extensions: ['.ts', '.tsx'] })) {
      const rel = toRepoRel(abs);
      if (exemptions.has(rel)) continue;
      const content = readText(rel);
      for (const pattern of cfg.storesForbiddenImportPatterns) {
        if (content.includes(pattern)) {
          addViolation(result, {
            rule: 'F-cross-layer-store',
            file: rel,
            message: `store must not import infrastructure pattern "${pattern}"`,
          });
        }
      }
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-no-cross-layer.mjs')) {
  const result = runCheckNoCrossLayer();
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(result) ? 0 : 1);
}
