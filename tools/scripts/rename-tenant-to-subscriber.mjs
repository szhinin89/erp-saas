#!/usr/bin/env node
/**
 * Physical + symbol cleanup: legacy Tenant naming → Subscriber (canónico NAMING.md).
 * Does NOT touch JWT wire "SuperAdmin", /superadmin/* redirects, or /companies bookmarks.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

/** Explicit path renames (old relative → new relative). Deepest paths first. */
const PATH_RENAMES = [
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/CreateTenant/CreateTenantCommandValidator.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/CreateSubscriber/CreateSubscriberCommandValidator.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/CreateTenant/CreateTenantHandler.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/CreateSubscriber/CreateSubscriberHandler.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/CreateTenant/CreateTenantCommand.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/CreateSubscriber/CreateSubscriberCommand.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateTenantSubscription/UpdateTenantSubscriptionCommandValidator.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberSubscription/UpdateSubscriberSubscriptionCommandValidator.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateTenantSubscription/UpdateTenantSubscriptionHandler.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberSubscription/UpdateSubscriberSubscriptionHandler.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateTenantSubscription/UpdateTenantSubscriptionCommand.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberSubscription/UpdateSubscriberSubscriptionCommand.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateTenantGlobalParameters/UpdateTenantGlobalParametersCommandValidator.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberGlobalParameters/UpdateSubscriberGlobalParametersCommandValidator.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateTenantGlobalParameters/UpdateTenantGlobalParametersHandler.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberGlobalParameters/UpdateSubscriberGlobalParametersHandler.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateTenantGlobalParameters/UpdateTenantGlobalParametersCommand.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberGlobalParameters/UpdateSubscriberGlobalParametersCommand.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateTenantOperationalSettings/UpdateTenantOperationalSettingsValidator.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberOperationalSettings/UpdateSubscriberOperationalSettingsValidator.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateTenantOperationalSettings/UpdateTenantOperationalSettingsHandler.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberOperationalSettings/UpdateSubscriberOperationalSettingsHandler.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateTenantOperationalSettings/UpdateTenantOperationalSettingsCommand.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberOperationalSettings/UpdateSubscriberOperationalSettingsCommand.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdatePasswordResetMode/UpdateTenantPasswordResetModeCommandValidator.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdatePasswordResetMode/UpdateSubscriberPasswordResetModeCommandValidator.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdatePasswordResetMode/UpdateTenantPasswordResetModeHandler.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdatePasswordResetMode/UpdateSubscriberPasswordResetModeHandler.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdatePasswordResetMode/UpdateTenantPasswordResetModeCommand.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdatePasswordResetMode/UpdateSubscriberPasswordResetModeCommand.cs'],
  ['backend/src/ERP.Application/Modules/Access/UseCases/SwitchTenant/SwitchTenantCommandValidator.cs', 'backend/src/ERP.Application/Modules/Access/UseCases/SwitchSubscriber/SwitchSubscriberCommandValidator.cs'],
  ['backend/src/ERP.Application/Modules/Access/UseCases/SwitchTenant/SwitchTenantHandler.cs', 'backend/src/ERP.Application/Modules/Access/UseCases/SwitchSubscriber/SwitchSubscriberHandler.cs'],
  ['backend/src/ERP.Application/Modules/Access/UseCases/SwitchTenant/SwitchTenantCommand.cs', 'backend/src/ERP.Application/Modules/Access/UseCases/SwitchSubscriber/SwitchSubscriberCommand.cs'],
  ['backend/src/ERP.Application/Modules/Auth/UseCases/SwitchTenant/SwitchTenantCommandValidator.cs', 'backend/src/ERP.Application/Modules/Auth/UseCases/SwitchSubscriber/SwitchSubscriberCommandValidator.cs'],
  ['backend/src/ERP.Application/Modules/Auth/UseCases/SwitchTenant/SwitchTenantHandler.cs', 'backend/src/ERP.Application/Modules/Auth/UseCases/SwitchSubscriber/SwitchSubscriberHandler.cs'],
  ['backend/src/ERP.Application/Modules/Auth/UseCases/SwitchTenant/SwitchTenantCommand.cs', 'backend/src/ERP.Application/Modules/Auth/UseCases/SwitchSubscriber/SwitchSubscriberCommand.cs'],
  ['backend/src/ERP.Application/Modules/Access/UseCases/RegisterTenantWithAdmin/RegisterTenantWithAdminCommandValidator.cs', 'backend/src/ERP.Application/Modules/Access/UseCases/RegisterSubscriberWithAdmin/RegisterSubscriberWithAdminCommandValidator.cs'],
  ['backend/src/ERP.Application/Modules/Access/UseCases/RegisterTenantWithAdmin/RegisterTenantWithAdminHandler.cs', 'backend/src/ERP.Application/Modules/Access/UseCases/RegisterSubscriberWithAdmin/RegisterSubscriberWithAdminHandler.cs'],
  ['backend/src/ERP.Application/Modules/Access/UseCases/RegisterTenantWithAdmin/RegisterTenantWithAdminCommand.cs', 'backend/src/ERP.Application/Modules/Access/UseCases/RegisterSubscriberWithAdmin/RegisterSubscriberWithAdminCommand.cs'],
  ['backend/src/ERP.Application/Modules/Access/UseCases/TenantAccess/TenantMembershipHandlers.cs', 'backend/src/ERP.Application/Modules/Access/UseCases/SubscriberAccess/SubscriberMembershipHandlers.cs'],
  ['backend/src/ERP.Application/Modules/Access/UseCases/TenantAccess/TenantMembershipCommands.cs', 'backend/src/ERP.Application/Modules/Access/UseCases/SubscriberAccess/SubscriberMembershipCommands.cs'],
  ['backend/src/ERP.Application/Modules/Access/UseCases/TenantAccess/TenantMembershipDtos.cs', 'backend/src/ERP.Application/Modules/Access/UseCases/SubscriberAccess/SubscriberMembershipDtos.cs'],
  ['backend/src/ERP.Application/Subscriptions/ITenantEntitlementsService.cs', 'backend/src/ERP.Application/Subscriptions/ISubscriberEntitlementsService.cs'],
  ['backend/src/ERP.Application/Subscriptions/ITenantSubscriptionOverridesService.cs', 'backend/src/ERP.Application/Subscriptions/ISubscriptionFeatureOverridesService.cs'],
  ['backend/src/ERP.Application/Subscriptions/TenantEntitlementsSnapshot.cs', 'backend/src/ERP.Application/Subscriptions/SubscriberEntitlementsSnapshot.cs'],
  ['backend/src/ERP.Application/Common/Interfaces/ITenantOnboardingService.cs', 'backend/src/ERP.Application/Common/Interfaces/ISubscriberOnboardingService.cs'],
  ['backend/src/ERP.Application/Common/TenantSubscriptionCatalog.cs', 'backend/src/ERP.Application/Common/SubscriberSubscriptionCatalog.cs'],
  ['backend/src/ERP.Application/Navigation/ITenantMenuAdminService.cs', 'backend/src/ERP.Application/Navigation/ISubscriberMenuAdminService.cs'],
  ['backend/src/ERP.Application/Navigation/ITenantSessionMenuResolver.cs', 'backend/src/ERP.Application/Navigation/ISubscriberSessionMenuResolver.cs'],
  ['backend/src/ERP.Infrastructure/Services/CurrentTenantService.cs', 'backend/src/ERP.Infrastructure/Services/CurrentSubscriberService.cs'],
  ['backend/src/ERP.Infrastructure/Services/ManualCurrentTenant.cs', 'backend/src/ERP.Infrastructure/Services/ManualCurrentSubscriber.cs'],
  ['backend/src/ERP.Infrastructure/Services/JobTenantContext.cs', 'backend/src/ERP.Infrastructure/Services/JobSubscriberContext.cs'],
  ['backend/src/ERP.Infrastructure/Services/TenantEntitlementsService.cs', 'backend/src/ERP.Infrastructure/Services/SubscriberEntitlementsService.cs'],
  ['backend/src/ERP.Infrastructure/Services/TenantSubscriptionOverridesService.cs', 'backend/src/ERP.Infrastructure/Services/SubscriptionFeatureOverridesService.cs'],
  ['backend/src/ERP.Infrastructure/Seeding/TenantOnboardingService.cs', 'backend/src/ERP.Infrastructure/Seeding/SubscriberOnboardingService.cs'],
  ['backend/src/ERP.Infrastructure/Persistence/TenantIamMenuMerger.cs', 'backend/src/ERP.Infrastructure/Persistence/SubscriberIamMenuMerger.cs'],
  ['backend/src/ERP.Infrastructure/Persistence/Configurations/TenantConfiguration.cs', 'backend/src/ERP.Infrastructure/Persistence/Configurations/SubscriberConfiguration.cs'],
  ['backend/src/ERP.Infrastructure/Persistence/Configurations/TenantCustomMenuConfiguration.cs', 'backend/src/ERP.Infrastructure/Persistence/Configurations/SubscriberCustomMenuConfiguration.cs'],
  ['backend/src/ERP.Infrastructure/Persistence/Configurations/TenantConfigurationHierarchyConfig.cs', 'backend/src/ERP.Infrastructure/Persistence/Configurations/ConfigHierarchyConfigurations.cs'],
  ['backend/src/ERP.Domain/Modules/Common/ITenantEntity.cs', 'backend/src/ERP.Domain/Modules/Common/ISubscriberScopedEntity.cs'],
  ['backend/src/ERP.Domain/Modules/Common/IMustHaveTenant.cs', 'backend/src/ERP.Domain/Modules/Common/IMustHaveSubscriber.cs'],
  ['backend/src/ERP.Domain/Navigation/Entities/TenantCustomMenu.cs', 'backend/src/ERP.Domain/Navigation/Entities/SubscriberCustomMenu.cs'],
  ['backend/src/ERP.Domain/Subscriptions/Entities/TenantSubscriptionFeatureOverride.cs', 'backend/src/ERP.Domain/Subscriptions/Entities/SubscriptionFeatureOverride.cs'],
  ['backend/src/ERP.Domain/Subscriptions/Entities/TenantSubscriptionStatus.cs', 'backend/src/ERP.Domain/Subscriptions/Entities/SubscriptionStatus.cs'],
  ['backend/src/ERP.Domain/Subscriptions/Entities/TenantSubscriptionUsage.cs', 'backend/src/ERP.Domain/Subscriptions/Entities/SubscriptionUsage.cs'],
  ['backend/src/ERP.API/Hangfire/HangfireTenantContextFilter.cs', 'backend/src/ERP.API/Hangfire/HangfireSubscriberContextFilter.cs'],
  ['backend/src/ERP.API.Tests/Support/MutableCurrentTenant.cs', 'backend/src/ERP.API.Tests/Support/MutableCurrentSubscriber.cs'],
  ['backend/src/ERP.Infrastructure.Tests/Services/TenantEntitlementsServiceTests.cs', 'backend/src/ERP.Infrastructure.Tests/Services/SubscriberEntitlementsServiceTests.cs'],
  ['backend/src/ERP.Infrastructure.Tests/Services/TenantSubscriptionOverridesServiceTests.cs', 'backend/src/ERP.Infrastructure.Tests/Services/SubscriptionFeatureOverridesServiceTests.cs'],
  ['backend/src/ERP.Application.Tests/UpdateTenantSubscriptionHandlerTests.cs', 'backend/src/ERP.Application.Tests/UpdateSubscriberSubscriptionHandlerTests.cs'],
  ['backend/src/ERP.Application.Tests/TenantSubscriptionCatalogTests.cs', 'backend/src/ERP.Application.Tests/SubscriberSubscriptionCatalogTests.cs'],
  ['backend/src/ERP.Domain.Tests/TenantSubscriptionTests.cs', 'backend/src/ERP.Domain.Tests/SubscriberSubscriptionTests.cs'],
  ['frontend/src/modules/subscribers/api/tenantSubscriberService.ts', 'frontend/src/modules/subscribers/api/runtimeSubscriberService.ts'],
  ['qa-engine/suites/tenant/tenantSuite.js', 'qa-engine/suites/subscriber/subscriberSuite.js'],
  ['tools/architecture/check-backend-subscriber-rules.mjs', 'tools/architecture/check-backend-subscriber-rules.mjs'],
];

const SKIP_DIR = /[\\/](Migrations|node_modules)([\\/]|$)/;
const SCAN_ROOTS = [
  path.join(ROOT, 'backend/src'),
  path.join(ROOT, 'backend/src/ERP.Architecture.Tests'),
  path.join(ROOT, 'frontend/src'),
  path.join(ROOT, 'tools'),
  path.join(ROOT, 'qa-engine'),
  path.join(ROOT, 'scripts'),
  path.join(ROOT, 'AI-RULES'),
  path.join(ROOT, '.cursor/rules'),
  path.join(ROOT, 'docs/platform'),
];

const SYMBOL_REPLACEMENTS = [
  ['HangfireTenantContextFilter', 'HangfireSubscriberContextFilter'],
  ['GetResolvedMenuForTenantAsync', 'GetResolvedMenuForSubscriberAsync'],
  ['SeedForTenantAsync', 'SeedForSubscriberAsync'],
  ['UpsertTenantCompanyUserMembership', 'UpsertSubscriberCompanyUserMembership'],
  ['RevokeTenantCompanyUserMembership', 'RevokeSubscriberCompanyUserMembership'],
  ['RegisterTenant(', 'RegisterSubscriber('],
  ['tenantSubscriberService', 'runtimeSubscriberService'],
  ['TenantSubscriberDetailDto', 'RuntimeSubscriberDetailDto'],
  ['UpdateTenantSubscriberCompanyBody', 'UpdateRuntimeSubscriberCompanyBody'],
  ['runCheckBackendTenantRules', 'runCheckBackendSubscriberRules'],
  ['check-backend-subscriber-rules.mjs', 'check-backend-subscriber-rules.mjs'],
  ['runTenantSuite', 'runSubscriberSuite'],
  ['suites/tenant/tenantSuite.js', 'suites/subscriber/subscriberSuite.js'],
  ['tenantMenuAdmin', 'subscriberMenuAdmin'],
  ["'/api/admin/iam/register-tenant',\n  ", ''],
  ['companiesTenantDetailNav.ts', 'platformSubscriberDetailNav.ts'],
  ['detailTenantId', 'detailSubscriberId'],
  ['subscriptionTenantId', 'subscriptionSubscriberId'],
  ['goToCompaniesTenantDetail', 'goToSubscriberDetail'],
  ['goToCompaniesTenantSubscription', 'goToSubscriberSubscription'],
  ['companiesTenantDetailNav', 'platformSubscriberDetailNav'],
  ['/api/setup/superadmin', '/api/setup/platform-operator'],
  ['identity.claim-superadmin', 'identity.claim-platform-operator'],
  ['claim superadmin failed', 'claim platform-operator failed'],
  ['SEED_MANIFEST.superAdmin', 'SEED_MANIFEST.platformOperator'],
  ['superAdmin:', 'platformOperator:'],
  ['tenantA:', 'subscriberA:'],
  ['tenantB:', 'subscriberB:'],
  ['runTenantSuite', 'runSubscriberSuite'],
];

function renamePath(relFrom, relTo) {
  const from = path.join(ROOT, relFrom);
  const to = path.join(ROOT, relTo);
  if (!fs.existsSync(from)) {
    console.warn(`SKIP missing: ${relFrom}`);
    return false;
  }
  fs.mkdirSync(path.dirname(to), { recursive: true });
  fs.renameSync(from, to);
  console.log(`  ${relFrom} → ${relTo}`);
  return true;
}

function removeEmptyDirs(dir) {
  if (!fs.existsSync(dir)) return;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    if (fs.statSync(full).isDirectory()) removeEmptyDirs(full);
  }
  if (fs.readdirSync(dir).length === 0) {
    fs.rmdirSync(dir);
    console.log(`  RMDIR ${path.relative(ROOT, dir)}`);
  }
}

function walkFiles(dir, acc = []) {
  if (!fs.existsSync(dir) || SKIP_DIR.test(dir)) return acc;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    if (SKIP_DIR.test(full)) continue;
    const st = fs.statSync(full);
    if (st.isDirectory()) walkFiles(full, acc);
    else acc.push(full);
  }
  return acc;
}

function applySymbols() {
  const exts = new Set(['.cs', '.ts', '.tsx', '.js', '.mjs', '.md', '.mdc', '.json', '.ps1']);
  let changed = 0;
  for (const root of SCAN_ROOTS) {
    if (!fs.existsSync(root)) continue;
    for (const file of walkFiles(root)) {
      if (file.includes('rename-tenant-to-subscriber.mjs')) continue;
      if (!exts.has(path.extname(file))) continue;
      let text = fs.readFileSync(file, 'utf8');
      let next = text;
      for (const [from, to] of SYMBOL_REPLACEMENTS) {
        next = next.split(from).join(to);
      }
      if (next !== text) {
        fs.writeFileSync(file, next, 'utf8');
        changed++;
        console.log(`  text ${path.relative(ROOT, file)}`);
      }
    }
  }
  console.log(`Symbol pass: ${changed} files updated.`);
}

function main() {
  console.log('=== Physical renames ===');
  const sorted = [...PATH_RENAMES].sort(
    (a, b) => b[0].split('/').length - a[0].split('/').length,
  );
  for (const [from, to] of sorted) renamePath(from, to);

  // Remove empty legacy folders
  const emptyCandidates = [
    'backend/src/ERP.Application/Modules/Subscribers/UseCases/CreateTenant',
    'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateTenantSubscription',
    'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateTenantGlobalParameters',
    'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateTenantOperationalSettings',
    'backend/src/ERP.Application/Modules/Access/UseCases/SwitchTenant',
    'backend/src/ERP.Application/Modules/Auth/UseCases/SwitchTenant',
    'backend/src/ERP.Application/Modules/Access/UseCases/RegisterTenantWithAdmin',
    'backend/src/ERP.Application/Modules/Access/UseCases/TenantAccess',
    'qa-engine/suites/tenant',
  ];
  for (const d of emptyCandidates) removeEmptyDirs(path.join(ROOT, d));

  console.log('\n=== Symbol replacements ===');
  applySymbols();
  console.log('\nDone.');
}

main();
