#!/usr/bin/env node
/**
 * CI guard `frontend-http-access` — el HTTP del frontend vive solo en la capa `api/` del owner.
 *
 * Regla (docs/architecture/frontend.md § Estructura de módulo, ZH-FRONTEND-HTTP-CLIENT-SSOT-01):
 *  - F-http-outside-api: un archivo de `frontend/src` que no está en una carpeta `api/` ni en la
 *    infraestructura HTTP oficial (`src/lib/`, `src/modules/lib/`) no puede hacer HTTP:
 *      · importar los primitivos `apiGet/apiPost/apiPut/apiPatch/apiDelete` (`lib/apiEnvelope`);
 *      · importar la instancia axios `api` (`lib/api`);
 *      · usar axios para enviar requests (`axios.get/post/…`, `axios(…)`, `axios.create`, o
 *        importar esos miembros con nombre). Clasificar errores (`axios.isAxiosError`) no es HTTP;
 *      · `fetch(…)` global o `new XMLHttpRequest()`.
 *    Pantallas, hooks y componentes consumen el service `api/` de su módulo o la facade pública
 *    del owner — nunca un segundo cliente del mismo endpoint.
 *  - F-http-stale-exception: cada entrada de `frontendHttpAccess.infrastructureFiles`
 *    (architecture-rules.json) debe existir y hacer HTTP; lista cerrada con motivo, nunca un
 *    grandfather de deuda.
 *
 * La detección es por import de los primitivos (no por nombre de variable), por eso una variable
 * local llamada `api` o un `refetch()` no son falsos positivos. Fuera de alcance: tests
 * (`*.test.ts(x)`, `__tests__/`), que mockean la capa HTTP.
 */
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { REPO_ROOT, walkFiles, toRepoRel, readText, loadConfig } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';

export const CHECK_NAME = 'frontend-http-access';

export const RULES = {
  outsideApi: 'F-http-outside-api',
  staleException: 'F-http-stale-exception',
};

const SRC_PREFIX = 'frontend/src/';
const TEST_FILE = /(\.test\.tsx?$)|(\/__tests__\/)/;
const ENVELOPE_PRIMITIVES = new Set(['apiGet', 'apiPost', 'apiPut', 'apiPatch', 'apiDelete']);
/** Miembros de axios que no envían requests (clasificación de errores / tipos). */
const AXIOS_NON_HTTP = new Set(['isAxiosError', 'AxiosError', 'isCancel', 'CanceledError', 'AxiosHeaders']);
const AXIOS_HTTP_MEMBER = /\baxios\s*\.\s*(get|post|put|patch|delete|head|options|request|create|postForm|putForm|patchForm)\s*[<(]/;
const AXIOS_CALL = /(?<![\w$.])axios\s*\(/;
const GLOBAL_FETCH = /(?<![\w$.])fetch\s*\(/;
const XHR = /\bnew\s+XMLHttpRequest\s*\(/;

/** @param {string} p */
const toPosix = (p) => p.replace(/\\/g, '/');

/** Blanquea comentarios de línea/bloque preservando numeración de líneas. */
function blankComments(content) {
  return content
    .replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, ' '))
    .replace(/(^|[^:"'`\\])\/\/[^\n]*/g, (m, pre) => pre + ' '.repeat(m.length - pre.length));
}

const lineOf = (code, index) => code.slice(0, index).split('\n').length;

/**
 * Imports del archivo: { names: nombres importados (sin `type`), defaultName, specifier, line }.
 * @param {string} code
 */
function parseImports(code) {
  const out = [];
  const re = /\bimport\s+(type\s+)?([\w$]+\s*,?\s*)?(\{[^}]*\})?\s*(?:\*\s+as\s+([\w$]+)\s*)?from\s*['"]([^'"]+)['"]/g;
  for (const m of code.matchAll(re)) {
    if (m[1]) continue; // import type … — sin runtime
    const defaultName = m[2] ? m[2].replace(/[\s,]/g, '') || null : null;
    const names = m[3]
      ? m[3]
          .slice(1, -1)
          .split(',')
          .map((s) => s.trim())
          .filter((s) => s && !s.startsWith('type '))
          .map((s) => s.split(/\s+as\s+/)[0].trim())
      : [];
    out.push({ names, defaultName: defaultName ?? m[4] ?? null, specifier: m[5], line: lineOf(code, m.index) });
  }
  return out;
}

/**
 * Hallazgos de HTTP en un archivo (independiente de su ubicación).
 * @param {string} content
 * @returns {{ line: number, what: string }[]}
 */
export function findHttpUsages(content) {
  const code = blankComments(content);
  const found = [];
  let importsAxiosDefault = false;
  for (const imp of parseImports(code)) {
    const spec = toPosix(imp.specifier);
    if (/(^|\/)lib\/apiEnvelope$/.test(spec)) {
      const used = imp.names.filter((n) => ENVELOPE_PRIMITIVES.has(n));
      if (used.length) found.push({ line: imp.line, what: `imports ${used.join(', ')} from "${imp.specifier}"` });
    }
    if (/(^|\/)lib\/api$/.test(spec) && imp.names.includes('api')) {
      found.push({ line: imp.line, what: `imports the axios instance "api" from "${imp.specifier}"` });
    }
    if (spec === 'axios') {
      if (imp.defaultName) importsAxiosDefault = true;
      const http = imp.names.filter((n) => !AXIOS_NON_HTTP.has(n));
      if (http.length) found.push({ line: imp.line, what: `imports ${http.join(', ')} from "axios"` });
    }
  }
  if (importsAxiosDefault) {
    for (const re of [AXIOS_HTTP_MEMBER, AXIOS_CALL]) {
      const m = re.exec(code);
      if (m) found.push({ line: lineOf(code, m.index), what: `sends a request with axios ("${m[0].trim()}")` });
    }
  }
  for (const [re, what] of [
    [GLOBAL_FETCH, 'calls global fetch()'],
    [XHR, 'creates an XMLHttpRequest'],
  ]) {
    const m = re.exec(code);
    if (m) found.push({ line: lineOf(code, m.index), what });
  }
  return found;
}

/**
 * ¿Ubicación autorizada para HTTP? Carpeta `api/` (service del owner) o infraestructura oficial.
 * @param {string} rel repo-relative POSIX
 * @param {string[]} allowedDirs
 */
export function isHttpLayer(rel, allowedDirs) {
  if (allowedDirs.some((d) => rel.startsWith(d))) return true;
  return rel.slice(SRC_PREFIX.length).split('/').slice(0, -1).includes('api');
}

/**
 * @param {{
 *   files?: { rel: string, content: string }[],
 *   allowedDirs?: string[],
 *   infrastructureFiles?: { file: string, reason: string }[],
 * }} [opts] files/allowedDirs/infrastructureFiles permiten ejecutar el guard sobre fixtures.
 */
export function runCheckFrontendHttpAccess(opts = {}) {
  const cfg = opts.allowedDirs && opts.infrastructureFiles
    ? { allowedDirs: opts.allowedDirs, infrastructureFiles: opts.infrastructureFiles }
    : loadConfig('architecture-rules.json').frontendHttpAccess;
  const allowedDirs = cfg.allowedDirs;
  const infrastructure = new Map(cfg.infrastructureFiles.map((e) => [toPosix(e.file), e.reason]));
  const files =
    opts.files ??
    walkFiles(path.join(REPO_ROOT, 'frontend/src'), { extensions: ['.ts', '.tsx'] }).map((abs) => {
      const rel = toRepoRel(abs);
      return { rel, content: readText(rel) };
    });
  const result = createCheckResult(CHECK_NAME);
  const seenInfrastructure = new Set();

  for (const { rel: rawRel, content } of files) {
    const rel = toPosix(rawRel);
    if (!rel.startsWith(SRC_PREFIX) || TEST_FILE.test(rel)) continue;
    const usages = findHttpUsages(content);
    if (infrastructure.has(rel)) {
      if (usages.length) seenInfrastructure.add(rel);
      continue;
    }
    if (isHttpLayer(rel, allowedDirs)) continue;
    for (const { line, what } of usages) {
      addViolation(result, {
        rule: RULES.outsideApi,
        file: rel,
        line,
        message: `HTTP outside the api/ layer: ${what} — call the owner's api/ service (or its public facade)`,
      });
    }
  }

  for (const [file, reason] of infrastructure) {
    if (seenInfrastructure.has(file)) continue;
    addViolation(result, {
      rule: RULES.staleException,
      file,
      message: `listed in frontendHttpAccess.infrastructureFiles ("${reason}") but the file does not exist or no longer does HTTP — remove the entry`,
    });
  }
  return result;
}

const isMain =
  process.argv[1] &&
  path.resolve(fileURLToPath(import.meta.url)) === path.resolve(process.argv[1]);

if (isMain) {
  const result = runCheckFrontendHttpAccess();
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(result) ? 0 : 1);
}
