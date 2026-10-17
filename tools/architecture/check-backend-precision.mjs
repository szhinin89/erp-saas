import path from 'node:path';
import { REPO_ROOT, loadConfig, toRepoRel, walkFiles, readText, matchGlob } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation, addWarning } from './shared/report-utils.mjs';
import { stripCommentsKeepLines } from './check-frontend-precision.mjs';

/**
 * B-PREC — guard de precisión decimal en backend. Canónico (SSOT única):
 * docs/architecture/data-standards.md § Estándar de Precisión Numérica.
 *
 * La precisión funcional se lee de la política única (`ICompanyPrecisionPolicyProvider` →
 * `EffectivePrecisionPolicyDto`); las escalas fijas del sistema se nombran por su constante
 * (`FiscalPrecision.*`, `WarehousePrecision`, …). El guard detecta en código productivo:
 *   B-PREC-literal-round   `Math.Round`/`decimal.Round` con escala literal (`, 2`, `, 4`, …).
 *   B-PREC-round-helper    declaración de un helper de redondeo propio (`Round2`, `RoundAmount`, …).
 *   B-PREC-fiscal-default  parámetro con default `FiscalPrecision.Quantity|UnitCost` (campo
 *                          configurable por empresa: el valor debe llegar de la política).
 *
 * Trinquete: la deuda existente se registra en config/backend-precision.json → exceptions
 * (archivo + regla + conteo exacto + reason). Superar el conteo → violación; quedar por debajo →
 * aviso para bajar la excepción. No detecta "faltó usar el provider" (no es fiable por texto).
 */

export const CHECK_NAME = 'backend-precision';
export const SSOT = 'docs/architecture/data-standards.md';

export const RULES = {
  literalRound: 'B-PREC-literal-round',
  roundHelper: 'B-PREC-round-helper',
  fiscalDefault: 'B-PREC-fiscal-default',
};

const MESSAGES = {
  [RULES.literalRound]:
    'redondeo con escala literal — usar el campo de la política (`policy.<Campo>Decimals`) o la constante fija nombrada.',
  [RULES.roundHelper]:
    'helper de redondeo propio — redondear con `Math.Round(valor, policy.<Campo>Decimals, MidpointRounding.AwayFromZero)`.',
  [RULES.fiscalDefault]:
    'default `FiscalPrecision.*` para un campo configurable — Application debe pasar el valor de la política.',
};

function lineOf(content, index) {
  return content.slice(0, index).split('\n').length;
}

/** Argumentos de primer nivel de la llamada cuyo `(` está en `open`. */
function callArgs(content, open) {
  let depth = 0;
  let current = '';
  const args = [];
  for (let j = open; j < content.length; j++) {
    const c = content[j];
    if (c === '(' || c === '{' || c === '[') {
      depth++;
      if (depth === 1) continue;
    } else if (c === ')' || c === '}' || c === ']') {
      depth--;
      if (depth === 0) {
        if (current.trim()) args.push(current.trim());
        return args;
      }
    } else if (c === ',' && depth === 1) {
      args.push(current.trim());
      current = '';
      continue;
    }
    current += c;
  }
  return args;
}

/**
 * Hallazgos B-PREC de un archivo C# (sin aplicar excepciones).
 * @param {string} rel @param {string} raw
 * @returns {{ rule: string, file: string, line: number, message: string }[]}
 */
export function findBackendPrecisionHits(rel, raw) {
  const content = stripCommentsKeepLines(raw);
  const hits = [];
  const push = (rule, index) =>
    hits.push({ rule, file: rel, line: lineOf(content, index), message: `${MESSAGES[rule]} SSOT: ${SSOT}` });

  for (const m of content.matchAll(/\b(?:Math|decimal|Decimal)\s*\.\s*Round\s*\(/g)) {
    const args = callArgs(content, m.index + m[0].length - 1);
    if (args.length >= 2 && /^\d+$/.test(args[1])) push(RULES.literalRound, m.index);
  }

  for (const m of content.matchAll(/\bdecimal\??\s+(Round\d+|Round(?:Amount|Money|Currency|Monetary|Fiscal))\s*\(/g))
    push(RULES.roundHelper, m.index);

  for (const m of content.matchAll(/\bint\s+\w+\s*=\s*FiscalPrecision\s*\.\s*(Quantity|UnitCost)\s*[,)]/g))
    push(RULES.fiscalDefault, m.index);

  return hits;
}

/** Archivos productivos en alcance (tests, migraciones y binarios fuera). */
export function isInScope(cfg, rel) {
  if (!cfg.scanGlobs.some((g) => matchGlob(g, rel))) return false;
  return !cfg.excludeGlobs.some((g) => matchGlob(g, rel));
}

function toAllowanceMap(entries) {
  const map = new Map();
  for (const e of entries ?? []) {
    const key = `${e.file}|${e.rule}`;
    map.set(key, (map.get(key) ?? 0) + e.count);
  }
  return map;
}

/**
 * Aplica las excepciones auditadas (conteo exacto) a los hallazgos.
 * @param {ReturnType<typeof findBackendPrecisionHits>} hits
 * @param {{ exceptions: object[] }} allowances
 */
export function evaluateHits(hits, allowances, result = createCheckResult(CHECK_NAME)) {
  const allowed = toAllowanceMap(allowances.exceptions ?? []);
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
        message: `${h.message} (${list.length} ocurrencia(s) en el archivo; excepción registrada ${max})`,
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
        message: `excepción registrada ${max} > ocurrencias actuales ${actual} — bajar la excepción. SSOT: ${SSOT}`,
      });
    }
  }
  return result;
}

function scanRepo(cfg) {
  const hits = [];
  const files = walkFiles(path.join(REPO_ROOT, 'backend/src'), {
    extensions: ['.cs'],
    skipDirNames: ['bin', 'obj', 'Migrations', 'node_modules', '.git'],
  });
  for (const abs of files) {
    const rel = toRepoRel(abs);
    if (!isInScope(cfg, rel)) continue;
    hits.push(...findBackendPrecisionHits(rel, readText(rel)));
  }
  return hits;
}

export function runCheckBackendPrecision() {
  const cfg = loadConfig('backend-precision.json');
  return evaluateHits(scanRepo(cfg), { exceptions: cfg.exceptions });
}

if (process.argv[1]?.endsWith('check-backend-precision.mjs')) {
  if (process.argv.includes('--list')) {
    const cfg = loadConfig('backend-precision.json');
    for (const h of scanRepo(cfg)) console.log(`${h.file}:${h.line}\t${h.rule}`);
  } else {
    const result = runCheckBackendPrecision();
    const { printCheckResult } = await import('./shared/report-utils.mjs');
    process.exit(printCheckResult(result) ? 0 : 1);
  }
}
