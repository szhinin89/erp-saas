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
      rule: 'F-04-color',
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
 * @param {Set<string>} definedTokens
 * @param {string} rel
 * @param {string} content
 */
function findUndefinedTokenViolations(definedTokens, rel, content) {
  /** @type {{ rule: string, file: string, message: string, line: number }[]} */
  const found = [];
  const varRe = /var\(\s*--([a-zA-Z0-9-]+)/g;
  let m;
  while ((m = varRe.exec(content)) !== null) {
    const name = m[1];
    if (definedTokens.has(name)) continue;
    found.push({
      rule: 'F-04-token',
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

  const tokensDefPath = 'frontend/src/styles/design-tokens.css';
  const definedTokens = buildDefinedTokenSet(tokensDefPath);

  const cssFiles = walkFiles(path.join(REPO_ROOT, 'frontend/src'), { extensions: ['.css'] });
  for (const abs of cssFiles) {
    const rel = toRepoRel(abs);
    const content = readText(rel);
    const allowed = gfMap.get(rel);

    for (const v of findCssHexViolations(cfg, rel, content)) {
      if (allowed?.has(v.rule)) continue;
      addViolation(result, v);
    }

    if (rel !== tokensDefPath) {
      for (const v of findUndefinedTokenViolations(definedTokens, rel, content)) {
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
