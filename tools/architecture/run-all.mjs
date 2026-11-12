#!/usr/bin/env node
import path from 'node:path';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
import { runCheckPagesWrapper } from './check-pages-wrapper.mjs';
import { runCheckImportBoundaries } from './check-import-boundaries.mjs';
import { runCheckModuleBoundaries } from './check-module-boundaries.mjs';
import { runCheckCssPrefixes } from './check-css-prefixes.mjs';
import { runCheckDesignSystem } from './check-design-system.mjs';
import { runCheckCssReachability } from './check-css-reachability.mjs';
import { runCheckFrontendPrecision } from './check-frontend-precision.mjs';
import { runCheckBackendPrecision } from './check-backend-precision.mjs';
import { runCheckFrontendDateTime } from './check-frontend-datetime.mjs';
import { runCheckNoCrossLayer } from './check-no-cross-layer.mjs';
import { runCheckPlatformLegacySurface } from './check-platform-legacy-surface.mjs';
import { runCheckFrontendSubscriberNaming } from './check-frontend-subscriber-naming.mjs';
import { runCheckFrontendHttpAccess } from './check-frontend-http-access.mjs';
import { runCheckBackendLayering } from './check-backend-layering.mjs';
import { runCheckBackendCleanArchitecture } from './check-backend-clean-architecture.mjs';
import { runCheckBackendControllerThin } from './check-backend-controller-thin.mjs';
import { runCheckBackendSubscriberRules } from './check-backend-subscriber-rules.mjs';
import { runCheckPermissionsAuthorizationRules } from './check-permissions-authorization-rules.mjs';
import { runCheckFrontendPermissionsRules } from './check-frontend-permissions-rules.mjs';
import { runCheckDuplicateServices } from './check-duplicate-services.mjs';
import { runCheckNamingConventions } from './check-naming-conventions.mjs';
import { runCheckI18nKeys } from './check-i18n-keys.mjs';
import { runCheckGrandfatherIntegrity } from './check-grandfather-integrity.mjs';
import { calculateArchitectureScore } from './calculate-score.mjs';
import { emitGithubAnnotations } from './github-annotations.mjs';
import { createCheckResult, addViolation, toJsonReport, writeJsonReport } from './shared/report-utils.mjs';
import { loadArchitectureBaseline, compareArchitectureBaseline } from './ratchet.mjs';
import {
  formatConsoleCheck,
  formatConsoleSummary,
  formatRatchetCheck,
  formatRatchetSummary,
} from './formatters/console-formatter.mjs';

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const DOCUMENT_ENTRIES = [
  'AGENTS.md', 'CLAUDE.md', 'ARCHITECTURE_GATES.md', 'CONTEXT.md', 'CONTRIBUTING.md',
  'ERP_CORE_FREEZE.md', 'README.md', 'FEATURES.md', 'STATUS.md',
  'backend/CLAUDE.md', 'frontend/CLAUDE.md', '.github/INSTRUCCIONES-COPILOT.md',
  'docs/ARCHITECTURE-RULES.md', 'docs/DEVELOPMENT.md', 'docs/agent/WORK_QUEUE.md',
  'tools/architecture/README.md',
  '.cursor/rules/erp-unified-rules.mdc', '.cursor/rules/rules-consolidated-map.mdc',
];

// Mantener el control documental en el runner existente: sin red, dependencias ni jobs nuevos.
function prose(source) {
  let fence = null;
  return source.replace(/<!--[^]*?-->/g, (value) => value.replace(/[^\n]/g, ' '))
    .split('\n').map((line) => {
      const marker = line.match(/^ {0,3}(`{3,}|~{3,})/);
      if (marker && !fence) { fence = marker[1]; return ''; }
      if (fence) {
        if (new RegExp(`^ {0,3}${fence[0]}{${fence.length},}\\s*$`).test(line)) fence = null;
        return '';
      }
      return line;
    }).join('\n');
}

function headingIds(source) {
  const text = prose(source);
  const ids = new Set();
  const used = new Set();
  for (const match of text.matchAll(/\b(?:id|name)=["']([^"']+)["']/g)) ids.add(match[1]);
  const lines = text.split(/\r?\n/);
  for (let i = 0; i < lines.length; i++) {
    const atx = lines[i].match(/^ {0,3}#{1,6}\s+(.+?)(?:\s+#+)?\s*$/);
    const setext = i + 1 < lines.length && /^ {0,3}(?:=+|-+)\s*$/.test(lines[i + 1]) && lines[i].trim();
    if (!atx && !setext) continue;
    const title = (atx ? atx[1] : lines[i]).replace(/!?\[([^\]]*)\]\([^)]*\)/g, '$1')
      .replace(/<[^>]*>/g, '').replace(/&amp;/g, '&');
    const base = title.toLowerCase().replace(/[^\p{L}\p{M}\p{N}_\- ]/gu, '').replace(/ /g, '-');
    let slug = base;
    for (let n = 1; used.has(slug); n++) slug = `${base}-${n}`;
    used.add(slug); ids.add(slug);
    if (setext && !atx) i++;
  }
  return ids;
}

function markdownLinks(source) {
  const text = prose(source).replace(/(`+)([^]*?)\1/g, (value) => value.replace(/[^\n]/g, ' '));
  const links = [];
  // Destinos con espacios entre <>, títulos opcionales y paréntesis anidados en rutas.
  const destination = String.raw`(<[^>\n]+>|(?:\\.|[^\s()\\]|\((?:\\.|[^()\\]|\([^()]*\))*\))+)`;
  const inline = new RegExp(String.raw`!?\[(?:\\.|[^\]\\\n])*\]\(\s*${destination}(?:\s+["'][^\n]*?["'])?\s*\)`, 'g');
  for (const match of text.matchAll(inline)) {
    links.push({ url: match[1].replace(/^<|>$/g, ''), index: match.index });
  }
  const definitions = new Map();
  for (const match of text.matchAll(new RegExp(String.raw`^ {0,3}\[([^\]]+)\]:\s*${destination}`, 'gm'))) {
    definitions.set(match[1].trim().replace(/\s+/g, ' ').toLowerCase(), match[2].replace(/^<|>$/g, ''));
  }
  for (const match of text.matchAll(/!?\[([^\]\n]+)\](?:\[([^\]\n]*)\])?(?!\()/g)) {
    const label = (match[2] || match[1]).trim().replace(/\s+/g, ' ').toLowerCase();
    if (definitions.has(label)) links.push({ url: definitions.get(label), index: match.index });
  }
  return links.map((link) => ({ ...link, line: text.slice(0, link.index).split('\n').length }));
}

function existsExact(root, relative) {
  let current = root;
  for (const part of relative.split('/').filter(Boolean)) {
    if (!fs.existsSync(current) || !fs.statSync(current).isDirectory() || !fs.readdirSync(current).includes(part)) return false;
    current = path.join(current, part);
  }
  return fs.existsSync(current);
}

export function runCheckDocumentation({ root = REPO_ROOT, files, adapters } = {}) {
  const result = createCheckResult('documentation');
  const list = (dir, extension) => fs.existsSync(path.join(root, dir))
    ? fs.readdirSync(path.join(root, dir)).filter((name) => name.endsWith(extension)).map((name) => `${dir}/${name}`) : [];
  const cursor = list('.cursor/rules', '.mdc');
  files ??= [...DOCUMENT_ENTRIES, ...list('docs/architecture', '.md'), ...cursor];
  adapters ??= ['AGENTS.md', 'backend/CLAUDE.md', 'frontend/CLAUDE.md', '.github/INSTRUCCIONES-COPILOT.md',
    'docs/ARCHITECTURE-RULES.md', 'docs/architecture/pr-rules-catalog.md', ...cursor];
  const anchors = new Map();
  for (const file of new Set(files)) {
    const fail = (rule, message, line = 1) => addViolation(result, { rule, file, line, message });
    if (!existsExact(root, file)) { fail('DOC-file', 'Documento oficial ausente o mayúsculas incorrectas'); continue; }
    const source = fs.readFileSync(path.join(root, file), 'utf8');
    const targets = new Set();
    for (const link of markdownLinks(source)) {
      if (/^(?:[a-z][a-z\d+.-]*:|\/\/)/i.test(link.url)) continue;
      let url;
      try { url = decodeURIComponent(link.url.replace(/\\([\\()[\] ])/g, '$1')); }
      catch { fail('DOC-link', `URL interna inválida: ${link.url}`, link.line); continue; }
      const [pathname, fragment] = url.split('#');
      const target = pathname ? path.resolve(root, pathname.startsWith('/') ? pathname.slice(1) : path.join(path.dirname(file), pathname)) : path.join(root, file);
      const relative = path.relative(root, target).replace(/\\/g, '/');
      if (relative.startsWith('../') || path.isAbsolute(relative) || !existsExact(root, relative)) {
        fail('DOC-link', `Destino interno inexistente, fuera del repo o mayúsculas incorrectas: ${link.url}`, link.line); continue;
      }
      targets.add(relative);
      if (fragment && /\.(md|mdc)$/i.test(target)) {
        if (!anchors.has(target)) anchors.set(target, headingIds(fs.readFileSync(target, 'utf8')));
        if (!anchors.get(target).has(fragment)) fail('DOC-anchor', `Ancla interna inexistente: ${link.url}`, link.line);
      }
    }
    // Contrato estructural verificable; no pretende deducir contradicciones de negocio en prosa.
    if (adapters.includes(file) && !targets.has('CLAUDE.md')) {
      fail('DOC-authority', 'La entrada debe enlazar la jerarquía global de CLAUDE.md');
    }
    if (adapters.includes(file)) {
      const sections = prose(source).split(/(?=^#{1,6}\s)/m);
      for (const section of sections) {
        if (/^#{1,6}\s+(?:\d+\.\s*)?(?:Precedencia|Jerarquía)\b/i.test(section)
          && /^(?:\d+\.\s|\|\s*(?:Prioridad|Orden)\s*\|)/m.test(section)) {
          fail('DOC-authority', 'Una entrada no debe mantener otra lista ordenada de precedencia; enlazar CLAUDE.md');
        }
      }
    }
    if (file === 'CLAUDE.md') {
      for (const target of ['AGENTS.md', 'docs/architecture/README.md', 'docs/architecture/enforcement.md',
        'CONTRIBUTING.md', 'docs/DEVELOPMENT.md', 'FEATURES.md', 'STATUS.md', 'docs/agent/WORK_QUEUE.md']) {
        if (!targets.has(target)) fail('DOC-authority', `Falta referencia a la fuente oficial: ${target}`);
      }
    }
  }
  return result;
}

export const CHECKS = [
  { name: 'documentation', run: runCheckDocumentation },
  { name: 'pages-wrapper', run: runCheckPagesWrapper },
  { name: 'import-boundaries', run: runCheckImportBoundaries },
  { name: 'module-boundaries', run: runCheckModuleBoundaries },
  { name: 'css-prefixes', run: runCheckCssPrefixes },
  { name: 'design-system', run: runCheckDesignSystem },
  { name: 'css-reachability', run: runCheckCssReachability },
  { name: 'frontend-precision', run: runCheckFrontendPrecision },
  { name: 'frontend-datetime', run: runCheckFrontendDateTime },
  { name: 'no-cross-layer', run: runCheckNoCrossLayer },
  { name: 'platform-legacy-surface', run: runCheckPlatformLegacySurface },
  { name: 'frontend-subscriber-naming', run: runCheckFrontendSubscriberNaming },
  { name: 'frontend-http-access', run: runCheckFrontendHttpAccess },
  { name: 'backend-layering', run: runCheckBackendLayering },
  { name: 'backend-clean-architecture', run: runCheckBackendCleanArchitecture },
  { name: 'backend-controller-thin', run: runCheckBackendControllerThin },
  { name: 'backend-subscriber-rules', run: runCheckBackendSubscriberRules },
  { name: 'backend-precision', run: runCheckBackendPrecision },
  { name: 'permissions-authorization-rules', run: runCheckPermissionsAuthorizationRules },
  { name: 'frontend-permissions-rules', run: runCheckFrontendPermissionsRules },
  { name: 'duplicate-services', run: runCheckDuplicateServices },
  { name: 'naming-conventions', run: runCheckNamingConventions },
  { name: 'i18n-keys', run: runCheckI18nKeys },
  { name: 'grandfather-integrity', run: runCheckGrandfatherIntegrity },
];

/**
 * @param {{ only?: string, silent?: boolean }} [opts]
 */
export function runAllChecks(opts = {}) {
  /** @type {import('./shared/report-utils.mjs').CheckResult[]} */
  const results = [];
  for (const check of CHECKS) {
    if (opts.only && check.name !== opts.only) continue;
    const result = check.run();
    results.push(result);
    if (!opts.silent) {
      for (const line of formatConsoleCheck(result)) {
        console.log(line);
      }
    }
  }
  return results;
}

const isMain =
  process.argv[1] &&
  path.resolve(fileURLToPath(import.meta.url)) === path.resolve(process.argv[1]);

if (isMain) {
  const args = new Set(process.argv.slice(2));
  const jsonOut = args.has('--json');
  const annotate = args.has('--annotate') || process.env.GITHUB_ACTIONS === 'true';
  const only = args.has('--only') ? process.argv[process.argv.indexOf('--only') + 1] : null;

  const results = runAllChecks({ only, silent: true });
  const score = calculateArchitectureScore(results);
  let ratchet = null;
  if (!only) {
    try {
      ratchet = compareArchitectureBaseline(results, loadArchitectureBaseline());
    } catch (error) {
      console.error(`Architecture baseline invalid (fail closed): ${error.message}`);
      process.exit(1);
    }
  }
  const report = toJsonReport(results, score, ratchet);
  writeJsonReport(report);

  if (annotate) {
    emitGithubAnnotations(results, ratchet ? { newViolations: ratchet.newViolations } : {});
  }

  if (jsonOut) {
    console.log(JSON.stringify(report, null, 2));
    process.exit(report.passed ? 0 : 1);
  }

  const formatted = ratchet
    ? [...results.flatMap((result) => formatRatchetCheck(result, ratchet)), ...formatRatchetSummary(ratchet, score)]
    : [...results.flatMap(formatConsoleCheck), ...formatConsoleSummary(results, score)];
  for (const line of formatted) {
    console.log(line);
  }

  const shouldFail = ratchet
    ? !ratchet.passed
    : results.some((result) => result.violations.length > 0);
  process.exit(shouldFail ? 1 : 0);
}
