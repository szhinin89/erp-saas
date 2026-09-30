#!/usr/bin/env node
/**
 * CI guard `frontend-subscriber-naming` — contrato owner → subscriber entre módulos frontend.
 *
 * Owner = módulo dueño de una funcionalidad (`frontend/src/modules/<owner>/`).
 * Subscriber = cualquier otro módulo que la consume.
 *
 * Reglas (docs/architecture/frontend.md § Contratos públicos entre módulos):
 *  - F-subscriber-internal-import: un subscriber solo importa del owner archivos ubicados
 *    directamente en una carpeta `facades/` del owner (`modules/<owner>/[<área>/]facades/<x>Facade.ts`).
 *    Nunca api/services, pages, hooks, components, store, types, utils, constants ni barrels.
 *    Aplica también a `import type` (los tipos públicos se re-exportan desde la facade) y a
 *    `import()` dinámico / `typeof import()`.
 *  - F-subscriber-facade-naming: cada archivo de `facades/` se llama `<concepto><Propósito>Facade.ts`
 *    (camelCase, dueño y propósito explícitos) y, si exporta un objeto `const …Facade`, este lleva
 *    exactamente el nombre del archivo.
 *  - F-subscriber-css-import: un módulo nunca importa hojas de estilo privadas de otro módulo
 *    (.css/.scss/.sass/.less bajo `modules/<owner>/`). Un estilo compartido pertenece al Design
 *    System (`src/styles/`, `src/components/zh/`); uno propio del consumidor, a su propio módulo.
 *
 * Fuera de alcance: módulos compartidos declarados en `architecture-rules.json` →
 * `moduleBoundaries.sharedModules`, imports hacia fuera de `modules/` (src/lib, src/styles,
 * components, store…), specifiers de paquetes, assets no-estilo (imágenes/fuentes) y rutas
 * pasadas a `vi.mock()` (arnés de test, no dependencia de producción).
 * El frontend no define aliases de import (sin `paths` en tsconfig ni `resolve.alias` en Vite):
 * solo los specifiers relativos pueden apuntar a otro módulo.
 */
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { REPO_ROOT, loadConfig, toRepoRel, walkFiles, readText } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';

export const CHECK_NAME = 'frontend-subscriber-naming';

export const RULES = {
  internalImport: 'F-subscriber-internal-import',
  facadeNaming: 'F-subscriber-facade-naming',
  cssImport: 'F-subscriber-css-import',
};

const MODULES_PREFIX = 'frontend/src/modules/';
const FACADES_DIR = 'facades';
const STYLE_EXT = /\.(css|scss|sass|less)$/i;
const ASSET_EXT = /\.(svg|png|jpe?g|gif|webp|ico|woff2?|ttf|otf)$/i;
const TEST_FILE = /\.test\.tsx?$/;
/** Nombres que no identifican propósito: `lookupFacade`, `publicFacade`… no dicen de qué owner ni para qué. */
const GENERIC_FACADE_BASES = new Set(['', 'index', 'lookup', 'public', 'module', 'shared', 'common', 'api', 'service', 'main']);

/** @param {string} p */
export function toPosix(p) {
  return p.replace(/\\/g, '/');
}

/** Blanquea líneas de comentario (JSDoc, //, bloques) preservando numeración de líneas. */
function blankCommentLines(content) {
  return content
    .split(/\r?\n/)
    .map((line) => {
      const t = line.trim();
      return t.startsWith('//') || t.startsWith('*') || t.startsWith('/*') ? '' : line;
    })
    .join('\n');
}

const IMPORT_RE =
  /(?:^|[;\s])(?:import|export)\s+(?:type\s+)?(?:[\w*{}\s,$]+?\s+from\s+)?['"]([^'"\n]+)['"]|\bimport\s*(?:<[^>]*>)?\(\s*['"]([^'"\n]+)['"]\s*\)/g;

/**
 * Specifiers importados por un archivo (static, re-export, type-only, dynamic y `typeof import()`),
 * con su número de línea. Ignora `vi.mock("…")`.
 * @param {string} content
 * @returns {{ source: string, line: number }[]}
 */
export function extractImportSpecifiers(content) {
  const code = blankCommentLines(content);
  const out = [];
  let m;
  IMPORT_RE.lastIndex = 0;
  while ((m = IMPORT_RE.exec(code)) !== null) {
    const source = m[1] ?? m[2];
    const line = code.slice(0, m.index).split('\n').length + (code[m.index] === '\n' ? 1 : 0);
    out.push({ source, line });
  }
  return out;
}

/** @param {string} relFile repo-relative, cualquier separador */
export function moduleOf(relFile) {
  const posix = toPosix(relFile);
  if (!posix.startsWith(MODULES_PREFIX)) return null;
  return posix.slice(MODULES_PREFIX.length).split('/')[0] || null;
}

/**
 * Resuelve un specifier relativo contra el archivo que lo importa (repo-relative, POSIX).
 * Specifiers no relativos (paquetes) → null.
 * @param {string} fromFile
 * @param {string} source
 */
export function resolveSpecifier(fromFile, source) {
  const spec = toPosix(source);
  if (!spec.startsWith('.')) return null;
  return path.posix.normalize(path.posix.join(path.posix.dirname(toPosix(fromFile)), spec));
}

/**
 * Clasifica un import:
 *  own | shared | outside | package | asset | style | facade | internal
 * @param {{ fromFile: string, source: string, sharedModules: string[] }} args
 */
export function classifyImport({ fromFile, source, sharedModules }) {
  const subscriber = moduleOf(fromFile);
  const resolved = resolveSpecifier(fromFile, source);
  if (resolved === null) return { kind: 'package', subscriber, owner: null, resolved };
  const owner = moduleOf(resolved);
  if (!owner) return { kind: 'outside', subscriber, owner, resolved };
  if (!subscriber || owner === subscriber) return { kind: 'own', subscriber, owner, resolved };
  if (sharedModules.includes(owner)) return { kind: 'shared', subscriber, owner, resolved };
  if (STYLE_EXT.test(resolved)) return { kind: 'style', subscriber, owner, resolved };
  if (ASSET_EXT.test(resolved)) return { kind: 'asset', subscriber, owner, resolved };
  const segments = resolved.slice(MODULES_PREFIX.length).split('/');
  // el archivo debe estar DIRECTAMENTE dentro de facades/ (nunca `…/facades` como barrel ni subcarpetas)
  const isFacade = segments.length >= 3 && segments[segments.length - 2] === FACADES_DIR;
  return { kind: isFacade ? 'facade' : 'internal', subscriber, owner, resolved };
}

/**
 * Naming de un archivo dentro de `facades/`: `<concepto><Propósito>Facade.ts(x)` y, si exporta
 * `const <x>Facade`, coincide con el nombre del archivo. Devuelve mensajes de violación.
 * @param {string} relFile
 * @param {string} content
 */
export function checkFacadeFileNaming(relFile, content) {
  const base = path.posix.basename(toPosix(relFile));
  if (TEST_FILE.test(base)) return [];
  const m = base.match(/^([a-z][A-Za-z0-9]*)\.tsx?$/);
  if (!m || !m[1].endsWith('Facade')) {
    return [`facade file "${base}" must be named <concept><Purpose>Facade.ts (camelCase, e.g. warehouseLookupFacade.ts)`];
  }
  const name = m[1];
  if (GENERIC_FACADE_BASES.has(name.slice(0, -'Facade'.length).toLowerCase())) {
    return [`facade file "${base}" is too generic — the name must state the owner concept and public purpose`];
  }
  const errors = [];
  const code = blankCommentLines(content);
  for (const exp of code.matchAll(/export\s+const\s+([A-Za-z0-9_]+Facade)\b/g)) {
    if (exp[1] !== name) errors.push(`exported facade "${exp[1]}" must match file name "${name}"`);
  }
  return errors;
}

/**
 * @param {{ files?: { rel: string, content: string }[], sharedModules?: string[] }} [opts]
 *   files/sharedModules permiten ejecutar el guard sobre fixtures en memoria (tests).
 */
export function runCheckFrontendSubscriberNaming(opts = {}) {
  const sharedModules = opts.sharedModules ?? loadConfig('architecture-rules.json').moduleBoundaries.sharedModules;
  const files =
    opts.files ??
    walkFiles(path.join(REPO_ROOT, 'frontend/src/modules'), { extensions: ['.ts', '.tsx'] }).map((abs) => {
      const rel = toRepoRel(abs);
      return { rel, content: readText(rel) };
    });
  const result = createCheckResult(CHECK_NAME);

  for (const { rel: rawRel, content } of files) {
    const rel = toPosix(rawRel);
    if (!rel.startsWith(MODULES_PREFIX)) continue;

    for (const { source, line } of extractImportSpecifiers(content)) {
      const c = classifyImport({ fromFile: rel, source, sharedModules });
      if (c.kind === 'style') {
        addViolation(result, {
          rule: RULES.cssImport,
          file: rel,
          line,
          message: `module "${c.subscriber}" imports private stylesheet of module "${c.owner}" via "${source}" — move shared styles to the Design System (src/styles, src/components/zh) or use own classes`,
        });
        continue;
      }
      if (c.kind !== 'internal') continue;
      addViolation(result, {
        rule: RULES.internalImport,
        file: rel,
        line,
        message: `subscriber "${c.subscriber}" imports owner "${c.owner}" internals via "${source}" — consume modules/${c.owner}/**/facades/<x>Facade.ts`,
      });
    }

    const segments = rel.split('/');
    if (segments[segments.length - 2] === FACADES_DIR) {
      for (const message of checkFacadeFileNaming(rel, content)) {
        addViolation(result, { rule: RULES.facadeNaming, file: rel, message });
      }
    }
  }
  return result;
}

const isMain =
  process.argv[1] &&
  path.resolve(fileURLToPath(import.meta.url)) === path.resolve(process.argv[1]);

if (isMain) {
  const result = runCheckFrontendSubscriberNaming();
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(result) ? 0 : 1);
}
