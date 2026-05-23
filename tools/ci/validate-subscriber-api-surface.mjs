#!/usr/bin/env node
/**
 * Fail if `/api/subscribers` appears outside runtime whitelist files/paths.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { REPO_ROOT, loadConfig, walkSourceFiles, isCommentLine } from './platform-guard-lib.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const FRONTEND_SRC = path.join(REPO_ROOT, 'frontend/src');

/** @param {string} line */
function isDocCommentAboutRuntime(line) {
  const t = line.trim();
  return t.includes('GET /api/subscribers/entitlements/me') ||
    t.includes('Snapshot de `GET /api/subscribers/entitlements/me`');
}

export function runValidateSubscriberApiSurface() {
  const config = loadConfig();
  const allowFiles = new Set(
    (config.subscriberRuntimeApiAllowFiles ?? []).map((f) => f.replace(/\\/g, '/')),
  );
  const allowPathLiterals = config.subscriberRuntimeApiAllowPaths ?? [
    '/api/subscribers/entitlements/me',
  ];

  /** @type {import('./platform-guard-lib.mjs').GuardViolation[]} */
  const violations = [];

  for (const file of walkSourceFiles(FRONTEND_SRC, ['.ts', '.tsx'], ['docs'], config.excludeFiles ?? [])) {
    const rel = path.relative(REPO_ROOT, file).replace(/\\/g, '/');
    const allowedFile = allowFiles.has(rel);
    const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);

    lines.forEach((line, idx) => {
      if (isCommentLine(line) || isDocCommentAboutRuntime(line)) return;
      if (!line.includes('/api/subscribers')) return;

      if (allowedFile) {
        const hasAllowedLiteral = allowPathLiterals.some((p) => line.includes(p));
        const hasRuntimeBase =
          line.includes("RUNTIME_SUBSCRIBER_API") ||
          line.includes('runtimeSubscriber(') ||
          line.includes('tenantSubscriberService') ||
          line.includes('/public-settings');
        if (hasAllowedLiteral || hasRuntimeBase) return;
      }

      violations.push({
        check: 'subscriber-api-surface',
        rule: 'forbidden-subscribers-api',
        file: rel,
        line: idx + 1,
        message:
          'Use /api/platform/* for Platform Control Plane. Runtime whitelist: entitlements/me, public-settings, tenantSubscriberService only.',
      });
    });
  }

  return {
    name: 'subscriber-api-surface',
    passed: violations.length === 0,
    violations,
    detail: `${allowFiles.size} runtime allow-files`,
  };
}

const isMain =
  process.argv[1] &&
  path.resolve(fileURLToPath(import.meta.url)) === path.resolve(process.argv[1]);

if (isMain) {
  const result = runValidateSubscriberApiSurface();
  if (!result.passed) {
    console.error('Subscriber API surface guard FAILED:\n');
    for (const v of result.violations) {
      console.error(`  [${v.rule}] ${v.file}:${v.line} — ${v.message}`);
    }
    process.exit(1);
  }
  console.log(`Subscriber API surface guard OK (${result.detail})`);
}
