#!/usr/bin/env node
/**
 * Static code scan — frontend + backend (docs/ excluded at walk level).
 */
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  REPO_ROOT,
  loadConfig,
  walkSourceFiles,
  scanFileForPatterns,
} from './platform-guard-lib.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

export function runForbiddenPatternScan() {
  const config = loadConfig();
  const extensions = config.sourceExtensions;
  const exclude = config.excludeDirectories ?? ['docs'];
  const excludeFiles = config.excludeFiles ?? [];

  /** @type {import('./platform-guard-lib.mjs').GuardViolation[]} */
  const violations = [];

  for (const [label, relRoot] of Object.entries(config.scanRoots)) {
    const root = path.join(REPO_ROOT, relRoot);
    const rules = (config.forbiddenPatterns ?? []).filter(
      (rule) => !rule.scanRoots || rule.scanRoots.includes(label),
    );
    for (const file of walkSourceFiles(root, extensions, exclude, excludeFiles)) {
      violations.push(
        ...scanFileForPatterns(file, rules, `static-scan-${label}`),
      );
    }
  }

  return {
    name: 'static-forbidden-patterns',
    passed: violations.length === 0,
    violations,
    detail: `Scanned ${Object.values(config.scanRoots).join(', ')}`,
  };
}

const isMain =
  process.argv[1] &&
  path.resolve(fileURLToPath(import.meta.url)) === path.resolve(process.argv[1]);

if (isMain) {
  const result = runForbiddenPatternScan();
  if (!result.passed) {
    console.error('Forbidden pattern scan FAILED:\n');
    for (const v of result.violations) {
      console.error(`  [${v.rule}] ${v.file}:${v.line} — ${v.message}`);
    }
    process.exit(1);
  }
  console.log(`Forbidden pattern scan OK (${result.detail})`);
}
