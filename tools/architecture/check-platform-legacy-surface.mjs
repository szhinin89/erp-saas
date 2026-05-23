#!/usr/bin/env node
/**
 * Phase 5 — CI guard: falla si reaparece superficie legacy SuperAdmin API en frontend/src.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';

export const CHECK_NAME = 'platform-legacy-surface';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, '../..');
const SCAN_ROOT = path.join(REPO_ROOT, 'frontend/src');

/** @type {{ id: string, pattern: RegExp, hint: string }[]} */
const FORBIDDEN = [
  { id: 'superadmin-login', pattern: /superadmin-login/, hint: 'Use POST /api/platform/auth/login' },
  { id: 'api-superadmin', pattern: /\/api\/superadmin\//, hint: 'Use /api/platform/*' },
  { id: 'iam-superadmin', pattern: /\/api\/admin\/iam\/superadmin/, hint: 'Use /api/platform/subscribers' },
  { id: 'legacy-platform-api', pattern: /LEGACY_PLATFORM/, hint: 'Remove LEGACY_PLATFORM_* constants' },
  { id: 'superadmin-service-import', pattern: /superAdminService/, hint: 'Use platformService from platformService.ts' },
  { id: 'legacy-platform-path-const', pattern: /LEGACY_PLATFORM_API/, hint: 'Use PLATFORM_API only' },
];

function walk(dir, out = []) {
  for (const name of fs.readdirSync(dir)) {
    const p = path.join(dir, name);
    const st = fs.statSync(p);
    if (st.isDirectory()) walk(p, out);
    else if (/\.(ts|tsx|js|jsx|mjs)$/.test(name)) out.push(p);
  }
  return out;
}

export function runCheckPlatformLegacySurface() {
  const result = createCheckResult(CHECK_NAME);
  for (const file of walk(SCAN_ROOT)) {
    const rel = path.relative(REPO_ROOT, file).replace(/\\/g, '/');
    const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
    lines.forEach((line, idx) => {
      const trimmed = line.trim();
      if (trimmed.startsWith('//') || trimmed.startsWith('*')) return;
      for (const rule of FORBIDDEN) {
        if (rule.pattern.test(line)) {
          addViolation(result, {
            rule: rule.id,
            file: rel,
            line: idx + 1,
            message: `${rule.hint} — ${trimmed.slice(0, 100)}`,
          });
        }
      }
    });
  }
  return result;
}

const isMain =
  process.argv[1] &&
  path.resolve(fileURLToPath(import.meta.url)) === path.resolve(process.argv[1]);

if (isMain) {
  const result = runCheckPlatformLegacySurface();
  if (result.violations.length > 0) {
    console.error('Platform legacy surface guard FAILED:\n');
    for (const v of result.violations) {
      console.error(`  [${v.rule}] ${v.file}:${v.line} — ${v.message}`);
    }
    process.exit(1);
  }
  console.log('Platform legacy surface guard OK (frontend/src)');
}
