#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
export const REPO_ROOT = path.resolve(__dirname, '../..');
export const CONFIG_PATH = path.join(__dirname, 'platform-guard-config.json');

/** @typedef {{ id: string, pattern: string, hint: string }} PatternRule */

/** @typedef {{ rule: string, file: string, line?: number, message: string, check: string }} GuardViolation */

export function loadConfig() {
  return JSON.parse(fs.readFileSync(CONFIG_PATH, 'utf8'));
}

/**
 * @param {string} dir
 * @param {string[]} extensions
 * @param {string[]} excludeDirs
 */
export function walkSourceFiles(dir, extensions, excludeDirs = [], excludeFiles = []) {
  /** @type {string[]} */
  const out = [];
  if (!fs.existsSync(dir)) return out;

  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    const rel = path.relative(REPO_ROOT, full).replace(/\\/g, '/');
    if (excludeDirs.some((ex) => rel === ex || rel.startsWith(`${ex}/`))) continue;
    if (excludeFiles.includes(rel)) continue;

    const st = fs.statSync(full);
    if (st.isDirectory()) {
      walkSourceFiles(full, extensions, excludeDirs, excludeFiles).forEach((f) => out.push(f));
      continue;
    }
    if (extensions.some((ext) => name.endsWith(ext))) out.push(full);
  }
  return out;
}

/** @param {string} line */
export function isCommentLine(line) {
  const t = line.trim();
  return (
    t.startsWith('//') ||
    t.startsWith('*') ||
    t.startsWith('///') ||
    t.startsWith('/*') ||
    t.startsWith('* ')
  );
}

/**
 * Case-insensitive substring match (exact token / path segment semantics via includes).
 * @param {string} haystack
 * @param {string} needle
 */
export function matchesForbidden(haystack, needle) {
  return haystack.toLowerCase().includes(needle.toLowerCase());
}

/**
 * @param {string} filePath
 * @param {PatternRule[]} rules
 * @param {string} checkName
 * @param {{ skipComments?: boolean }} [opts]
 * @returns {GuardViolation[]}
 */
export function scanFileForPatterns(filePath, rules, checkName, opts = { skipComments: true }) {
  /** @type {GuardViolation[]} */
  const violations = [];
  const rel = path.relative(REPO_ROOT, filePath).replace(/\\/g, '/');
  const lines = fs.readFileSync(filePath, 'utf8').split(/\r?\n/);

  lines.forEach((line, idx) => {
    if (opts.skipComments && isCommentLine(line)) return;
    for (const rule of rules) {
      if (matchesForbidden(line, rule.pattern)) {
        violations.push({
          check: checkName,
          rule: rule.id,
          file: rel,
          line: idx + 1,
          message: `${rule.hint} — ${line.trim().slice(0, 120)}`,
        });
      }
    }
  });

  return violations;
}

/**
 * @param {GuardViolation[]} violations
 * @param {string} outPath
 * @param {{ checks: { name: string, passed: boolean, violations: GuardViolation[], detail?: string }[], endpoints?: object, status: 'PASS' | 'FAIL' }} report
 */
export function writeGuardReport(outPath, report) {
  fs.mkdirSync(path.dirname(outPath), { recursive: true });

  const totalViolations = report.checks.reduce((n, c) => n + c.violations.length, 0);
  const lines = [
    '# Platform Control Plane — CI Guard Report',
    '',
    `**Generated:** ${new Date().toISOString()}`,
    `**Status:** ${report.status}`,
    `**Violations:** ${totalViolations}`,
    '',
    '## Summary',
    '',
    '| Check | Result | Violations |',
    '|-------|--------|------------|',
  ];

  for (const check of report.checks) {
    lines.push(`| ${check.name} | ${check.passed ? 'PASS' : 'FAIL'} | ${check.violations.length} |`);
  }

  lines.push('', '## Violations', '');
  if (totalViolations === 0) {
    lines.push('_No violations detected._');
  } else {
    for (const check of report.checks) {
      if (check.violations.length === 0) continue;
      lines.push(`### ${check.name}`, '');
      for (const v of check.violations) {
        const loc = v.line != null ? `${v.file}:${v.line}` : v.file;
        lines.push(`- \`[${v.rule}]\` ${loc} — ${v.message}`);
      }
      lines.push('');
    }
  }

  if (report.endpoints) {
    lines.push('## Endpoints detected (frontend)', '');
    lines.push('```json');
    lines.push(JSON.stringify(report.endpoints, null, 2));
    lines.push('```');
    lines.push('');
  }

  lines.push('## Design', '');
  lines.push('- Preventive, mandatory, fail-fast guard for Platform Control Plane.');
  lines.push('- Legacy SuperAdmin API surface must not reappear.');
  lines.push('- Config: `tools/ci/platform-guard-config.json`');
  lines.push('');

  fs.writeFileSync(outPath, `${lines.join('\n')}\n`, 'utf8');
}
