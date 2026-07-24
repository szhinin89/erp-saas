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

export function runValidateSubscriberApiSurface() {
  const config = loadConfig();

  /** @type {import('./platform-guard-lib.mjs').GuardViolation[]} */
  const violations = [];

  for (const file of walkSourceFiles(FRONTEND_SRC, ['.ts', '.tsx'], ['docs'], config.excludeFiles ?? [])) {
    const rel = path.relative(REPO_ROOT, file).replace(/\\/g, '/');
    const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);

    lines.forEach((line, idx) => {
      if (isCommentLine(line)) return;
      if (!line.includes('/api/subscribers')) return;

      violations.push({
        check: 'subscriber-api-surface',
        rule: 'forbidden-subscribers-api',
        file: rel,
        line: idx + 1,
        message: 'Use /api/platform/* for Platform Control Plane. /api/subscribers/* does not exist in the current backend surface.',
      });
    });
  }

  return {
    name: 'subscriber-api-surface',
    passed: violations.length === 0,
    violations,
    detail: `${violations.length === 0 ? 'no' : violations.length} /api/subscribers reference(s) found`,
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
