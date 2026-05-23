#!/usr/bin/env node
/** Segunda pasada: eliminar isSuperAdmin y nombres SuperAdmin* del código visible. */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const SRC = path.join(ROOT, 'frontend/src');
const E2E = path.join(ROOT, 'frontend/e2e');

const SKIP = new Set([
  'frontend/src/constants/platformAuth.ts',
  'frontend/src/deployment/deploymentConfig.ts',
  'frontend/src/lib/session/sessionStorageKeys.ts',
  'frontend/src/routes/platformRoutes.tsx',
  'frontend/src/modules/platform/api/platformApiPaths.ts',
  'frontend/src/modules/auth/normalizeAuthResponse.test.ts',
  'frontend/src/access/permissionUi.test.ts',
]);

function walk(dir, out = []) {
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    if (fs.statSync(full).isDirectory()) {
      if (name === 'node_modules' || name === 'dist') continue;
      walk(full, out);
    } else if (/\.(ts|tsx|css|md|mjs)$/.test(name)) {
      out.push(full);
    }
  }
  return out;
}

const replacements = [
  ['isSuperAdmin', 'isPlatformOperator'],
  ['SuperAdminMenuBuilderSharedEffectsParams', 'PlatformMenuBuilderSharedEffectsParams'],
  ['SuperAdminMenuBuilderActionsParams', 'PlatformMenuBuilderActionsParams'],
  ['isPlatformSuperAdmin', 'isPlatformOperatorSession'],
  ['superAdminOn', 'platformPanelOn'],
  ['globalSa', 'globalPlatformOperator'],
  ['enterSuperAdminDashboard', 'enterPlatformDashboard'],
  ["makeExportFileName('superadmin-crm-workspace')", "makeExportFileName('platform-crm-workspace')"],
  ["makeExportFileName('superadmin-crm-audit')", "makeExportFileName('platform-crm-audit')"],
  ['Panel SuperAdmin', 'Panel platform'],
  ['Roles: SuperAdmin,', 'Roles: Platform operator (JWT SuperAdmin),'],
  ['→ superadmin;', '→ platform;'],
  ['shell `/superadmin/*`', 'shell `/platform/*`'],
  ['/superadmin/*` bajo', '/platform/*` bajo'],
  ['SuperAdmin + SaaS', 'Platform + SaaS'],
  ['Plantilla CRUD/listado SuperAdmin:', 'Plantilla CRUD/listado platform:'],
  ['pantallas SuperAdmin:', 'pantallas platform:'],
  ['cuando el rol no es SuperAdmin.', 'cuando el usuario no es operador platform.'],
  ['p. ej. solo SuperAdmin).', 'p. ej. solo operador platform).'],
  ['estáticos para SuperAdmin).', 'estáticos para operador platform).'],
  ['filtro panel superadmin).', 'filtro panel platform).'],
  ['para SuperAdmin en contexto global', 'para operador platform en contexto global'],
  ['panel SuperAdmin:', 'panel platform:'],
  ['estáticos de SuperAdmin que falten', 'estáticos de platform que falten'],
  ['antigua página solo SuperAdmin.', 'antigua página solo platform.'],
  ['SuperAdmin CRM workspace', 'Platform CRM workspace'],
  ['SuperAdmin Menu Builder CRM', 'Platform Menu Builder CRM'],
  ['SuperAdminPlansSection', 'PlatformPlansSection'],
  ['SuperAdminPanel,', 'Platform panel,'],
  ['(SuperAdmin, sin admin)', '(operador platform, sin admin)'],
  ['Null para SuperAdmin.', 'Null para operador platform global.'],
  ['global (SuperAdmin sin empresa activa)', 'global (operador platform sin empresa activa)'],
  ['Admin/SuperAdmin.', 'Admin/platform operator.'],
  ['Admin and SuperAdmin', 'Admin and platform operator JWT role'],
  ['isTenantAdminRole(\'SuperAdmin\')', 'isTenantAdminRole(JWT_PLATFORM_OPERATOR_ROLE)'],
];

for (const root of [SRC, E2E]) {
  for (const file of walk(root)) {
    const rel = path.relative(ROOT, file).replace(/\\/g, '/');
    if (SKIP.has(rel)) continue;
    let text = fs.readFileSync(file, 'utf8');
    let changed = false;
    for (const [from, to] of replacements) {
      if (text.includes(from)) {
        text = text.split(from).join(to);
        changed = true;
      }
    }
    if (changed) {
      fs.writeFileSync(file, text, 'utf8');
      console.log('updated:', rel);
    }
  }
}

console.log('cleanup pass 2 done');
