#!/usr/bin/env node
/**
 * Physical + symbol cleanup: legacy Saas* filenames → canonical Commercial/Subscriber/Platform names.
 * Keeps appsettings section keys (Saas:Entitlements, SaasEntitlementsCache) for config wire-compat.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

const PATH_RENAMES = [
  ['backend/src/ERP.Application/Subscriptions/ISaasPlansAdminService.cs', 'backend/src/ERP.Application/Subscriptions/ICommercialPlansAdminService.cs'],
  ['backend/src/ERP.Application/Subscriptions/ISaasCatalogQuery.cs', 'backend/src/ERP.Application/Subscriptions/ICommercialCatalogQuery.cs'],
  ['backend/src/ERP.Application/Subscriptions/ISaasPublicPlansQuery.cs', 'backend/src/ERP.Application/Subscriptions/IPublicCommercialPlansQuery.cs'],
  ['backend/src/ERP.Application/Subscriptions/SaasPlansAdminDtos.cs', 'backend/src/ERP.Application/Subscriptions/CommercialPlansAdminDtos.cs'],
  ['backend/src/ERP.Application/Subscriptions/SaasPlanCatalogDtos.cs', 'backend/src/ERP.Application/Subscriptions/CommercialPlanCatalogDtos.cs'],
  ['backend/src/ERP.Application/Common/Config/SaasEntitlementsOptions.cs', 'backend/src/ERP.Application/Common/Config/SubscriberEntitlementsOptions.cs'],
  ['backend/src/ERP.Application/Subscriptions/Caching/SaasEntitlementsCacheOptions.cs', 'backend/src/ERP.Application/Subscriptions/Caching/SubscriberEntitlementsCacheOptions.cs'],
  ['backend/src/ERP.Infrastructure/Persistence/SaasPlansAdminService.cs', 'backend/src/ERP.Infrastructure/Persistence/CommercialPlansAdminService.cs'],
  ['backend/src/ERP.Infrastructure/Persistence/SaasPlansBootstrap.cs', 'backend/src/ERP.Infrastructure/Persistence/CommercialPlansBootstrap.cs'],
  ['backend/src/ERP.Infrastructure/Persistence/Saas/SaasCatalogQuery.cs', 'backend/src/ERP.Infrastructure/Persistence/CommercialCatalogQuery.cs'],
  ['backend/src/ERP.Domain/Subscriptions/SaasFeatureKind.cs', 'backend/src/ERP.Domain/Subscriptions/PlatformFeatureKind.cs'],
  ['backend/src/ERP.Domain/Subscriptions/SaasBillingCycle.cs', 'backend/src/ERP.Domain/Subscriptions/CommercialBillingCycle.cs'],
  ['backend/src/ERP.Domain.Tests/Subscriptions/SaasPlanLayoutTests.cs', 'backend/src/ERP.Domain.Tests/Subscriptions/CommercialPlanLayoutTests.cs'],
  ['backend/src/ERP.API/Controllers/SaasEntitlementsController.cs', 'backend/src/ERP.API/Controllers/SubscriberEntitlementsController.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberSaasProfile/UpdateSubscriberSaasProfileCommandValidator.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberCommercialProfile/UpdateSubscriberCommercialProfileCommandValidator.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberSaasProfile/UpdateSubscriberSaasProfileHandler.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberCommercialProfile/UpdateSubscriberCommercialProfileHandler.cs'],
  ['backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberSaasProfile/UpdateSubscriberSaasProfileCommand.cs', 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberCommercialProfile/UpdateSubscriberCommercialProfileCommand.cs'],
];

const SKIP = /[\\/](Migrations|node_modules)([\\/]|$)/;

const SYMBOL_REPLACEMENTS = [
  ['SaasEntitlementsOptions', 'SubscriberEntitlementsOptions'],
  ['SaasEntitlementsCacheOptions', 'SubscriberEntitlementsCacheOptions'],
  ['ISaasPublicPlansQuery', 'IPublicCommercialPlansQuery'],
  ['SaasPublicPlanDto', 'PublicCommercialPlanDto'],
  ['SaasPublicPlanFeatureDto', 'PublicCommercialPlanFeatureDto'],
  ['UpdateSubscriberSaasProfile', 'UpdateSubscriberCommercialProfile'],
  ['SaasPlansAdminService.cs', 'CommercialPlansAdminService.cs'],
  ['SaasPlansBootstrap.cs', 'CommercialPlansBootstrap.cs'],
  ['Saas/SaasCatalogQuery.cs', 'CommercialCatalogQuery.cs'],
  ['SaasPlanCatalogDtos.cs', 'CommercialPlanCatalogDtos.cs'],
  ['SaasPlansAdminDtos.cs', 'CommercialPlansAdminDtos.cs'],
  ['ISaasPlansAdminService.cs', 'ICommercialPlansAdminService.cs'],
  ['ISaasCatalogQuery.cs', 'ICommercialCatalogQuery.cs'],
  ['ISaasPublicPlansQuery.cs', 'IPublicCommercialPlansQuery.cs'],
  ['SaasFeatureKind.cs', 'PlatformFeatureKind.cs'],
  ['SaasBillingCycle.cs', 'CommercialBillingCycle.cs'],
  ['SaasPlanLayoutTests.cs', 'CommercialPlanLayoutTests.cs'],
  ['SaasEntitlementsController.cs', 'SubscriberEntitlementsController.cs'],
  ['| 1 | Renombrar archivos dominio `SaasPlan.cs` → `CommercialPlan.cs`, etc. | ⏳ backlog cosmético |', '| 1 | Renombrar archivos dominio `SaasPlan.cs` → `CommercialPlan.cs`, etc. | ✅ (2026-05-25) |'],
  ['Outbox `TenantId` (columna) | `OutboxMessages.TenantId` | Propiedad EF `SubscriberId` (mapeo column) |', 'Outbox legacy column | `OutboxMessages.SubscriberId` | Renombrado desde `TenantId` (2026-05-25) |'],
];

function renamePath(relFrom, relTo) {
  const from = path.join(ROOT, relFrom);
  const to = path.join(ROOT, relTo);
  if (!fs.existsSync(from)) {
    console.warn(`SKIP missing: ${relFrom}`);
    return;
  }
  fs.mkdirSync(path.dirname(to), { recursive: true });
  fs.renameSync(from, to);
  console.log(`  ${relFrom} → ${relTo}`);
}

function removeEmptyDirs(dir) {
  if (!fs.existsSync(dir)) return;
  for (const name of fs.readdirSync(dir)) {
    removeEmptyDirs(path.join(dir, name));
  }
  if (fs.readdirSync(dir).length === 0) {
    fs.rmdirSync(dir);
    console.log(`  RMDIR ${path.relative(ROOT, dir)}`);
  }
}

function walk(dir, acc = []) {
  if (!fs.existsSync(dir) || SKIP.test(dir)) return acc;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    if (SKIP.test(full)) continue;
    const st = fs.statSync(full);
    if (st.isDirectory()) walk(full, acc);
    else if (/\.(cs|ts|tsx|js|mjs|md|json|mdc)$/.test(name)) acc.push(full);
  }
  return acc;
}

function main() {
  console.log('=== Saas physical renames ===');
  for (const [from, to] of PATH_RENAMES) renamePath(from, to);
  removeEmptyDirs(path.join(ROOT, 'backend/src/ERP.Infrastructure/Persistence/Saas'));
  removeEmptyDirs(path.join(ROOT, 'backend/src/ERP.Application/Modules/Subscribers/UseCases/UpdateSubscriberSaasProfile'));

  const roots = [
    path.join(ROOT, 'backend/src'),
    path.join(ROOT, 'backend/src/ERP.Architecture.Tests'),
    path.join(ROOT, 'docs/platform'),
    path.join(ROOT, 'tools'),
  ];

  console.log('\n=== Symbol replacements ===');
  let changed = 0;
  for (const root of roots) {
    if (!fs.existsSync(root)) continue;
    for (const file of walk(root)) {
      if (file.includes('rename-saas-physical.mjs')) continue;
      let text = fs.readFileSync(file, 'utf8');
      let next = text;
      for (const [from, to] of SYMBOL_REPLACEMENTS) next = next.split(from).join(to);
      if (next !== text) {
        fs.writeFileSync(file, next, 'utf8');
        changed++;
        console.log(`  ${path.relative(ROOT, file)}`);
      }
    }
  }
  console.log(`Updated ${changed} files.\nDone.`);
}

main();
