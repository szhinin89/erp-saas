import path from 'node:path';
import {
  REPO_ROOT,
  loadConfig,
  toRepoRel,
  walkFiles,
  readText,
} from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';

export const CHECK_NAME = 'css-reachability';

/**
 * F-04-css-reach — dependencia CSS entre rutas.
 *
 * Una pantalla (raíz lazy de `routes/`) debe cargar por sí sola el CSS de todas las clases que
 * usan sus componentes. Si un componente usa una clase cuya ÚNICA hoja propietaria no está en el
 * cierre de imports de esa pantalla ni en el CSS global (`main.tsx` + `App`), la pantalla solo se
 * ve bien tras visitar otra que sí cargó esa hoja (acceso directo / recarga → sin estilos).
 *
 * Modelo (deliberadamente simple):
 *  - Grafo de imports relativos (TS/TSX/CSS `@import`). Los `import type` no cuentan.
 *  - CSS global = hojas alcanzables desde `entry` (incluye el `import()` de `main.tsx` hacia App).
 *  - Raíz de ruta = destino de un `import()` escrito dentro de `routeDirs`.
 *  - Cierre de una raíz = imports estáticos, y también los `import()` de archivos que NO están en
 *    `routeDirs` (un modal lazy interno carga junto a su pantalla).
 *  - "Propietaria" de una clase = hoja donde la clase encabeza algún selector. Una clase usada
 *    solo como contexto (`.x .marcador`) no hace propietaria a esa hoja: evita falsos positivos.
 *  - Una clase propiedad de una hoja global nunca se marca.
 *  - Comentarios y archivos de prueba no cuentan.
 *
 * Línea base (`config/css-reachability.json`): conjunto de pares `consumidor|hoja(s)` conocidos.
 * Solo puede bajar: un par nuevo falla, y un par ya resuelto que sigue en la base también falla.
 */

const RULE = 'F-04-css-reach';

/** Reemplaza comentarios por espacios (conserva saltos de línea). */
export function blankComments(src) {
  const blank = (m) => m.replace(/[^\n]/g, ' ');
  return src
    .replace(/\/\*[\s\S]*?\*\//g, blank)
    .replace(/(^|[^:\w"'`])(\/\/[^\n]*)/g, (_, pre, c) => pre + blank(c));
}

const isTest = (rel) => /\.(test|spec)\.tsx?$/.test(rel) || /\.d\.ts$/.test(rel);

function resolveRel(files, fromRel, spec) {
  if (!spec.startsWith('.')) return null;
  const base = path.posix.normalize(path.posix.join(path.posix.dirname(fromRel), spec));
  for (const c of [base, `${base}.ts`, `${base}.tsx`, `${base}.css`, `${base}/index.ts`, `${base}/index.tsx`]) {
    if (files.has(c)) return c;
  }
  return null;
}

/** Primer compuesto de un selector (hasta espacio/combinador de nivel 0). */
function firstCompound(selector) {
  const s = selector.trim();
  let depth = 0;
  let out = '';
  for (const ch of s) {
    if (ch === '(' || ch === '[') depth++;
    else if (ch === ')' || ch === ']') depth--;
    if (depth === 0 && /[\s>+~]/.test(ch)) break;
    out += ch;
  }
  return out;
}

/** clase -> Set(css) donde la clase encabeza algún selector. */
export function cssClassOwners(files) {
  /** @type {Map<string, Set<string>>} */
  const owners = new Map();
  for (const [rel, content] of files) {
    if (!rel.endsWith('.css')) continue;
    const code = blankComments(content);
    for (const m of code.matchAll(/([^{}@;]+)\{/g)) {
      for (const sel of m[1].split(',')) {
        for (const c of firstCompound(sel).matchAll(/\.([a-zA-Z_][\w-]*)/g)) {
          if (!owners.has(c[1])) owners.set(c[1], new Set());
          owners.get(c[1]).add(rel);
        }
      }
    }
  }
  return owners;
}

/** Tokens con forma de clase (`algo-otro`, `algo__otro`) dentro de literales de cadena. */
export function classTokens(code) {
  const out = new Set();
  for (const m of code.matchAll(/(["'`])((?:\\.|(?!\1)[^\\\n])*)\1/g)) {
    for (const t of m[2].replace(/\$\{[^}]*\}/g, ' ').split(/\s+/)) {
      if (/^[a-z][\w]*([-_]{1,2}[\w]+)+$/.test(t)) out.add(t);
    }
  }
  return out;
}

/**
 * @param {Map<string, string>} files  rel-posix -> contenido (solo frontend/src, sin pruebas)
 * @param {{ entry: string, routeDirs: string[], baseline?: string[] }} cfg
 */
export function analyzeCssReachability(files, cfg) {
  const staticDeps = new Map();
  const dynDeps = new Map();
  for (const [rel, content] of files) {
    if (rel.endsWith('.css')) {
      const deps = [...blankComments(content).matchAll(/@import\s+(?:url\()?["']([^"']+)["']/g)]
        .map((m) => resolveRel(files, rel, m[1]))
        .filter(Boolean);
      staticDeps.set(rel, deps);
      dynDeps.set(rel, []);
      continue;
    }
    const code = blankComments(content);
    const st = [];
    for (const m of code.matchAll(/\b(import|export)\s+(type\s+)?(?:[^"';]*?\sfrom\s+)?["']([^"']+)["']/g)) {
      if (m[2]) continue; // import type / export type: sin efecto en runtime
      const r = resolveRel(files, rel, m[3]);
      if (r) st.push(r);
    }
    const dy = [...code.matchAll(/\bimport\(\s*["']([^"']+)["']\s*\)/g)]
      .map((m) => resolveRel(files, rel, m[1]))
      .filter(Boolean);
    staticDeps.set(rel, st);
    dynDeps.set(rel, dy);
  }

  const inRoutes = (rel) => cfg.routeDirs.some((d) => rel.startsWith(d));
  const cache = new Map();
  const closure = (root) => {
    if (cache.has(root)) return cache.get(root);
    const seen = new Set();
    const stack = [root];
    while (stack.length) {
      const n = stack.pop();
      if (seen.has(n)) continue;
      seen.add(n);
      for (const d of staticDeps.get(n) ?? []) stack.push(d);
      if (!inRoutes(n)) for (const d of dynDeps.get(n) ?? []) stack.push(d);
    }
    cache.set(root, seen);
    return seen;
  };

  const globalCss = new Set([...closure(cfg.entry)].filter((f) => f.endsWith('.css')));
  const owners = cssClassOwners(files);
  const roots = new Set();
  for (const [rel, ds] of dynDeps) if (inRoutes(rel)) for (const d of ds) roots.add(d);

  /** @type {Map<string, { consumer: string, css: string[], classes: Set<string>, roots: Set<string> }>} */
  const found = new Map();
  const tokenCache = new Map();
  for (const root of roots) {
    const cl = closure(root);
    const loaded = (css) => globalCss.has(css) || cl.has(css);
    for (const f of cl) {
      if (!f.endsWith('.tsx')) continue;
      if (!tokenCache.has(f)) tokenCache.set(f, classTokens(blankComments(files.get(f))));
      for (const tok of tokenCache.get(f)) {
        const own = owners.get(tok);
        if (!own) continue;
        if ([...own].some(loaded)) continue;
        const css = [...own].sort();
        const key = `${f}|${css.join(',')}`;
        if (!found.has(key)) found.set(key, { consumer: f, css, classes: new Set(), roots: new Set() });
        const e = found.get(key);
        e.classes.add(tok);
        e.roots.add(root);
      }
    }
  }

  const baseline = new Set(cfg.baseline ?? []);
  const keys = new Set(found.keys());
  const added = [...keys].filter((k) => !baseline.has(k)).sort();
  const stale = [...baseline].filter((k) => !keys.has(k)).sort();
  return { found, added, stale, rootCount: roots.size, globalCss: [...globalCss].sort() };
}

function loadFrontendFiles() {
  /** @type {Map<string, string>} */
  const files = new Map();
  for (const abs of walkFiles(path.join(REPO_ROOT, 'frontend/src'), { extensions: ['.ts', '.tsx', '.css'] })) {
    const rel = toRepoRel(abs);
    if (isTest(rel)) continue;
    files.set(rel, readText(rel));
  }
  return files;
}

export function runCheckCssReachability() {
  const cfg = loadConfig('css-reachability.json');
  const result = createCheckResult(CHECK_NAME);
  const { found, added, stale } = analyzeCssReachability(loadFrontendFiles(), cfg);

  for (const key of added) {
    const e = found.get(key);
    addViolation(result, {
      rule: RULE,
      file: e.consumer,
      message:
        `Usa ${[...e.classes].sort().slice(0, 4).map((c) => `.${c}`).join(', ')}${e.classes.size > 4 ? '…' : ''} ` +
        `cuya hoja propietaria (${e.css.join(' | ')}) no se carga con ${e.roots.size} pantalla(s) que lo incluyen ` +
        `(p. ej. ${[...e.roots].sort()[0]}). Importar el CSS desde el componente, o extraer a un CSS compartido propio; ` +
        'no depender de haber visitado otra pantalla.',
    });
  }
  for (const key of stale) {
    addViolation(result, {
      rule: RULE,
      file: key.split('|')[0],
      message: `Entrada de línea base obsoleta '${key}': la dependencia ya no existe — eliminarla de config/css-reachability.json (la base solo baja).`,
    });
  }
  return result;
}

if (process.argv[1]?.endsWith('check-css-reachability.mjs')) {
  const result = runCheckCssReachability();
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(result) ? 0 : 1);
}
