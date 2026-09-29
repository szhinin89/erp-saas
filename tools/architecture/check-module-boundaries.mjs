import path from 'node:path';
import {
  REPO_ROOT,
  loadConfig,
  toRepoRel,
  walkFiles,
  readText,
} from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';
import { extractImportSources, moduleFromPath, moduleFromImport } from './shared/rule-utils.mjs';

export const CHECK_NAME = 'module-boundaries';

/**
 * Dirección de dependencias entre módulos: pares `forbiddenCrossImports` que nunca pueden
 * depender entre sí, ni siquiera vía facade. Por QUÉ superficie se consume otro módulo
 * (solo `modules/<owner>/facades/`) lo valida `frontend-subscriber-naming`.
 */
export function runCheckModuleBoundaries() {
  const rules = loadConfig('architecture-rules.json');
  const cfg = rules.moduleBoundaries;
  const exemptions = new Set(rules.exemptions?.moduleBoundaries ?? []);
  const result = createCheckResult(CHECK_NAME);

  const modulesRoot = path.join(REPO_ROOT, 'frontend/src/modules');
  for (const abs of walkFiles(modulesRoot, { extensions: ['.ts', '.tsx'] })) {
    const rel = toRepoRel(abs);
    if (exemptions.has(rel)) continue;

    const fromMod = moduleFromPath(rel);
    if (!fromMod) continue;

    const content = readText(rel);
    for (const { source } of extractImportSources(content)) {
      const toMod = moduleFromImport(source, rel);
      if (!toMod || toMod === fromMod) continue;

      if (cfg.sharedModules.includes(toMod)) continue;

      const forbidden = cfg.forbiddenCrossImports.some((r) => r.from === fromMod && r.to === toMod);
      if (forbidden) {
        addViolation(result, {
          rule: 'F-module-boundary',
          file: rel,
          message: `module "${fromMod}" must not import "${toMod}" via "${source}"`,
        });
      }
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-module-boundaries.mjs')) {
  const result = runCheckModuleBoundaries();
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(result) ? 0 : 1);
}
