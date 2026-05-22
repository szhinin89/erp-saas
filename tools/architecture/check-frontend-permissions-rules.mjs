/**
 * check-frontend-permissions-rules.mjs
 *
 * PERM-F1 — Pages must not duplicate `isAdmin || hasPerm` (use usePermissionsUi)
 * PERM-F2 — Permission checks should go through access/usePermissionsUi or permissionsStore.has
 */

import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT, walkFiles, toRepoRel } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';

export const CHECK_NAME = 'frontend-permissions-rules';

export function runCheckFrontendPermissionsRules() {
  const result = createCheckResult(CHECK_NAME);
  const frontendSrc = path.join(REPO_ROOT, 'frontend', 'src');

  if (!fs.existsSync(frontendSrc)) return result;

  for (const file of walkFiles(frontendSrc, { extensions: ['.ts', '.tsx'] })) {
    const rel = toRepoRel(file);
    const content = fs.readFileSync(file, 'utf8');

    if (content.includes('isAdmin || hasPerm') || content.includes('isAdmin || has(')) {
      addViolation(result, {
        rule: 'PERM-F1',
        file: rel,
        message: 'Use usePermissionsUi().canShow instead of isAdmin || hasPerm pattern.',
      });
    }

    if (
      rel.includes('/access/permissionUi') ||
      rel.includes('\\access\\permissionUi')
    ) {
      continue;
    }

    // Local booleans like `const hasPerm = perm.length > 0` (menu JSON) are OK.
    if (/\bhasPerm\s*\(/.test(content)) {
      addViolation(result, {
        rule: 'PERM-F2',
        file: rel,
        message: 'Call usePermissionsUi().canShow(key) instead of hasPerm(key).',
      });
    }

    if (
      /\bconst\s+hasPerm\s*=/.test(content) &&
      /usePermissionsStore|permissionsStore/.test(content)
    ) {
      addViolation(result, {
        rule: 'PERM-F2',
        file: rel,
        message: 'Define permission UI checks via usePermissionsUi().canShow, not local hasPerm alias.',
      });
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-frontend-permissions-rules.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckFrontendPermissionsRules()) ? 0 : 1);
}
