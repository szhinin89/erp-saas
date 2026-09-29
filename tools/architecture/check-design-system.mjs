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

export const CHECK_NAME = 'design-system';

const CSS_HEX_RULE = 'F-04-color';
const CSS_TOKEN_RULE = 'F-04-token';
const TOKENS_DEF_PATH = 'frontend/src/styles/design-tokens.css';

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
    if (rel !== TOKENS_DEF_PATH) rules.add(CSS_TOKEN_RULE);
  }
  return rules;
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
 * @param {{ scanGlobs: string[], deprecatedClassPatterns: { rule: string, pattern: string, message: string }[], iconInlineStyleRule: string, iconInlineStyleMessage: string }} cfg
 * @param {string} rel
 * @param {string} content
 */
function findViolations(cfg, rel, content) {
  /** @type {{ rule: string, file: string, message: string, line: number }[]} */
  const found = [];

  for (const { rule, pattern, message } of cfg.deprecatedClassPatterns) {
    const re = new RegExp(pattern, 'g');
    let m;
    while ((m = re.exec(content)) !== null) {
      found.push({ rule, file: rel, message, line: lineOf(content, m.index) });
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

    for (const v of findCssHexViolations(cfg, rel, content)) {
      if (allowed?.has(v.rule)) continue;
      addViolation(result, v);
    }

    if (rel !== TOKENS_DEF_PATH) {
      for (const v of findUndefinedTokenViolations(definedTokens, rel, content, dynamicByCss.get(rel))) {
        if (allowed?.has(v.rule)) continue;
        addViolation(result, v);
      }
    }
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
