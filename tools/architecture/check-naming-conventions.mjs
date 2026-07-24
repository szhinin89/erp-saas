// check-naming-conventions.mjs
// Verifica convenciones de nombres en backend y frontend.
//
// Backend (C#):
//   - Controllers/    → clase termina en Controller
//   - UseCases/xxx/   → Handler/Command/Query siguen sufijo de archivo
// Frontend (TS/TSX):
//   - pages/          → PascalCase, termina en Page
//   - hooks/          → empieza con "use"
import path from 'node:path';
import { readFileSync } from 'node:fs';
import { REPO_ROOT, walkFiles, toRepoRel, readText } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';

export const CHECK_NAME = 'naming-conventions';

const _gfPath = path.join(REPO_ROOT, 'tools/architecture/architecture-grandfather.json');
const _gfSet = new Set(
  JSON.parse(readFileSync(_gfPath, 'utf8')).namingConventionsGrandfathered ?? []
);

const PASCAL = /^[A-Z][A-Za-z0-9]*$/;

export function runCheckNamingConventions() {
  const result = createCheckResult(CHECK_NAME);

  // ── Backend ────────────────────────────────────────────────────────────────

  const backendSrc = path.join(REPO_ROOT, 'backend/src');

  for (const abs of walkFiles(backendSrc, { extensions: ['.cs'] })) {
    const rel = toRepoRel(abs);
    if (_gfSet.has(rel)) continue;
    const filename = path.basename(abs, '.cs');

    // Controllers: archivos *Controller.cs deben declarar tipo con ese nombre
    if (/\/Controllers\//.test(rel) && filename.endsWith('Controller') && !rel.includes('Tests')) {
      const text = readText(rel);
      if (!text.includes(filename)) {
        addViolation(result, {
          rule: 'B-naming-controller',
          file: rel,
          message: `"${filename}.cs" must declare type ${filename}`,
        });
      }
    }

    // UseCases: *Handler.cs debe tener su tipo en el archivo
    if (/\/UseCases\//.test(rel) && filename.endsWith('Handler')) {
      const text = readText(rel);
      if (!text.includes(filename)) {
        addViolation(result, {
          rule: 'B-naming-handler',
          file: rel,
          message: `"${filename}.cs" must declare type ${filename}`,
        });
      }
    }

    // UseCases: *Command.cs o *Query.cs
    if (/\/UseCases\//.test(rel) &&
        (filename.endsWith('Command') || filename.endsWith('Query'))) {
      const text = readText(rel);
      if (!text.includes(filename)) {
        addViolation(result, {
          rule: 'B-naming-cqrs',
          file: rel,
          message: `"${filename}.cs" must declare type ${filename}`,
        });
      }
    }
  }

  // ── Frontend ───────────────────────────────────────────────────────────────

  const fePages = path.join(REPO_ROOT, 'frontend/src/pages');
  for (const abs of walkFiles(fePages, { extensions: ['.tsx', '.ts'] })) {
    const rel = toRepoRel(abs);
    const filename = path.basename(abs).replace(/\.(tsx|ts)$/, '');
    if (filename === 'index') continue;
    if (!filename.endsWith('Page') && !filename.endsWith('page')) {
      addViolation(result, {
        rule: 'F-naming-page',
        file: rel,
        message: `Page file "${filename}" must end in Page`,
      });
    }
    if (!PASCAL.test(filename)) {
      addViolation(result, {
        rule: 'F-naming-pascal',
        file: rel,
        message: `Page file "${filename}" must be PascalCase`,
      });
    }
  }

  const feHooks = path.join(REPO_ROOT, 'frontend/src/hooks');
  for (const abs of walkFiles(feHooks, { extensions: ['.ts', '.tsx'] })) {
    const rel = toRepoRel(abs);
    const filename = path.basename(abs).replace(/\.(tsx|ts)$/, '');
    if (filename === 'index') continue;
    if (!filename.startsWith('use') && !filename.startsWith('Use')) {
      addViolation(result, {
        rule: 'F-naming-hook',
        file: rel,
        message: `Hook file "${filename}" must start with "use"`,
      });
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-naming-conventions.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckNamingConventions()) ? 0 : 1);
}
