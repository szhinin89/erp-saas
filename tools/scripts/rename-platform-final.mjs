#!/usr/bin/env node
/**
 * Renombrado masivo SuperAdmin → platform (excluye Migrations históricas).
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const SCAN_ROOTS = [
  path.join(ROOT, 'backend/src'),
  path.join(ROOT, 'frontend/src'),
  path.join(ROOT, 'scripts'),
  path.join(ROOT, 'tools/ci'),
  path.join(ROOT, 'docs'),
];

const SKIP_DIR = /[\\/](Migrations)([\\/]|$)/;
const SKIP_FILES = new Set([
  path.normalize('tools/scripts/rename-platform-final.mjs'),
  path.normalize('tools/scripts/migrate-superadmin-to-platform.mjs'),
  path.normalize('tools/scripts/cleanup-superadmin-naming.mjs'),
]);

/** Longest-first replacements */
const REPLACEMENTS = [
  ['SuperAdminCreateSubscriberWithAdminHandler', 'PlatformCreateSubscriberWithAdminHandler'],
  ['SuperAdminCreateSubscriberWithAdminCommand', 'PlatformCreateSubscriberWithAdminCommand'],
  ['GetSuperAdminSubscribersHandler', 'GetPlatformSubscribersHandler'],
  ['GetSuperAdminSubscribersQuery', 'GetPlatformSubscribersQuery'],
  ['SuperAdminSubscriberItemDto', 'PlatformSubscriberItemDto'],
  ['SuperAdminSubscribers', 'PlatformSubscribers'],
  ['ClaimInitialSuperAdminCommandValidator', 'ClaimInitialPlatformOperatorCommandValidator'],
  ['ClaimInitialSuperAdminHandler', 'ClaimInitialPlatformOperatorHandler'],
  ['ClaimInitialSuperAdminCommand', 'ClaimInitialPlatformOperatorCommand'],
  ['ClaimInitialSuperAdmin', 'ClaimInitialPlatformOperator'],
  ['GetSuperAdminGrowthMonetaryQuery', 'GetPlatformGrowthMonetaryQuery'],
  ['GetSuperAdminGrowthAnalyticsQuery', 'GetPlatformGrowthAnalyticsQuery'],
  ['GetSuperAdminNavigationMenuQuery', 'GetPlatformNavigationMenuQuery'],
  ['GetSuperAdminPlansCatalogQuery', 'GetPlatformPlansCatalogQuery'],
  ['GetSuperAdminMetricsQuery', 'GetPlatformMetricsQuery'],
  ['SuperAdminRecentSubscriberDto', 'PlatformRecentSubscriberDto'],
  ['SuperAdminMetricsTotalsDto', 'PlatformMetricsTotalsDto'],
  ['SuperAdminMetricsDto', 'PlatformMetricsDto'],
  ['SuperAdminGlobal', 'PlatformGlobal'],
  ['GlobalSuperAdminHandler', 'GlobalPlatformOperatorHandler'],
  ['GlobalSuperAdminRequirement', 'GlobalPlatformOperatorRequirement'],
  ['GlobalSuperAdmin', 'GlobalPlatformOperator'],
  ['AuthorizeInitialSuperAdminSetup', 'AuthorizeInitialPlatformOperatorSetup'],
  ['InitialSuperAdminSetupToken', 'InitialPlatformOperatorSetupToken'],
  ['SuperAdminPanelDisabledUserMessage', 'PlatformPanelDisabledUserMessage'],
  ['SuperAdminPanelDisabled', 'PlatformPanelDisabled'],
  ['IsSuperAdminPanelEnabled', 'IsPlatformPanelEnabled'],
  ['SuperAdminPanelEnabled', 'PlatformPanelEnabled'],
  ['RequireSuperAdminPanel', 'RequirePlatformPanel'],
  ['requireSuperAdminPanel', 'requirePlatformPanel'],
  ['IsPlatformSuperAdmin', 'IsPrimaryPlatformOperator'],
  ['CreatePlatformSuperAdmin', 'CreatePlatformOperator'],
  ['PlatformRole.SuperAdmin', 'PlatformRole.PlatformOperator'],
  ['Enums.PlatformRole.SuperAdmin', 'Enums.PlatformRole.PlatformOperator'],
  ['SuperAdminGrowthRangeParser', 'PlatformGrowthRangeParser'],
  ['ReorderSuperAdminNavigationItemLevelsHandler', 'ReorderPlatformNavigationItemLevelsHandler'],
  ['ReorderSuperAdminNavigationItemLevelsCommand', 'ReorderPlatformNavigationItemLevelsCommand'],
  ['ReorderSuperAdminNavigationGroupsHandler', 'ReorderPlatformNavigationGroupsHandler'],
  ['ReorderSuperAdminNavigationGroupsCommand', 'ReorderPlatformNavigationGroupsCommand'],
  ['RevokeSuperAdminUserSessionsHandler', 'RevokePlatformUserSessionsHandler'],
  ['RevokeSuperAdminUserSessionsCommand', 'RevokePlatformUserSessionsCommand'],
  ['DeleteSuperAdminNavigationMenuItemHandler', 'DeletePlatformNavigationMenuItemHandler'],
  ['DeleteSuperAdminNavigationMenuItemCommand', 'DeletePlatformNavigationMenuItemCommand'],
  ['UpdateSuperAdminNavigationMenuItemHandler', 'UpdatePlatformNavigationMenuItemHandler'],
  ['UpdateSuperAdminNavigationMenuItemCommand', 'UpdatePlatformNavigationMenuItemCommand'],
  ['CreateSuperAdminNavigationMenuItemHandler', 'CreatePlatformNavigationMenuItemHandler'],
  ['CreateSuperAdminNavigationMenuItemCommand', 'CreatePlatformNavigationMenuItemCommand'],
  ['GetSuperAdminNavigationMenuHandler', 'GetPlatformNavigationMenuHandler'],
  ['GetSuperAdminGrowthMonetaryHandler', 'GetPlatformGrowthMonetaryHandler'],
  ['GetSuperAdminGrowthAnalyticsHandler', 'GetPlatformGrowthAnalyticsHandler'],
  ['GetSuperAdminPlansCatalogHandler', 'GetPlatformPlansCatalogHandler'],
  ['GetSuperAdminMetricsHandler', 'GetPlatformMetricsHandler'],
  ['LoginPlatformSuperAdminAsync', 'LoginPlatformOperatorAsync'],
  ['GetPlatformSuperAdminByEmailAsync', 'GetPlatformOperatorByEmailAsync'],
  ['SeedPlatformSuperAdminAsync', 'SeedPlatformOperatorAsync'],
  ['CreatePlatformSuperAdminJwt', 'CreatePlatformOperatorJwt'],
  ['TypeSuperAdmin', 'TypePlatformOperator'],
  ['RemovedSuperAdmins', 'RemovedPlatformOperators'],
  ['hasSuperAdmin', 'hasPlatformOperator'],
  ['superAdmins', 'platformOperators'],
  ['isSuperAdminInTenant', 'isPlatformOperatorInTenant'],
  ['PlatformAuthorizationRoles.SuperAdmin', 'PlatformAuthorizationRoles.PlatformOperator'],
  ['public const string SuperAdmin = "SuperAdmin"', 'public const string PlatformOperator = "PlatformOperator"'],
  ['Roles = "SuperAdmin"', 'Roles = PlatformAuthorizationRoles.PlatformOperator'],
  ['[Authorize(Roles = "SuperAdmin")]', '[Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]'],
  ['role: "SuperAdmin"', 'role: PlatformAuthConstants.JwtPlatformOperatorRole'],
  ['=> "SuperAdmin"', '=> PlatformAuthConstants.JwtPlatformOperatorRole'],
  ['_ => "SuperAdmin"', '_ => PlatformAuthConstants.JwtPlatformOperatorRole'],
  ['"SuperAdmin",', 'PlatformAuthConstants.JwtPlatformOperatorRole,'],
  ['"SuperAdmin")', 'PlatformAuthConstants.JwtPlatformOperatorRole)'],
  ['"SuperAdmin"', 'PlatformAuthConstants.JwtPlatformOperatorRole'],
  ['RefreshUserType.SuperAdmin', 'RefreshUserType.PlatformOperator'],
  ['IsSuperAdmin', 'IsPlatformOnlyFeature'],
  ['useSuperAdmin', 'usePlatformGate'],
  ['SuperAdminLayout', 'PlatformLayout'],
  ['SuperAdminCrudTemplate', 'PlatformCrudTemplate'],
  ['SuperAdminPageTemplate', 'PlatformPageTemplate'],
  ['SuperAdminPanelPage', 'PlatformPanelPage'],
  ['SuperAdminPlansSection', 'PlatformPlansSection'],
  ['SuperAdminMenuBuilder', 'PlatformMenuBuilder'],
  ['superAdminService', 'platformService'],
];

function walk(dir, out = []) {
  if (!fs.existsSync(dir)) return out;
  for (const name of fs.readdirSync(dir)) {
    const p = path.join(dir, name);
    const rel = path.relative(ROOT, p).replace(/\\/g, '/');
    if (SKIP_DIR.test(p)) continue;
    if (SKIP_FILES.has(path.normalize(rel))) continue;
    const st = fs.statSync(p);
    if (st.isDirectory()) walk(p, out);
    else if (/\.(cs|tsx?|json|md|sql|ps1|mjs|example)$/.test(name)) out.push(p);
  }
  return out;
}

let changed = 0;
for (const root of SCAN_ROOTS) {
  for (const file of walk(root)) {
    let text = fs.readFileSync(file, 'utf8');
    let next = text;
    for (const [from, to] of REPLACEMENTS) {
      next = next.split(from).join(to);
    }
    if (next !== text) {
      fs.writeFileSync(file, next, 'utf8');
      changed++;
    }
  }
}
console.log(`Updated ${changed} files.`);
