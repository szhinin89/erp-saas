// check-grandfather-integrity.mjs
// Integridad de architecture-grandfather.json: toda entrada de una lista de archivos debe
// apuntar a un archivo existente (con mayúsculas exactas, como lo compara cada checker)
// y seguir dentro del alcance de la regla que la consume. Una entrada muerta no exime nada
// pero deja un hueco: si el archivo reaparece, pasaría sin revisión.
// Listas vacías son válidas; valores no-lista (p. ej. frontendIndexChunkMaxKb) no son de archivos.
import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT, loadConfig, loadGrandfather, matchGlob } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';
import { designSystemRulesFor } from './check-design-system.mjs';
import { backendNamingRuleFor } from './check-naming-conventions.mjs';

export const CHECK_NAME = 'grandfather-integrity';

const GRANDFATHER_FILE = 'tools/architecture/architecture-grandfather.json';

/**
 * @typedef {{
 *   consumer: string,
 *   shape: 'path' | 'fileRules',
 *   inScope: (rel: string) => boolean,
 *   rulesFor?: (rel: string) => Set<string>,
 * }} GrandfatherListSpec
 */

/**
 * Listas de archivos conocidas y el alcance real del checker que las consume.
 * Una lista nueva en el JSON debe registrarse aquí junto con su consumidor.
 * @returns {Record<string, GrandfatherListSpec>}
 */
export function defaultListSpecs() {
  const designCfg = loadConfig('design-system.json');
  const controllersPath = loadConfig('architecture-rules.json').backend.controller.path;
  return {
    designSystemGrandfathered: {
      consumer: 'check-design-system.mjs',
      shape: 'fileRules',
      inScope: (rel) => designSystemRulesFor(designCfg, rel).size > 0,
      rulesFor: (rel) => designSystemRulesFor(designCfg, rel),
    },
    handlerHandleMaxLines150: {
      consumer: 'tools/quality/check-handler-size.ps1',
      shape: 'path',
      inScope: (rel) =>
        rel.startsWith('backend/src/ERP.Application/') && /Handler[^/]*\.cs$/.test(rel),
    },
    tsxMaxLines500: {
      consumer: 'check-architecture-guardrails.ps1',
      shape: 'path',
      inScope: (rel) => matchGlob('frontend/src/**/*.tsx', rel),
    },
    tsxPageWrapperMaxLines15: {
      consumer: 'check-pages-wrapper.mjs',
      shape: 'path',
      inScope: (rel) => matchGlob('frontend/src/pages/**/*.tsx', rel),
    },
    modulesLegacyRootServiceImports: {
      consumer: 'check-import-boundaries.mjs',
      shape: 'path',
      inScope: (rel) => /^frontend\/src\/(pages|modules)\/.+\.tsx?$/.test(rel),
    },
    backendControllerMaxLines: {
      consumer: 'check-backend-controller-thin.mjs',
      shape: 'path',
      inScope: (rel) => rel.startsWith(`${controllersPath}/`) && rel.endsWith('.cs'),
    },
    namingConventionsGrandfathered: {
      consumer: 'check-naming-conventions.mjs',
      shape: 'path',
      inScope: (rel) => backendNamingRuleFor(rel) !== null,
    },
  };
}

/**
 * Existe como archivo con las mayúsculas exactas de la ruta (los checkers comparan con
 * toRepoRel, que devuelve el nombre real; en Windows existsSync ignoraría mayúsculas).
 * @param {string} rel repo-relativo posix
 */
export function fileExistsExactCase(rel) {
  let current = REPO_ROOT;
  const segments = rel.split('/');
  for (let i = 0; i < segments.length; i++) {
    let entries;
    try {
      entries = fs.readdirSync(current, { withFileTypes: true });
    } catch {
      return false;
    }
    const entry = entries.find((e) => e.name === segments[i]);
    if (!entry) return false;
    const isLast = i === segments.length - 1;
    if (isLast ? !entry.isFile() : !entry.isDirectory()) return false;
    current = path.join(current, entry.name);
  }
  return true;
}

/**
 * @param {Record<string, unknown>} grandfather contenido de architecture-grandfather.json
 * @param {{ specs: Record<string, GrandfatherListSpec>, fileExists: (rel: string) => boolean }} deps
 * @returns {{ rule: string, file: string, message: string }[]}
 */
export function findGrandfatherIntegrityViolations(grandfather, { specs, fileExists }) {
  /** @type {{ rule: string, file: string, message: string }[]} */
  const found = [];
  const report = (rule, message) => found.push({ rule, file: GRANDFATHER_FILE, message });

  for (const [list, value] of Object.entries(grandfather)) {
    if (list.startsWith('$') || !Array.isArray(value) || value.length === 0) continue;

    const spec = specs[list];
    if (!spec) {
      report(
        'GF-unknown-list',
        `Lista "${list}" no tiene checker consumidor registrado en check-grandfather-integrity.mjs — sus ${value.length} entrada(s) no eximen nada; eliminarla o registrar su consumidor.`,
      );
      continue;
    }

    // Duplicados se toleran (los consumidores usan Set/some); se reporta una vez por ruta.
    const seen = new Set();
    for (const item of value) {
      const raw = spec.shape === 'fileRules' ? item?.file : item;
      if (typeof raw !== 'string' || raw.trim() === '') {
        report('GF-invalid-entry', `Lista "${list}": entrada inválida ${JSON.stringify(item)} — se esperaba ${spec.shape === 'fileRules' ? '{ file, rules }' : 'una ruta repo-relativa'}.`);
        continue;
      }
      const rel = raw.replace(/\\/g, '/');
      if (seen.has(rel)) continue;
      seen.add(rel);

      if (!fileExists(rel)) {
        report(
          'GF-dead-entry',
          `Lista "${list}": la ruta "${rel}" no existe — eliminar la entrada muerta de architecture-grandfather.json.`,
        );
        continue;
      }
      if (!spec.inScope(rel)) {
        report(
          'GF-out-of-scope',
          `Lista "${list}": la ruta "${rel}" está fuera del alcance de ${spec.consumer} — la entrada no exime nada; eliminarla.`,
        );
        continue;
      }
      if (spec.shape === 'fileRules') {
        const applicable = spec.rulesFor(rel);
        const rules = Array.isArray(item.rules) ? item.rules : [];
        if (rules.length === 0) {
          report('GF-invalid-entry', `Lista "${list}": la ruta "${rel}" no declara reglas — eliminar la entrada.`);
        }
        for (const rule of rules) {
          if (!applicable.has(rule)) {
            report(
              'GF-out-of-scope',
              `Lista "${list}": la regla "${rule}" no aplica a "${rel}" en ${spec.consumer} — eliminarla de la entrada.`,
            );
          }
        }
      }
    }
  }
  return found;
}

export function runCheckGrandfatherIntegrity() {
  const result = createCheckResult(CHECK_NAME);
  const violations = findGrandfatherIntegrityViolations(loadGrandfather(), {
    specs: defaultListSpecs(),
    fileExists: fileExistsExactCase,
  });
  for (const v of violations) addViolation(result, v);
  return result;
}

if (process.argv[1]?.endsWith('check-grandfather-integrity.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckGrandfatherIntegrity()) ? 0 : 1);
}
