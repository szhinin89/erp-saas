#!/usr/bin/env node
/**
 * Fail if frontend imports legacy platform paths or superadminService tokens.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  REPO_ROOT,
  loadConfig,
  walkSourceFiles,
  isCommentLine,
  matchesForbidden,
} from './platform-guard-lib.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const FRONTEND_SRC = path.join(REPO_ROOT, 'frontend/src');

export function runValidatePlatformImports() {
  const config = loadConfig();
  const pathRules = [
    ...(config.forbiddenImportPaths ?? []),
    ...(config.forbiddenFrontendImportTokens ?? []),
  ];

  /** @type {import('./platform-guard-lib.mjs').GuardViolation[]} */
  const violations = [];

  for (const file of walkSourceFiles(FRONTEND_SRC, ['.ts', '.tsx', '.js', '.jsx', '.mjs'], ['docs'])) {
    const rel = path.relative(REPO_ROOT, file).replace(/\\/g, '/');
    const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);

    lines.forEach((line, idx) => {
      if (isCommentLine(line)) return;
      const isImportLike =
        /\bimport\b/.test(line) ||
        /\bfrom\s+['"]/.test(line) ||
        /\brequire\s*\(/.test(line) ||
        /\bimport\s*\(/.test(line);

      if (!isImportLike && !/superadminService|superAdminService/i.test(line)) return;

      for (const rule of pathRules) {
        if (matchesForbidden(line, rule.pattern)) {
          violations.push({
            check: 'platform-imports',
            rule: rule.id,
            file: rel,
            line: idx + 1,
            message: `${rule.hint} — ${line.trim().slice(0, 120)}`,
          });
        }
      }
    });
  }

  return {
    name: 'platform-imports',
    passed: violations.length === 0,
    violations,
    detail: 'frontend/src import paths',
  };
}

const isMain =
  process.argv[1] &&
  path.resolve(fileURLToPath(import.meta.url)) === path.resolve(process.argv[1]);

if (isMain) {
  const result = runValidatePlatformImports();
  if (!result.passed) {
    console.error('Platform import guard FAILED:\n');
    for (const v of result.violations) {
      console.error(`  [${v.rule}] ${v.file}:${v.line} — ${v.message}`);
    }
    process.exit(1);
  }
  console.log('Platform import guard OK');
}
