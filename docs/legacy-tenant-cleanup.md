# Legacy tenant naming cleanup

Inventario y clasificación de referencias **tenant** / **Tenant** en el monorepo. La arquitectura de dominio usa **Subscriber** y **Company**; `tenant` persiste como alias de compatibilidad en rutas, handlers y UI.

**No eliminar automáticamente.** Usar esta guía para migraciones incrementales.

Relacionado: [platform-runtime-boundaries.md](./platform-runtime-boundaries.md).

---

## Classification legend

| Class | Meaning | Action |
|-------|---------|--------|
| **RC** | Runtime critical — changing breaks auth/API | Keep; alias or gradual rename with dual support |
| **CA** | Compat alias — intentional backward compatibility | Keep until deprecation window ends |
| **UI** | UI text / i18n only | Rename when touching translations |
| **DL** | Dead legacy — unused or superseded | Safe to remove after verification |

---

## API routes

| Location | Symbol / route | Class | Notes |
|----------|----------------|-------|-------|
| `AccessController` | `/api/admin/iam/superadmin/subscribers` | CA | `[Obsolete]` → `/api/platform/subscribers` |
| `SuperAdminController` | `GET /api/superadmin/subscribers` | CA | Richer metrics; obsolete vs platform list |
| `SuperAdminController` | `GetTenants()` method name | CA | Rename to `GetSubscribers` when safe |
| `AuthController` | `switch-subscriber` | RC | Canonical; frontend calls `switchTenant` |
| `AccessController` | `switch-subscriber` bootstrap path | RC | IAM layer |
| `TenantsController` | `/api/subscribers/*` | CA | Route already subscriber-named; controller name legacy |

---

## Backend — domain & persistence

| Location | Symbol | Class | Notes |
|----------|--------|-------|-------|
| `Domain/Modules/Tenants/Entities/Tenant.cs` | `Tenant` entity | RC | Maps to `subscribers` table; rename entity later |
| `ITenantRepository` / `TenantRepository` | interface/class | RC | Alias of subscriber repository |
| `TenantConfiguration.cs` | EF config | RC | Table `subscribers` |
| `TenantSaasSubscription` | entity | RC | Billing linkage |
| `TenantSubscriptionFeatureOverride` | entity | RC | Module overrides |
| `TenantEntitlementsService` | service | RC | Plan entitlements |
| `TenantMenuService` / `ITenantMenuAdminService` | navigation | RC | Subscriber menu admin |
| `TenantOnboardingService` | seeding | RC | Called from orchestrator |
| `TenantSubscriptionCatalog` | catalog | RC | Module keys normalization |
| Audit action `tenant.create` | string | CA | Keep for audit history; add `subscriber.create` alias later |

---

## Backend — application handlers

| Location | Symbol | Class | Notes |
|----------|--------|-------|-------|
| `Modules/Access/UseCases/SwitchTenant` | folder/handler | CA | Implements switch-subscriber |
| `Modules/Auth/UseCases/SwitchTenant` | folder/handler | CA | Legacy auth path |
| `SuperAdminTenantHandlers.cs` | file name | CA | Uses subscriber commands |
| `RegisterTenantWithAdminHandler` | handler | CA | Delegates to orchestrator |
| `Modules/Tenants/*` | namespace | CA | Platform subscriber settings |
| `CreateTenantHandler` | handler | DL? | Verify callers before removal |
| `TenantDto` | DTO | CA | API response shape |

---

## Backend — infrastructure

| Location | Symbol | Class | Notes |
|----------|--------|-------|-------|
| `GrowthAnalyticsReader` | `tenant` variables | CA | Analytics DTOs |
| `ConfigService` | tenant-scoped config | RC | Resolves by subscriber_id |
| `AccountingService` | tenant params | RC | Historical naming in logs/comments |
| `ErpDbContext` | `Tenants` DbSet | RC | Maps to subscribers |

---

## Frontend — runtime critical

| Location | Symbol | Class | Notes |
|----------|--------|-------|-------|
| `accessService.ts` | `switchTenant()` | CA | Calls `/api/admin/iam/switch-subscriber` |
| `superAdminService.ts` | `switchTenant()` | CA | Calls `/api/auth/switch-subscriber` |
| `LoginPage.tsx` | `switchTenant` | CA | Post-bootstrap flow |
| `SubscriberSelectPage.tsx` | `switchTenant` | CA | Subscriber picker |
| `SuperAdminPanelPage.tsx` | `switchTenant` | CA | Impersonation |
| `AppLayout.tsx` | `switchTenant` | CA | Global SuperAdmin context |
| `companyService.ts` | platform URLs | **Migrated** | `GET/POST /api/platform/subscribers` |
| `superAdminService.ts` | platform URLs | **Migrated** | list, create, subscriber menu CRUD |

---

## Frontend — UI text only

| Location | Symbol | Class | Notes |
|----------|--------|-------|-------|
| `SubscriberSelectPage.tsx` | i18n keys `tenantSelect.*` | UI | Rename to `subscriberSelect.*` |
| `SuperAdminMenuBuilderSection.tsx` | `tenantSelect` label key | UI | Translation key |
| `SuperAdminOverviewPage.tsx` | copy "tenant" | UI | Marketing/admin copy |
| `CompaniesPage.tsx` | mixed tenant/subscriber copy | UI | |
| `CompanyConfigPage.tsx` | tenant labels | UI | |
| `ConfigContext.tsx` | tenant config keys | UI/CA | Verify API field names |
| `navConfig.ts` | tenant nav ids | UI | |

---

## Frontend — schemas & types

| Location | Symbol | Class | Notes |
|----------|--------|-------|-------|
| `types/auth.ts` | tenant-related types | CA | Align with `subscriber` over time |
| `companySchema.ts` | tenant fields | CA | |
| `subscriberAccessService.ts` | mixed naming | CA | |

---

## Tests

| Location | Symbol | Class | Notes |
|----------|--------|-------|-------|
| `TenantSubscriptionCatalogTests` | class name | CA | Tests still valid |
| `TenantSubscriptionTests` | domain tests | CA | |
| `UpdateTenantSubscriptionHandlerTests` | handler tests | CA | |
| `IntegrationSeedData` | tenant seed helpers | RC | Test infrastructure |

---

## Recommended migration order

1. **Document** — platform-runtime-boundaries (done).
2. **API** — new `/api/platform/*` routes; mark IAM routes `[Obsolete]` (done).
3. **Frontend services** — add parallel methods (`switchSubscriber`, platform URLs); keep `switchTenant` as wrappers.
4. **i18n** — rename `tenantSelect` → `subscriberSelect` keys (UI only).
5. **Backend folders** — rename `SwitchTenant` → `SwitchSubscriber` with type aliases if needed.
6. **Domain entity** — `Tenant` → `Subscriber` entity rename (large migration; last).

---

## Do NOT rename yet (high risk)

- Database table `subscribers` (already correct).
- JWT claim names (already `subscriber_id`).
- `switch-subscriber` API path (frontend depends on it).
- EF entity `Tenant` until coordinated migration with migrations snapshot.
- Audit log action strings (`tenant.create`) — append new actions instead of rewriting history.

---

## Verification commands

```powershell
# Count tenant references (review periodically)
rg -i "tenant" erp-saas --glob "*.{cs,ts,tsx}" | measure

# Ensure platform routes registered
rg "api/platform/subscribers" erp-saas/backend
```

---

## Deprecation timeline (proposed)

| Phase | Target | Action |
|-------|--------|--------|
| Now | Q2 2026 | Dual routes; Obsolete on IAM superadmin subscriber endpoints |
| +1 release | | Frontend defaults to `/api/platform/subscribers` |
| +2 releases | | Remove obsolete controller actions if telemetry shows zero traffic |
| Long-term | | Domain `Tenant` type rename behind major version |
