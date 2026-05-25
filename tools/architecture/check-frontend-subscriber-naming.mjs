#!/usr/bin/env node
/**
 * CI guard: falla si reaparece naming legacy Tenant en frontend/src.
 * Canónico: Subscriber / subscriber (NAMING.md).
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';

export const CHECK_NAME = 'frontend-subscriber-naming';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, '../..');
const SCAN_ROOT = path.join(REPO_ROOT, 'frontend/src');

/** @type {{ id: string, pattern: RegExp, hint: string }[]} */
const FORBIDDEN = [
  { id: 'tenant-module-keys', pattern: /\bTENANT_MODULE_KEYS\b/, hint: 'Use SUBSCRIBER_MODULE_KEYS' },
  { id: 'tenant-module-key-type', pattern: /\bTenantModuleKey\b/, hint: 'Use SubscriberModuleKey' },
  { id: 'zh-multi-tenant-header', pattern: /\bZHMultiTenantHeader\b/, hint: 'Use ZHSubscriberShellHeader' },
  { id: 'zh-tenant-header-crumb', pattern: /\bZHTenantHeaderModuleCrumb\b/, hint: 'Use ZHSubscriberHeaderModuleCrumb' },
  { id: 'definitive-tenant-login', pattern: /\bisDefinitiveTenantLoginError\b/, hint: 'Use isDefinitiveSubscriberLoginError' },
  { id: 'same-tenant-context', pattern: /\bsameTenantAsContext\b/, hint: 'Use sameSubscriberAsContext' },
  {
    id: 'layout-frame-tenant-variant',
    pattern: /LayoutFrameVariant\s*=\s*[^;]*'tenant'|variant\s*=\s*['"]tenant['"]|shell-content-frame--tenant/,
    hint: "Use LayoutFrame variant 'subscriber'",
  },
  { id: 'tenant-id-identifier', pattern: /\btenantId\b/, hint: 'Use subscriberId' },
  {
    id: 'tenant-word-in-code',
    pattern: /\b[Tt]enant\b/,
    hint: 'Use subscriber/suscriptor naming (NAMING.md)',
  },
];

const ALLOW_FILES = new Set([
  'tools/architecture/check-frontend-subscriber-naming.mjs',
]);

function walk(dir, out = []) {
  for (const name of fs.readdirSync(dir)) {
    const p = path.join(dir, name);
    const st = fs.statSync(p);
    if (st.isDirectory()) walk(p, out);
    else if (/\.(ts|tsx|js|jsx|mjs|css|json)$/.test(name)) out.push(p);
  }
  return out;
}

function isCommentLine(trimmed) {
  return (
    trimmed.startsWith('//') ||
    trimmed.startsWith('*') ||
    trimmed.startsWith('/*') ||
    trimmed.startsWith('*/')
  );
}

export function runCheckFrontendSubscriberNaming() {
  const result = createCheckResult(CHECK_NAME);
  for (const file of walk(SCAN_ROOT)) {
    const rel = path.relative(REPO_ROOT, file).replace(/\\/g, '/');
    if (ALLOW_FILES.has(rel)) continue;
    const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
    lines.forEach((line, idx) => {
      const trimmed = line.trim();
      if (isCommentLine(trimmed)) return;
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
  const result = runCheckFrontendSubscriberNaming();
  if (result.violations.length > 0) {
    console.error('Frontend subscriber naming guard FAILED:\n');
    for (const v of result.violations) {
      console.error(`  [${v.rule}] ${v.file}:${v.line} — ${v.message}`);
    }
    process.exit(1);
  }
  console.log('Frontend subscriber naming guard OK (frontend/src)');
}
