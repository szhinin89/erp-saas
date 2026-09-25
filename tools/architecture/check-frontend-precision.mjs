import fs from 'node:fs';
import path from 'node:path';
import {
  ARCH_DIR,
  REPO_ROOT,
  loadConfig,
  loadGrandfather,
  toRepoRel,
  walkFiles,
  readText,
  matchGlob,
} from './shared/fs-utils.mjs';
import { createCheckResult, addViolation, addWarning } from './shared/report-utils.mjs';

/**
 * F-PREC — ZH-DESIGN-SYSTEM-PRECISION-05. Canónico: docs/architecture/frontend.md § Precision guard.
 *
 * Impide que código productivo del frontend vuelva a decidir precisión numérica a mano o por el
 * default legacy (2) en vez de la semántica (`PrecisionKind`) del Design System:
 *   F-PREC-decimals         `decimals={…}` en JSX que no es una constante contractual `*_DECIMALS`.
 *   F-PREC-implicit-format  `formatMoney(x)` / `formatMoneyWithSymbol(x)` sin escala.
 *   F-PREC-implicit-value   `<ZHMoneyValue|ZHNumberValue|ZhDecimalInput|ZhCurrencyInput>` sin
 *                           `precision` ni `decimals` (default legacy 2).
 *   F-PREC-toFixed          `.toFixed(…)` (formato/redondeo local; usar el motor único).
 *   F-PREC-intl             `Intl.NumberFormat` local (la representación la decide el Design System).
 *   F-PREC-policy-read      `getPrecisionPolicy()` fuera de la infraestructura (usar hooks/`precision`).
 *
 * Análisis textual contextual (sin AST): comentarios eliminados preservando líneas; llamadas y
 * etiquetas JSX con paréntesis/llaves balanceados. Tolerancia por ocurrencias EXACTAS: cada
 * (archivo, regla) tiene un máximo = deuda legacy (architecture-grandfather.json →
 * frontendPrecisionGrandfathered) + excepción justificada (config/frontend-precision.json →
 * exceptions). Superarlo → violación; quedar por debajo → aviso para bajar el baseline.
 */
export const CHECK_NAME = 'frontend-precision';

export const RULES = {
  decimals: 'F-PREC-decimals',
  implicitFormat: 'F-PREC-implicit-format',
  implicitValue: 'F-PREC-implicit-value',
  toFixed: 'F-PREC-toFixed',
  intl: 'F-PREC-intl',
  policyRead: 'F-PREC-policy-read',
};

const MESSAGES = {
  [RULES.decimals]:
    '`decimals={…}` decide la escala a mano — declarar `precision="<PrecisionKind>"`; un override contractual usa una constante nombrada `*_DECIMALS` documentada.',
  [RULES.implicitFormat]:
    'formatter sin escala (default legacy 2) — pasar `usePrecisionDecimals(kind)` o usar ZHMoneyValue/ZHNumberValue con `precision`.',
  [RULES.implicitValue]:
    'componente numérico sin `precision` ni `decimals` (default legacy 2) — declarar `precision="<PrecisionKind>"`.',
  [RULES.toFixed]:
    '`.toFixed()` local — la presentación usa el motor único (ZH*Value / formatDecimalDisplay con escala semántica); un cálculo legítimo se registra como excepción.',
  [RULES.intl]: '`Intl.NumberFormat` local — la representación numérica la decide el Design System.',
  [RULES.policyRead]:
    '`getPrecisionPolicy()` directo — en presentación usar `precision`/`usePrecisionDecimals`; un cálculo legítimo se registra como excepción.',
};

const CONTRACT_CONSTANT = /^[A-Z][A-Z0-9_]*_DECIMALS$/;
const VALUE_TAGS = ['ZHMoneyValue', 'ZHNumberValue', 'ZhDecimalInput', 'ZhCurrencyInput'];

/** Elimina comentarios `//` y `/* *\/` preservando saltos de línea (los números de línea no cambian). */
export function stripCommentsKeepLines(content) {
  let out = '';
  let i = 0;
  let quote = null;
  while (i < content.length) {
    const c = content[i];
    const n = content[i + 1];
    if (quote) {
      out += c;
      if (c === '\\') {
        out += n ?? '';
        i += 2;
        continue;
      }
      if (c === quote) quote = null;
      i++;
      continue;
    }
    if (c === '"' || c === "'" || c === '`') {
      quote = c;
      out += c;
      i++;
      continue;
    }
    if (c === '/' && n === '/') {
      while (i < content.length && content[i] !== '\n') i++;
      continue;
    }
    if (c === '/' && n === '*') {
      i += 2;
      while (i < content.length && !(content[i] === '*' && content[i + 1] === '/')) {
        out += content[i] === '\n' ? '\n' : ' ';
        i++;
      }
      i += 2;
      continue;
    }
    out += c;
    i++;
  }
  return out;
}

function lineOf(content, index) {
  return content.slice(0, index).split('\n').length;
}

/** Índice justo después del cierre balanceado que empieza en `open` (posición del delimitador). */
function balancedEnd(content, open) {
  let depth = 0;
  for (let j = open; j < content.length; j++) {
    const c = content[j];
    if (c === '(' || c === '{' || c === '[') depth++;
    else if (c === ')' || c === '}' || c === ']') {
      depth--;
      if (depth === 0) return j + 1;
    }
  }
  return content.length;
}

/** Cuenta argumentos de primer nivel de la llamada cuyo `(` está en `open`. */
function countArgs(content, open) {
  const end = balancedEnd(content, open);
  const inner = content.slice(open + 1, end - 1).trim().replace(/,\s*$/, '');
  if (!inner) return 0;
  let depth = 0;
  let args = 1;
  for (const c of inner) {
    if (c === '(' || c === '{' || c === '[') depth++;
    else if (c === ')' || c === '}' || c === ']') depth--;
    else if (c === ',' && depth === 0) args++;
  }
  return args;
}

/** Atributos de la etiqueta JSX que empieza en `start` (texto hasta el `>` de primer nivel). */
function jsxTagText(content, start) {
  let depth = 0;
  for (let j = start; j < content.length; j++) {
    const c = content[j];
    if (c === '{') depth++;
    else if (c === '}') depth--;
    else if (c === '>' && depth === 0) return content.slice(start, j + 1);
  }
  return content.slice(start);
}

/**
 * Hallazgos F-PREC de un archivo (sin aplicar tolerancias).
 * @param {string} rel @param {string} raw
 * @returns {{ rule: string, file: string, line: number, message: string }[]}
 */
export function findPrecisionHits(rel, raw) {
  const content = stripCommentsKeepLines(raw);
  const hits = [];
  const push = (rule, index) =>
    hits.push({ rule, file: rel, line: lineOf(content, index), message: MESSAGES[rule] });

  // F-PREC-decimals — atributo JSX `decimals={expr}`.
  for (const m of content.matchAll(/\bdecimals=\{/g)) {
    const open = m.index + m[0].length - 1;
    const expr = content.slice(open + 1, balancedEnd(content, open) - 1).trim();
    if (!CONTRACT_CONSTANT.test(expr)) push(RULES.decimals, m.index);
  }

  // F-PREC-implicit-format — formatter con un solo argumento.
  for (const m of content.matchAll(/\b(formatMoneyWithSymbol|formatMoney)\s*\(/g)) {
    const before = content.slice(Math.max(0, m.index - 20), m.index);
    if (/function\s+$/.test(before)) continue; // definición
    if (countArgs(content, m.index + m[0].length - 1) === 1) push(RULES.implicitFormat, m.index);
  }

  // F-PREC-implicit-value — etiqueta sin precision ni decimals.
  for (const m of content.matchAll(new RegExp(`<(${VALUE_TAGS.join('|')})\\b`, 'g'))) {
    const tag = jsxTagText(content, m.index);
    if (!/\bprecision=/.test(tag) && !/\bdecimals=/.test(tag)) push(RULES.implicitValue, m.index);
  }

  for (const m of content.matchAll(/\.toFixed\s*\(/g)) push(RULES.toFixed, m.index);
  for (const m of content.matchAll(/\bIntl\s*\.\s*NumberFormat\b/g)) push(RULES.intl, m.index);
  for (const m of content.matchAll(/\bgetPrecisionPolicy\s*\(/g)) {
    const before = content.slice(Math.max(0, m.index - 20), m.index);
    if (/function\s+$/.test(before)) continue;
    push(RULES.policyRead, m.index);
  }

  return hits;
}

/** @param {{ file: string, rule: string, count: number }[]} entries */
function toAllowanceMap(entries) {
  const map = new Map();
  for (const e of entries ?? []) {
    const key = `${e.file}|${e.rule}`;
    map.set(key, (map.get(key) ?? 0) + e.count);
  }
  return map;
}

/** Archivos productivos en alcance (tests, fixtures e infraestructura de precisión fuera). */
export function isInScope(cfg, rel) {
  if (!cfg.scanGlobs.some((g) => matchGlob(g, rel))) return false;
  return !cfg.excludeGlobs.some((g) => matchGlob(g, rel));
}

/**
 * Aplica tolerancias exactas (deuda legacy + excepciones) a los hallazgos.
 * @param {ReturnType<typeof findPrecisionHits>} hits
 * @param {{ legacy: object[], exceptions: object[] }} allowances
 */
export function evaluateHits(hits, allowances, result = createCheckResult(CHECK_NAME)) {
  const allowed = toAllowanceMap([...(allowances.legacy ?? []), ...(allowances.exceptions ?? [])]);
  /** @type {Map<string, typeof hits>} */
  const byKey = new Map();
  for (const h of hits) {
    const key = `${h.file}|${h.rule}`;
    if (!byKey.has(key)) byKey.set(key, []);
    byKey.get(key).push(h);
  }
  for (const [key, list] of byKey) {
    const max = allowed.get(key) ?? 0;
    if (list.length <= max) continue;
    for (const h of list) {
      addViolation(result, {
        ...h,
        message: `${h.message} (${list.length} ocurrencia(s) en el archivo; tolerancia registrada ${max})`,
      });
    }
  }
  for (const [key, max] of allowed) {
    const actual = byKey.get(key)?.length ?? 0;
    if (actual < max) {
      const [file, rule] = key.split('|');
      addWarning(result, {
        rule,
        file,
        message: `tolerancia registrada ${max} > ocurrencias actuales ${actual} — bajar el baseline/excepción (no deja crecer deuda).`,
      });
    }
  }
  return result;
}

function scanRepo(cfg) {
  const hits = [];
  const files = walkFiles(path.join(REPO_ROOT, 'frontend/src'), { extensions: ['.ts', '.tsx'] });
  for (const abs of files) {
    const rel = toRepoRel(abs);
    if (!isInScope(cfg, rel)) continue;
    hits.push(...findPrecisionHits(rel, readText(rel)));
  }
  return hits;
}

export function runCheckFrontendPrecision() {
  const cfg = loadConfig('frontend-precision.json');
  const grandfather = loadGrandfather();
  return evaluateHits(scanRepo(cfg), {
    legacy: grandfather.frontendPrecisionGrandfathered,
    exceptions: cfg.exceptions,
  });
}

/**
 * Regenera frontendPrecisionGrandfathered SOLO para `legacyGlobs` (módulos cerrados), con las
 * ocurrencias actuales. Los módulos abiertos nunca se baselinan: se corrigen o se registran como
 * excepción justificada en config/frontend-precision.json.
 */
function writeBaseline() {
  const cfg = loadConfig('frontend-precision.json');
  const grandfather = loadGrandfather();
  const counts = new Map();
  for (const h of scanRepo(cfg)) {
    if (!cfg.legacyGlobs.some((g) => matchGlob(g, h.file))) continue;
    const key = `${h.file}|${h.rule}`;
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  const entries = [...counts.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([key, count]) => {
      const [file, rule] = key.split('|');
      return { file, rule, count };
    });
  // Reescribe SOLO la clave propia (el resto del archivo conserva su formato/diff).
  const gfPath = path.join(ARCH_DIR, 'architecture-grandfather.json');
  const text = fs.readFileSync(gfPath, 'utf8');
  const block = JSON.stringify(entries, null, 2).replace(/\n/g, '\n  ');
  const keyRe = /("frontendPrecisionGrandfathered":\s*)\[[\s\S]*?\n {2}\]/;
  const next = keyRe.test(text)
    ? text.replace(keyRe, `$1${block}`)
    : text.replace(/\n\}\s*$/, `,\n  "frontendPrecisionGrandfathered": ${block}\n}\n`);
  if (!('frontendPrecisionGrandfathered' in grandfather) && next === text) {
    throw new Error('frontend-precision: no se pudo insertar la clave en architecture-grandfather.json');
  }
  fs.writeFileSync(gfPath, next, 'utf8');
  console.log(`frontend-precision: wrote ${entries.length} legacy entr(ies) to ${toRepoRel(gfPath)}`);
}

if (process.argv[1]?.endsWith('check-frontend-precision.mjs')) {
  if (process.argv.includes('--write-baseline')) {
    writeBaseline();
  } else if (process.argv.includes('--list')) {
    const cfg = loadConfig('frontend-precision.json');
    for (const h of scanRepo(cfg)) console.log(`${h.file}:${h.line}\t${h.rule}`);
  } else {
    const result = runCheckFrontendPrecision();
    const { printCheckResult } = await import('./shared/report-utils.mjs');
    process.exit(printCheckResult(result) ? 0 : 1);
  }
}
