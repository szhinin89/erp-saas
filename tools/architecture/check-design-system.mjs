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
import { createCheckResult, addViolation } from './shared/report-utils.mjs';
import { blankComments } from './check-css-reachability.mjs';

export const CHECK_NAME = 'design-system';

const CSS_HEX_RULE = 'F-04-color';
const CSS_TOKEN_RULE = 'F-04-token';
const CSS_PRIMITIVE_RULE = 'F-04-primitive';
const CSS_RGB_RULE = 'F-04-rgb';
const CSS_MOTION_RULE = 'F-04-motion';
const TOKENS_DEF_PATH = 'frontend/src/styles/design-tokens.css';

/**
 * ZH Visual Discipline (ZH-DS-VISUAL-DISCIPLINE-01) — literales CSS con señal robusta y su
 * mensaje. Solo fuera de design-tokens.css y sin contar comentarios.
 *   F-04-primitive: primitiva de color (escala --color-neutral-N / --color-white) consumida
 *                   fuera de la capa de tokens — usar el token semántico.
 *   F-04-rgb:       rgb()/rgba() literal — usar var(--color-*) / color-mix sobre tokens.
 *   F-04-motion:    duración literal en transition/animation — usar --transition /
 *                   --motion-*. Exentas: animaciones en bucle (`infinite`: spinner,
 *                   skeleton) y el reset de prefers-reduced-motion (`0.01ms`).
 */
const CSS_LITERAL_RULES = [
  {
    rule: CSS_PRIMITIVE_RULE,
    re: /var\(\s*--color-(?:neutral-\d+|white)\s*\)/g,
    message: (m) => `Primitiva '${m}' fuera de design-tokens.css — consumir el token semántico (--color-text-*, --color-surface-*, --color-border*…).`,
  },
  {
    rule: CSS_RGB_RULE,
    re: /\brgba?\(/g,
    message: () => `Color rgb()/rgba() literal fuera de design-tokens.css — usar var(--color-*) o color-mix sobre tokens.`,
  },
  {
    rule: CSS_MOTION_RULE,
    // Declaración completa con una sola clase negada (lineal); la duración se valida en `skip`.
    re: /\b(?:transition|animation)(?:-duration)?:[^;{}]*/g,
    skip: (m) =>
      !/(?:^|[\s,:])\.?\d+(?:\.\d+)?m?s\b/.test(m) || /\binfinite\b/.test(m) || /\.01ms\b/.test(m),
    message: (m) => `Duración literal en '${m.replace(/\s+/g, ' ').trim().slice(0, 60)}' — usar var(--transition) / var(--motion-duration-*).`,
  },
];

/**
 * Reglas F-04-* que este check puede reportar sobre un archivo (repo-relativo posix).
 * Fuente única del alcance: la usa este check y check-grandfather-integrity.mjs.
 * @param {{ scanGlobs: string[], deprecatedClassPatterns: { rule: string }[], iconInlineStyleRule: string, cssHexExcludeFiles?: string[] }} cfg
 * @param {string} rel
 * @returns {Set<string>}
 */
export function designSystemRulesFor(cfg, rel) {
  const rules = new Set();
  if (!rel.startsWith('frontend/src/')) return rules;
  if (rel.endsWith('.tsx') && cfg.scanGlobs.some((g) => matchGlob(g, rel))) {
    for (const { rule } of cfg.deprecatedClassPatterns) rules.add(rule);
    rules.add(cfg.iconInlineStyleRule);
  }
  if (rel.endsWith('.css')) {
    if (!cfg.cssHexExcludeFiles?.includes(rel)) rules.add(CSS_HEX_RULE);
    if (rel !== TOKENS_DEF_PATH) {
      rules.add(CSS_TOKEN_RULE);
      for (const { rule } of CSS_LITERAL_RULES) rules.add(rule);
    }
    for (const { rule } of cfg.retiredCssSelectors ?? []) rules.add(rule);
  }
  return rules;
}

/**
 * Literales CSS de F-04-primitive/-rgb/-motion con allowance por conteo (ratchet sin
 * baseline): `allowances[rule][file] = N` congela la deuda histórica documentada de ese
 * archivo. Más de N → falla (deuda nueva); menos de N → falla pidiendo bajar el allowance,
 * así la deuda solo puede decrecer. Sin allowance, cada ocurrencia es una violación.
 * @param {Record<string, Record<string, number>>} allowances cfg.cssLiteralAllowances
 * @param {string} rel
 * @param {string} content
 */
export function findCssLiteralViolations(allowances, rel, content) {
  /** @type {{ rule: string, file: string, message: string, line: number }[]} */
  const found = [];
  if (rel === TOKENS_DEF_PATH) return found;
  // Comentarios → espacios (conserva offsets para el número de línea).
  const code = content.replace(/\/\*[\s\S]*?\*\//g, (c) => c.replace(/[^\n]/g, ' '));

  for (const { rule, re, skip, message } of CSS_LITERAL_RULES) {
    const hits = [];
    for (const m of code.matchAll(re)) {
      if (skip?.(m[0])) continue;
      hits.push({ rule, file: rel, message: message(m[0]), line: lineOf(code, m.index) });
    }
    const allowed = allowances?.[rule]?.[rel];
    if (allowed === undefined) {
      found.push(...hits);
    } else if (hits.length > allowed) {
      found.push({
        rule,
        file: rel,
        message: `${hits.length} literal(es) ${rule} con allowance ${allowed} — deuda nueva: usar tokens (primera ocurrencia nueva no identificable por línea; revisar el diff).`,
        line: hits[allowed]?.line ?? 1,
      });
    } else if (hits.length < allowed) {
      found.push({
        rule,
        file: rel,
        message: `Allowance ${rule} desactualizado: ${hits.length} literal(es) < ${allowed} — bajar cssLiteralAllowances a ${hits.length} (ratchet).`,
        line: 1,
      });
    }
  }
  return found;
}

/**
 * Allowances que apuntan a archivos inexistentes no eximen nada pero dejan un hueco si el
 * archivo reaparece (mismo criterio que check-grandfather-integrity).
 * @param {Record<string, Record<string, number>>} allowances
 * @param {(rel: string) => boolean} fileExists
 */
export function findStaleLiteralAllowances(allowances, fileExists) {
  const found = [];
  for (const [rule, files] of Object.entries(allowances ?? {})) {
    if (rule.startsWith('$')) continue; // claves de documentación ($note)
    for (const rel of Object.keys(files)) {
      if (!fileExists(rel)) {
        found.push({
          rule,
          file: 'tools/architecture/config/design-system.json',
          message: `cssLiteralAllowances.${rule} apunta a '${rel}', que no existe — eliminar la entrada.`,
          line: 1,
        });
      }
    }
  }
  return found;
}

/**
 * Paquete C5 — pestañas manuales y controles nativos con equivalente oficial.
 *
 *   F-04-tabs-manual     clase `prd-tab-btn` en TSX fuera de <ZHTabBar> (components/zh) — usar <ZHTabBar>.
 *   F-04-native-select   <select> nativo — usar <ZhSelect>.
 *   F-04-native-textarea <textarea> nativo — usar <ZhTextarea>.
 *   F-04-native-input    <input> de texto/email/password/search/tel/url/number/fecha — usar
 *                        <ZhTextInput type=…>, <ZhDateInput>, <ZhNumberInput>/<ZhDecimalInput>.
 *   F-04-native-checkbox <input type="checkbox"> — usar <ZhCheckbox>.
 *
 * Sin equivalente oficial (no se marcan): radio (patrón `.zh-radio-*`), file, color, range, hidden.
 * Fuera de alcance: components/zh/** (internos del DS) y archivos de prueba.
 *
 * Excepciones: `nativeControlAllowances[rule][archivo] = { count, reason }` (línea base por
 * archivo, conteo exacto y motivo obligatorio). Más que `count` → falla (deuda nueva); menos →
 * falla pidiendo bajar el conteo (la deuda solo decrece); sin `reason` o con archivo inexistente → falla.
 */
export const NATIVE_CONTROL_RULES = [
  'F-04-tabs-manual',
  'F-04-native-select',
  'F-04-native-textarea',
  'F-04-native-input',
  'F-04-native-checkbox',
];

const NATIVE_INPUT_TEXT_LIKE = new Set([
  'text', 'email', 'password', 'search', 'tel', 'url', 'number', 'date', 'datetime-local', 'time', 'month', 'week',
]);

const NATIVE_MESSAGES = {
  'F-04-tabs-manual': "Pestañas manuales ('.prd-tab-btn') — usar <ZHTabBar>.",
  'F-04-native-select': '<select> nativo — usar <ZhSelect>.',
  'F-04-native-textarea': '<textarea> nativo — usar <ZhTextarea>.',
  'F-04-native-input': '<input> nativo de texto/fecha — usar <ZhTextInput type="…">, <ZhDateInput> o <ZhNumberInput>/<ZhDecimalInput>.',
  'F-04-native-checkbox': '<input type="checkbox"> nativo — usar <ZhCheckbox>.',
};

/** Alcance de las reglas nativas: TSX de producción fuera de los internos del DS. */
export function isNativeControlScope(rel) {
  return (
    rel.endsWith('.tsx') &&
    !/\.(test|spec)\.tsx$/.test(rel) &&
    !rel.startsWith('frontend/src/components/zh/')
  );
}

/** Índice del `>` que cierra la etiqueta JSX que empieza en `start` (respeta llaves y comillas). */
function jsxTagEnd(code, start) {
  let depth = 0;
  let quote = null;
  for (let i = start; i < code.length; i++) {
    const ch = code[i];
    if (quote) {
      if (ch === quote) quote = null;
    } else if ((ch === '"' || ch === "'") && depth === 0) {
      quote = ch;
    } else if (ch === '{') depth++;
    else if (ch === '}') depth--;
    else if (ch === '>' && depth === 0) return i;
  }
  return code.length - 1;
}

/** Ocurrencias de las reglas nativas en un TSX (sin comentarios). */
export function nativeControlHits(content) {
  const code = blankComments(content);
  /** @type {{ rule: string, line: number }[]} */
  const hits = [];
  for (const m of code.matchAll(/<(select|textarea|input)\b/g)) {
    const tag = code.slice(m.index, jsxTagEnd(code, m.index) + 1);
    const line = lineOf(code, m.index);
    if (m[1] === 'select') hits.push({ rule: 'F-04-native-select', line });
    else if (m[1] === 'textarea') hits.push({ rule: 'F-04-native-textarea', line });
    else {
      const typeAttr = /\stype=(?:"([^"]*)"|\{)/.exec(tag);
      const type = typeAttr ? (typeAttr[1] ?? '{dinámico}') : 'text';
      if (type === 'checkbox') hits.push({ rule: 'F-04-native-checkbox', line });
      else if (type === '{dinámico}' || NATIVE_INPUT_TEXT_LIKE.has(type)) hits.push({ rule: 'F-04-native-input', line });
    }
  }
  for (const m of code.matchAll(/(?<![\w-])prd-tab-btn(?![\w-])/g)) {
    hits.push({ rule: 'F-04-tabs-manual', line: lineOf(code, m.index) });
  }
  return hits;
}

/**
 * @param {Record<string, Record<string, { count: number, reason: string }>>} allowances cfg.nativeControlAllowances
 * @param {string} rel
 * @param {string} content
 */
export function findNativeControlViolations(allowances, rel, content) {
  /** @type {{ rule: string, file: string, message: string, line: number }[]} */
  const found = [];
  if (!isNativeControlScope(rel)) return found;
  const all = nativeControlHits(content);
  for (const rule of NATIVE_CONTROL_RULES) {
    const hits = all.filter((h) => h.rule === rule);
    const allowed = allowances?.[rule]?.[rel]?.count;
    if (allowed === undefined) {
      for (const h of hits) found.push({ rule, file: rel, message: NATIVE_MESSAGES[rule], line: h.line });
    } else if (hits.length > allowed) {
      found.push({
        rule,
        file: rel,
        message: `${hits.length} ocurrencia(s) ${rule} con excepción de ${allowed} — deuda nueva: ${NATIVE_MESSAGES[rule]}`,
        line: hits[allowed]?.line ?? 1,
      });
    } else if (hits.length < allowed) {
      found.push({
        rule,
        file: rel,
        message: `Excepción ${rule} desactualizada: ${hits.length} ocurrencia(s) < ${allowed} — bajar nativeControlAllowances a ${hits.length} (o eliminarla si es 0).`,
        line: 1,
      });
    }
  }
  return found;
}

/** Entradas de excepción sin motivo/conteo válido o que apuntan a archivos inexistentes. */
export function findInvalidNativeAllowances(allowances, fileExists) {
  const found = [];
  const cfgFile = 'tools/architecture/config/design-system.json';
  for (const [rule, files] of Object.entries(allowances ?? {})) {
    if (rule.startsWith('$')) continue;
    if (!NATIVE_CONTROL_RULES.includes(rule)) {
      found.push({ rule, file: cfgFile, message: `nativeControlAllowances.${rule}: regla desconocida.`, line: 1 });
      continue;
    }
    for (const [rel, entry] of Object.entries(files)) {
      if (!fileExists(rel)) {
        found.push({ rule, file: cfgFile, message: `nativeControlAllowances.${rule} apunta a '${rel}', que no existe — eliminar la entrada.`, line: 1 });
      } else if (!Number.isInteger(entry?.count) || entry.count < 1 || typeof entry?.reason !== 'string' || entry.reason.trim().length < 8) {
        found.push({ rule, file: cfgFile, message: `nativeControlAllowances.${rule}['${rel}'] requiere { count >= 1, reason } con motivo explícito.`, line: 1 });
      }
    }
  }
  return found;
}

/** @param {object} grandfather @returns {Map<string, Set<string>>} */
function buildGrandfatherMap(grandfather) {
  const map = new Map();
  for (const entry of grandfather.designSystemGrandfathered ?? []) {
    map.set(entry.file, new Set(entry.rules));
  }
  return map;
}

/** @param {string} content @param {number} index */
function lineOf(content, index) {
  return content.slice(0, index).split('\n').length;
}

/**
 * Sustituye comentarios (bloque y de línea) por espacios, conservando saltos de línea para que
 * offsets y números de línea no cambien. Sirve a las reglas que prohíben USOS de una clase y no
 * deben fallar por referencias históricas en comentarios. Un doble slash precedido por ':' (URL)
 * no es comentario.
 * @param {string} content
 */
export function stripComments(content) {
  const blank = (m) => m.replace(/[^\n]/g, ' ');
  return content
    .replace(/\/\*[\s\S]*?\*\//g, blank)
    .replace(/(^|[^:\w"'`])(\/\/[^\n]*)/g, (_, pre, c) => pre + blank(c));
}

/**
 * Selectores CSS retirados (`cfg.retiredCssSelectors`): una declaración activa de la clase en un
 * .css es violación; los comentarios no cuentan.
 * @param {{ retiredCssSelectors?: { rule: string, pattern: string, message: string }[] }} cfg
 * @param {string} rel
 * @param {string} content
 */
export function findRetiredCssSelectorViolations(cfg, rel, content) {
  /** @type {{ rule: string, file: string, message: string, line: number }[]} */
  const found = [];
  const code = content.replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, ' '));
  for (const { rule, pattern, message } of cfg.retiredCssSelectors ?? []) {
    const re = new RegExp(pattern, 'g');
    let m;
    while ((m = re.exec(code)) !== null) {
      found.push({ rule, file: rel, message, line: lineOf(code, m.index) });
    }
  }
  return found;
}

/**
 * @param {{ scanGlobs: string[], deprecatedClassPatterns: { rule: string, pattern: string, message: string }[], iconInlineStyleRule: string, iconInlineStyleMessage: string }} cfg
 * @param {string} rel
 * @param {string} content
 */
export function findViolations(cfg, rel, content) {
  /** @type {{ rule: string, file: string, message: string, line: number }[]} */
  const found = [];
  const code = stripComments(content);

  for (const { rule, pattern, message, ignoreComments } of cfg.deprecatedClassPatterns) {
    const source = ignoreComments ? code : content;
    const re = new RegExp(pattern, 'g');
    let m;
    while ((m = re.exec(source)) !== null) {
      found.push({ rule, file: rel, message, line: lineOf(source, m.index) });
    }
  }

  const lines = content.split('\n');
  const iconStyleRe = /style=\{\{[^}]*fontSize/;
  lines.forEach((lineText, i) => {
    if (lineText.includes('material-symbols-outlined') && iconStyleRe.test(lineText)) {
      found.push({
        rule: cfg.iconInlineStyleRule,
        file: rel,
        message: cfg.iconInlineStyleMessage,
        line: i + 1,
      });
    }
  });

  return found;
}

/**
 * Colores hexadecimales fuera de design-tokens.css (F-04-color).
 * design-tokens.css es la única fuente autorizada de valores hex — todo lo
 * demás debe consumir var(--color-*). `color-mix(..., #000)` / `#fff` como
 * operando de mezcla no está exceptuado a propósito: si aparece, se corrige
 * usando var(--color-on-primary) o el token semántico correspondiente.
 * @param {{ cssHexExcludeFiles: string[] }} cfg
 * @param {string} rel
 * @param {string} content
 */
function findCssHexViolations(cfg, rel, content) {
  /** @type {{ rule: string, file: string, message: string, line: number }[]} */
  const found = [];
  if (cfg.cssHexExcludeFiles?.includes(rel)) return found;

  const hexRe = /#[0-9a-fA-F]{3,8}\b/g;
  let m;
  while ((m = hexRe.exec(content)) !== null) {
    found.push({
      rule: CSS_HEX_RULE,
      file: rel,
      message: `Color hexadecimal '${m[0]}' fuera de design-tokens.css — usar var(--color-*).`,
      line: lineOf(content, m.index),
    });
  }
  return found;
}

/**
 * Tokens CSS inexistentes (F-04-token), p. ej. var(--space-16) cuando la
 * escala termina en --space-10. Construye el set de tokens reales a partir
 * de design-tokens.css y marca cualquier var(--x) fuera de ese set.
 * @param {string} tokensDefPath ruta repo-relativa de design-tokens.css
 * @returns {Set<string>}
 */
function buildDefinedTokenSet(tokensDefPath) {
  const content = readText(tokensDefPath);
  const defined = new Set();
  const defRe = /--([a-zA-Z0-9-]+)\s*:/g;
  let m;
  while ((m = defRe.exec(content)) !== null) defined.add(m[1]);
  return defined;
}

/**
 * Custom properties declaradas (`--x:`) en el propio archivo CSS, ignorando comentarios.
 * Son locales al archivo: una declaración en OTRO .css nunca habilita su uso aquí.
 * @param {string} content
 * @returns {Set<string>}
 */
export function collectLocalCustomProperties(content) {
  const withoutComments = content.replace(/\/\*[\s\S]*?\*\//g, '');
  const declared = new Set();
  for (const [, name] of withoutComments.matchAll(/(?<![\w-])--([a-zA-Z0-9-]+)\s*:/g)) {
    declared.add(name);
  }
  return declared;
}

/**
 * Custom properties dinámicas que un componente TS/TSX fija en runtime
 * (`style={{ "--x": valor }}` o `el.style.setProperty("--x", valor)`), asociadas SOLO a los .css
 * que ese mismo componente importa con ruta relativa. Así el valor dinámico queda limitado al
 * componente dueño del CSS — sin allowlist por archivo ni wildcard.
 * @param {{ rel: string, content: string }[]} sourceFiles
 * @returns {Map<string, Set<string>>} css repo-relativo → nombres (sin `--`)
 */
export function collectDynamicCustomProperties(sourceFiles) {
  /** @type {Map<string, Set<string>>} */
  const byCss = new Map();
  for (const { rel, content } of sourceFiles) {
    const names = [
      ...content.matchAll(/["']--([a-zA-Z0-9-]+)["']\s*:/g),
      ...content.matchAll(/setProperty\(\s*["']--([a-zA-Z0-9-]+)["']/g),
    ].map(([, name]) => name);
    if (names.length === 0) continue;

    // El frontend importa CSS solo como side-effect (`import "./x.css";`).
    for (const [, spec] of content.matchAll(/import\s+["'](\.{1,2}\/[^"']+\.css)["']/g)) {
      const cssRel = path.posix.normalize(path.posix.join(path.posix.dirname(rel), spec));
      const set = byCss.get(cssRel) ?? new Set();
      for (const name of names) set.add(name);
      byCss.set(cssRel, set);
    }
  }
  return byCss;
}

/**
 * Un var(--x) es válido si --x existe en design-tokens.css, está declarado en el mismo archivo
 * (custom property local) o es una custom property dinámica fijada por un componente que
 * importa este archivo. Cualquier otro caso (typo, token inexistente, local de otro .css) falla.
 * @param {Set<string>} definedTokens
 * @param {string} rel
 * @param {string} content
 * @param {Set<string>} [dynamicProperties]
 */
export function findUndefinedTokenViolations(definedTokens, rel, content, dynamicProperties = new Set()) {
  /** @type {{ rule: string, file: string, message: string, line: number }[]} */
  const found = [];
  const localProperties = collectLocalCustomProperties(content);
  const varRe = /var\(\s*--([a-zA-Z0-9-]+)/g;
  let m;
  while ((m = varRe.exec(content)) !== null) {
    const name = m[1];
    if (definedTokens.has(name) || localProperties.has(name) || dynamicProperties.has(name)) continue;
    found.push({
      rule: CSS_TOKEN_RULE,
      file: rel,
      message: `Token '--${name}' no está definido en design-tokens.css — corregir el nombre o agregarlo ahí (no crear tokens sueltos en otros archivos).`,
      line: lineOf(content, m.index),
    });
  }
  return found;
}

export function runCheckDesignSystem() {
  const cfg = loadConfig('design-system.json');
  const grandfather = loadGrandfather();
  const gfMap = buildGrandfatherMap(grandfather);
  const result = createCheckResult(CHECK_NAME);

  const tsxFiles = walkFiles(path.join(REPO_ROOT, 'frontend/src'), { extensions: ['.tsx'] });
  for (const abs of tsxFiles) {
    const rel = toRepoRel(abs);
    if (!cfg.scanGlobs.some((g) => matchGlob(g, rel))) continue;

    const content = readText(rel);
    const allowed = gfMap.get(rel);

    for (const v of findViolations(cfg, rel, content)) {
      if (allowed?.has(v.rule)) continue;
      addViolation(result, v);
    }
    for (const v of findNativeControlViolations(cfg.nativeControlAllowances, rel, content)) addViolation(result, v);
  }

  for (const v of findInvalidNativeAllowances(cfg.nativeControlAllowances, (rel) =>
    fs.existsSync(path.join(REPO_ROOT, rel)),
  )) {
    addViolation(result, v);
  }

  const definedTokens = buildDefinedTokenSet(TOKENS_DEF_PATH);
  const dynamicByCss = collectDynamicCustomProperties(
    walkFiles(path.join(REPO_ROOT, 'frontend/src'), { extensions: ['.ts', '.tsx'] }).map((abs) => {
      const rel = toRepoRel(abs);
      return { rel, content: readText(rel) };
    }),
  );

  const cssFiles = walkFiles(path.join(REPO_ROOT, 'frontend/src'), { extensions: ['.css'] });
  for (const abs of cssFiles) {
    const rel = toRepoRel(abs);
    const content = readText(rel);
    const allowed = gfMap.get(rel);
    const cssViolations = [
      ...findCssHexViolations(cfg, rel, content),
      ...(rel === TOKENS_DEF_PATH
        ? []
        : findUndefinedTokenViolations(definedTokens, rel, content, dynamicByCss.get(rel))),
      ...findCssLiteralViolations(cfg.cssLiteralAllowances, rel, content),
      ...findRetiredCssSelectorViolations(cfg, rel, content),
    ];
    for (const v of cssViolations) {
      if (!allowed?.has(v.rule)) addViolation(result, v);
    }
  }

  for (const v of findStaleLiteralAllowances(cfg.cssLiteralAllowances, (rel) =>
    fs.existsSync(path.join(REPO_ROOT, rel)),
  )) {
    addViolation(result, v);
  }

  return result;
}

/** Regenerates designSystemGrandfathered with the current violations (ignoring any existing baseline). */
function writeBaseline() {
  const cfg = loadConfig('design-system.json');
  const grandfather = loadGrandfather();
  const files = walkFiles(path.join(REPO_ROOT, 'frontend/src'), { extensions: ['.tsx'] });

  /** @type {Map<string, Set<string>>} */
  const byFile = new Map();
  for (const abs of files) {
    const rel = toRepoRel(abs);
    if (!cfg.scanGlobs.some((g) => matchGlob(g, rel))) continue;

    const content = readText(rel);
    for (const v of findViolations(cfg, rel, content)) {
      if (!byFile.has(rel)) byFile.set(rel, new Set());
      byFile.get(rel).add(v.rule);
    }
  }

  const entries = [...byFile.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([file, rules]) => ({ file, rules: [...rules].sort() }));

  grandfather.designSystemGrandfathered = entries;

  const gfPath = path.join(ARCH_DIR, 'architecture-grandfather.json');
  fs.writeFileSync(gfPath, `${JSON.stringify(grandfather, null, 2)}\n`, 'utf8');
  console.log(`design-system: wrote ${entries.length} grandfathered file(s) to ${toRepoRel(gfPath)}`);
}

if (process.argv[1]?.endsWith('check-design-system.mjs')) {
  if (process.argv.includes('--write-baseline')) {
    writeBaseline();
  } else {
    const result = runCheckDesignSystem();
    const { printCheckResult } = await import('./shared/report-utils.mjs');
    process.exit(printCheckResult(result) ? 0 : 1);
  }
}
