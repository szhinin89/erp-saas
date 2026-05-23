#!/usr/bin/env node
/**
 * Validates platform router files: no legacy imports, canonical platform shell wiring.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { REPO_ROOT, loadConfig, isCommentLine, matchesForbidden } from './platform-guard-lib.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const ROUTER_FILES = [
  'frontend/src/routes/platformRoutes.tsx',
  'frontend/src/App.tsx',
];

const FORBIDDEN_IMPORT_TOKENS = [
  { id: 'legacy-superadmin-import', pattern: 'superadminService', hint: 'Use platformService' },
  { id: 'legacy-superadmin-import-camel', pattern: 'superAdminService', hint: 'Use platformService' },
  { id: 'legacy-module-path', pattern: 'modules/superadmin', hint: 'Use modules/platform' },
  { id: 'legacy-components-path', pattern: 'components/superadmin', hint: 'Use components/platform' },
  { id: 'legacy-pages-path', pattern: 'pages/SuperAdmin', hint: 'Use pages/Platform' },
  { id: 'legacy-use-superadmin', pattern: 'useSuperAdmin', hint: 'Use usePlatformGate' },
];

export function runValidateFrontendRoutes() {
  const config = loadConfig();
  /** @type {import('./platform-guard-lib.mjs').GuardViolation[]} */
  const violations = [];

  for (const rel of ROUTER_FILES) {
    const file = path.join(REPO_ROOT, rel);
    if (!fs.existsSync(file)) {
      violations.push({
        check: 'frontend-routes',
        rule: 'missing-router-file',
        file: rel,
        message: `Required router file missing: ${rel}`,
      });
      continue;
    }

    const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
    lines.forEach((line, idx) => {
      if (isCommentLine(line)) return;

      for (const rule of FORBIDDEN_IMPORT_TOKENS) {
        if (matchesForbidden(line, rule.pattern)) {
          violations.push({
            check: 'frontend-routes',
            rule: rule.id,
            file: rel,
            line: idx + 1,
            message: `${rule.hint} — ${line.trim().slice(0, 120)}`,
          });
        }
      }
    });
  }

  const platformRoutesPath = path.join(REPO_ROOT, 'frontend/src/routes/platformRoutes.tsx');
  const platformRoutesText = fs.readFileSync(platformRoutesPath, 'utf8');
  if (!platformRoutesText.includes('platformShellRoutes')) {
    violations.push({
      check: 'frontend-routes',
      rule: 'platform-shell-missing',
      file: 'frontend/src/routes/platformRoutes.tsx',
      message: 'platformShellRoutes() must be exported from platformRoutes.tsx',
    });
  }
  if (!platformRoutesText.includes('PlatformLayout')) {
    violations.push({
      check: 'frontend-routes',
      rule: 'platform-layout-missing',
      file: 'frontend/src/routes/platformRoutes.tsx',
      message: 'Platform shell must use PlatformLayout',
    });
  }
  if (!/\/superadmin/.test(platformRoutesText)) {
    violations.push({
      check: 'frontend-routes',
      rule: 'platform-ui-route-missing',
      file: 'frontend/src/routes/platformRoutes.tsx',
      message: 'Canonical UI route prefix /superadmin/* must remain in platformRoutes.tsx',
    });
  }

  // Prohibited legacy API route strings in router layer
  for (const prefix of config.forbiddenApiRoutePrefixes ?? []) {
    if (matchesForbidden(platformRoutesText, prefix)) {
      violations.push({
        check: 'frontend-routes',
        rule: 'forbidden-api-route-in-router',
        file: 'frontend/src/routes/platformRoutes.tsx',
        message: `Forbidden API prefix ${prefix} in platform router`,
      });
    }
  }

  return {
    name: 'frontend-routes',
    passed: violations.length === 0,
    violations,
    detail: ROUTER_FILES.join(', '),
  };
}

const isMain =
  process.argv[1] &&
  path.resolve(fileURLToPath(import.meta.url)) === path.resolve(process.argv[1]);

if (isMain) {
  const result = runValidateFrontendRoutes();
  if (!result.passed) {
    console.error('Frontend route guard FAILED:\n');
    for (const v of result.violations) {
      const loc = v.line != null ? `${v.file}:${v.line}` : v.file;
      console.error(`  [${v.rule}] ${loc} — ${v.message}`);
    }
    process.exit(1);
  }
  console.log('Frontend route guard OK');
}
