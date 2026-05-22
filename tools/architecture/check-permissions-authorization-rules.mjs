/**
 * check-permissions-authorization-rules.mjs
 *
 * Guardrails for runtime authorization consistency:
 *   PERM-001 — PermissionHandler must not use repositories or decide permissions directly
 *   PERM-002 — ERP.API Authorization must not reference IAccessRepository / GetProfilePermissionsAsync
 *   PERM-003 — Application write handlers must not inject IPermissionsCacheService (use Invalidator)
 *   PERM-004 — GetProfilePermissionsAsync outside provider/admin read models is forbidden
 */

import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT, walkFiles, toRepoRel } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';

export const CHECK_NAME = 'permissions-authorization-rules';

const ADMIN_READ_MARKERS = [
  'AdminReadModel',
  'GetProfilePermissionsHandler',
  'UpsertProfilePermissionsHandler',
];

const CACHE_WRITE_ALLOWED = [
  'EffectivePermissionKeysProvider',
  'DistributedPermissionsCacheService',
  'ResilientPermissionsCacheService',
  'PermissionsCacheInvalidator',
  'IPermissionsCacheBackend',
];

function isAdminReadFile(content) {
  return ADMIN_READ_MARKERS.some((m) => content.includes(m));
}

function isCacheWriteAllowed(rel, content) {
  if (CACHE_WRITE_ALLOWED.some((m) => rel.includes(m) || content.includes(m))) return true;
  if (rel.includes('IPermissionsCacheService.cs')) return true;
  return false;
}

export function runCheckPermissionsAuthorizationRules() {
  const result = createCheckResult(CHECK_NAME);
  const backendSrc = path.join(REPO_ROOT, 'backend', 'src');

  if (!fs.existsSync(backendSrc)) return result;

  for (const file of walkFiles(backendSrc, { extensions: ['.cs'] })) {
    const rel = toRepoRel(file);
    const content = fs.readFileSync(file, 'utf8');

    // PERM-001 / PERM-002 — API authorization layer
    if (rel.includes('ERP.API/Authorization/') || rel.includes('ERP.API\\Authorization\\')) {
      if (content.includes('IAccessRepository') || content.includes('GetProfilePermissionsAsync')) {
        addViolation(result, {
          rule: 'PERM-002',
          file: rel,
          message: 'Authorization layer must delegate to IRuntimePermissionAuthorizer only (no permission repositories).',
        });
      }

      if (rel.endsWith('PermissionHandler.cs')) {
        const forbidden = [
          'IAccessRepository',
          'ICompanyRepository',
          'ISubscriberRepository',
          'ISubscriberEntitlementsService',
          'IEffectivePermissionKeysProvider',
          'ICompanyContextProvider',
        ];
        for (const sym of forbidden) {
          if (content.includes(sym)) {
            addViolation(result, {
              rule: 'PERM-001',
              file: rel,
              message: `PermissionHandler must not reference ${sym}; use IRuntimePermissionAuthorizer only.`,
            });
          }
        }
        if (!content.includes('IRuntimePermissionAuthorizer')) {
          addViolation(result, {
            rule: 'PERM-001',
            file: rel,
            message: 'PermissionHandler must depend on IRuntimePermissionAuthorizer.',
          });
        }
      }
    }

    // PERM-003 — write-side cache invalidation
    if (rel.includes('ERP.Application/') || rel.includes('ERP.Application\\')) {
      if (content.includes('IPermissionsCacheService')
        && !content.includes('IPermissionsCacheInvalidator')
        && !rel.includes('EffectivePermissionKeysProvider')
        && !rel.includes('IPermissionsCacheService.cs')
        && !isCacheWriteAllowed(rel, content)) {
        addViolation(result, {
          rule: 'PERM-003',
          file: rel,
          message: 'Application mutation handlers must use IPermissionsCacheInvalidator, not IPermissionsCacheService.',
        });
      }

      // PERM-004 — profile permission reads
      if (content.includes('GetProfilePermissionsAsync')
        && !isAdminReadFile(content)
        && !rel.includes('EffectivePermissionKeysProvider')) {
        addViolation(result, {
          rule: 'PERM-004',
          file: rel,
          message: 'GetProfilePermissionsAsync is restricted to admin read models and IEffectivePermissionKeysProvider.',
        });
      }
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-permissions-authorization-rules.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckPermissionsAuthorizationRules()) ? 0 : 1);
}
