# Platform Control Plane — Final Consistency Map

**Generated:** 2026-05-23  
**Scope:** Platform Control Plane only (`/api/platform/*`, `modules/platform/*`, `components/platform/*`, domain Platform)  
**Final state:** **CLEAN** (functional); **LOW** cosmetic drift documented below

---

## 1. Executive summary

| Dimension | State | Notes |
|-----------|-------|-------|
| API surface | **CLEAN** | 12 controllers, 100% under `/api/platform/*` |
| API duplication | **CLEAN** | 0 active `/api/subscribers/*` control-plane routes |
| Frontend HTTP client | **CLEAN** | Single `platformService.ts` |
| Frontend routing | **CLEAN** | `platformRoutes.tsx` → `/platform/*` (+ redirect `/superadmin/*`) |
| Domain ↔ DB | **PARTIAL (cosmetic)** | Class/table aligned; legacy **file names** (`SaasPlan.cs`) |
| i18n keys | **CLEAN** | Namespace `platform.*` (es/en/qu) |
| JWT / role | **Intentional** | Role claim canónico `PlatformOperator`; wire legacy solo en `platformAuth.ts` |

---

## 2. Backend inventory

### 2.1 Controllers (`/api/platform/*`)

| Controller | Route | Domain |
|------------|-------|--------|
| `PlatformAuthController` | `/api/platform/auth` | Platform login |
| `PlatformSubscribersController` | `/api/platform/subscribers` | Subscriber lifecycle, menu, entitlements |
| `PlatformPlansController` | `/api/platform/plans` | CommercialPlan CRUD + menu |
| `PlatformFeaturesController` | `/api/platform/features` | PlatformFeature catalog |
| `PlatformNavigationController` | `/api/platform/navigation-menu` | Global UI nav (`ui_nav_*`) |
| `PlatformConfigController` | `/api/platform/config` | Subscriber config overrides |
| `PlatformMetricsController` | `/api/platform/metrics` | KPIs + growth analytics |
| `PlatformAuditController` | `/api/platform/audit` | PlatformAuditLog |
| `PlatformBillingController` | `/api/platform/billing` | SaaS billing summary |
| `PlatformUsersController` | `/api/platform/users` | Platform operators |
| `PlatformObservabilityController` | `/api/platform/observability` | Legacy telemetry + health |
| `PlatformSettingsController` | `/api/platform/settings` | Instance quota |

**Forbidden (verified 0):** `/api/superadmin/*`, duplicate legacy platform controllers.

### 2.2 Application services (Platform)

| Area | Namespace / handlers |
|------|----------------------|
| Subscribers | `ERP.Application.Platform.Subscribers.*`, `Access.UseCases.PlatformSubscribers.*` |
| Plans | `ICommercialPlansAdminService`, plan menu admin |
| Audit | `IPlatformAuditLogger` |
| Observability | `ILegacyEndpointUsageTracker` |
| Navigation | `INavigationMenuAdminService`, `ISubscriberMenuAdminService` |
| Billing read | Platform billing queries via Infrastructure |

> **Naming note (LOW):** Application folder `PlatformSubscribers` is legacy folder name; handlers serve `/api/platform/subscribers` only.

### 2.3 Domain entities → DB tables

| Entity (class) | File (legacy name) | Table | API resource |
|----------------|-------------------|-------|--------------|
| `Subscriber` | `Subscriber.cs` | `subscribers` | `/api/platform/subscribers` |
| `CommercialPlan` | `SaasPlan.cs` | `commercial_plans` | `/api/platform/plans` |
| `CommercialPlanFeature` | `SaasPlanFeature.cs` | `commercial_plan_features` | (embedded in plans) |
| `CommercialPlanLimit` | `CommercialPlanLimit.cs` | `commercial_plan_limits` | (embedded in plans) |
| `PlatformFeature` | `SaasFeatureDefinition.cs` | `platform_features` | `/api/platform/features` |
| `SubscriberSubscription` | `TenantSaasSubscription.cs` | `subscriber_subscriptions` | lifecycle via subscribers |
| `SubscriberSubscriptionEvent` | `TenantSaasSubscriptionEvent.cs` | `subscriber_subscription_events` | audit trail |
| `PlatformAuditLog` | `PlatformAuditLog.cs` | `platform_audit_logs` | `/api/platform/audit` |
| `SubscriberBillingAccount` | Billing entities | `subscriber_billing_accounts` | `/api/platform/billing` |
| `LegacyUsageStat` / `LegacyUsageHit` | Observability | `legacy_usage_*` | `/api/platform/observability` |

### 2.4 Jobs (Hangfire — Platform)

| Job | Purpose |
|-----|---------|
| `CheckSubscriptionExpiryJob` | Grace period + suspend expired subscribers; writes `PlatformAuditLog` |

### 2.5 Middleware (Platform-related)

| Middleware | Role |
|------------|------|
| `PlatformPanelLockMiddleware` | Gates platform panel by deployment flag |
| `DeprecatedApiAttribute` | RFC 8594 for any future legacy surface |
| `EnterpriseDiagnosticMiddleware` | Whitelists `/api/platform`, `/api/subscribers` runtime |

**Removed:** `LegacySubscriberControlPlaneMiddleware` (no legacy routes remain).

---

## 3. Frontend inventory

### 3.1 Single HTTP client (source of truth)

| File | Status |
|------|--------|
| `modules/platform/api/platformService.ts` | **CANONICAL** — all `/api/platform/*` calls |
| `modules/platform/api/platformApiPaths.ts` | Path constants `PLATFORM_API`, `PLATFORM_UI` |
| `constants/platformAuth.ts` | JWT legacy literals + helpers (single source) |
| ~~`subscriberService.ts`~~ | **REMOVED** — merged into `platformService` |
| ~~`menuService.ts`~~ | **REMOVED** — merged into `platformService` |

Auth cross-cutting (allowed, not control plane):

- `switchSubscriber` → `POST /api/auth/switch-subscriber` (inside `platformService`)
- `getPublicPlans` → `GET /api/public/plans` (inside `platformService`)

### 3.2 Pages

| Page | Location | Route |
|------|----------|-------|
| Overview | `modules/platform/pages/PlatformOverviewPage.tsx` | `/platform/overview` |
| Subscribers list | `modules/platform/pages/PlatformSubscribersPage.tsx` | `/platform/subscribers` |
| Subscriber detail | `modules/platform/pages/PlatformSubscriberDetailPage.tsx` | `/platform/subscribers/:id` |
| Plans + menu hub | `pages/Platform/PlatformPlansPage.tsx` | `/platform/plans` |
| Users | `modules/platform/pages/PlatformUsersPage.tsx` | `/platform/users` |
| Billing | `modules/platform/pages/PlatformBillingPage.tsx` | `/platform/billing` |
| Observability | `modules/platform/pages/PlatformObservabilityPage.tsx` | `/platform/observability` |
| Audit | `modules/platform/pages/PlatformAuditPage.tsx` | `/platform/audit` |

**Legacy bookmarks:** `/superadmin/*` → redirect client-side a `/platform/*` (`PlatformLegacyUiRedirect`).

`pages/Platform/*` re-exports exist for lazy route loading — **not duplication**.

**Legacy shell (unrouted):** `PlatformPanelPage`, `PlatformCompaniesShellPage` — retained for bookmark compat docs; canonical routes use split pages above.

### 3.3 Hooks

| Hook | Location |
|------|----------|
| `usePlatformGate` | `hooks/usePlatformGate.ts` |
| `usePlatformPanelPage` | `modules/platform/usePlatformPanelPage.ts` |
| `usePlatformPlansSection` | `components/platform/usePlatformPlansSection.ts` |
| `usePlatformMenuBuilder*` | `components/platform/usePlatformMenuBuilder*.ts` |
| `useNavigationMenuEditorPanel` | `components/platform/useNavigationMenuEditorPanel.ts` |
| `useSubscriberDetailPage` | `modules/platform/pages/subscriber-detail/` |

### 3.4 Stores

No dedicated Zustand store for Platform — session via `authStore`, panel state local/hooks.

---

## 4. Drift detection results

### A. Naming drift

| Issue | Severity | Action |
|-------|----------|--------|
| Domain files `SaasPlan.cs` → class `CommercialPlan` | **LOW** | Cosmetic file rename backlog |
| Application folder `PlatformSubscribers` | **LOW** | Rename to `PlatformSubscribers` (non-blocking) |
| JWT role `SuperAdmin` | **Intentional** | Auth contract — `platformAuth.ts` only |
| URL `/superadmin/*` | **Intentional** | Redirect only; canonical UI is `/platform/*` |
| JSON `requirePlatformPanel` / `superAdminPanelEnabled` | **Intentional** | Backend API field names until API migration |

### B. API drift

| Check | Result |
|-------|--------|
| `/api/superadmin/*` | **0** |
| Platform dup in `/api/subscribers/*` | **0** (runtime only) |
| Platform dup in `/api/admin/*` | **0** (IAM/activity are ERP admin, not used by platform UI) |
| `/api/companies` | ERP operational companies — **out of scope** |

### C. Frontend drift

| Check | Result |
|-------|--------|
| `platformService` / `companyService` | **0** in platform code |
| `subscriberService` / `menuService` wrappers | **Removed** |
| Direct `/api/platform` outside `platformService` | **0** in platform modules |
| Imports `modules/platform` | **0** |
| `isPlatformOperator` / bare `'SuperAdmin'` outside `platformAuth.ts` | **0** (CI guard) |

---

## 5. Source of truth by domain

| Domain | Entity | Table | API | Frontend |
|--------|--------|-------|-----|----------|
| Subscriber | `Subscriber` | `subscribers` | `GET/POST/PATCH /api/platform/subscribers` | `platformService.getSubscribers`, `.getSubscriber`, lifecycle methods |
| Commercial plan | `CommercialPlan` | `commercial_plans` | `/api/platform/plans` | `platformService.listCommercialPlansAdmin`, CRUD |
| Feature | `PlatformFeature` | `platform_features` | `/api/platform/features` | `platformService.getFuncionalidadesArbol` |
| Subscription | `SubscriberSubscription` | `subscriber_subscriptions` | via subscribers lifecycle | `platformService.changePlan`, `.activateSubscriber`, … |
| Config | config entries | `subscriber_config_*` | `/api/platform/config/{id}` | `platformService.resolveSubscriberConfig`, … |
| Audit | `PlatformAuditLog` | `platform_audit_logs` | `/api/platform/audit` | `platformService.getPlatformAudit` |
| Billing | `SubscriberBillingAccount` | `subscriber_billing_*` | `/api/platform/billing` | `platformService.getPlatformBilling*` |
| Nav menu | `UiNavGroup` / `UiNavItem` | `ui_nav_*` | `/api/platform/navigation-menu` | `platformService.getNavigationMenu`, … |

---

## 6. Legacy aliases eliminated

| Removed | Replaced by |
|---------|-------------|
| `POST /api/subscribers` (operador platform create) | `POST /api/platform/subscribers` |
| `PATCH /api/subscribers/{id}/global-parameters` | `PATCH /api/platform/subscribers/{id}/global-parameters` |
| `PATCH /api/subscribers/{id}/subscription` | `PATCH /api/platform/subscribers/{id}/plan` |
| `companyService.ts` | `platformService` + `tenantSubscriberService` (runtime) |
| `subscriberService.ts` | `platformService` |
| `menuService.ts` | `platformService` |
| `SuperAdminController` / `/api/superadmin/*` | `Platform*Controller` / `/api/platform/*` |
| `LegacySubscriberControlPlaneMiddleware` | N/A (routes removed) |

---

## 7. CI verification (enforced)

| Guard | Enforces |
|-------|----------|
| `run-platform-guard.mjs` | No legacy platform API, no `companyService`, no `subscriberService`/`menuService` |
| `validate-subscriber-api-surface.mjs` | `/api/subscribers` only in runtime whitelist files |
| `PlatformControlPlaneGuardTests.cs` | No `/api/superadmin`, no duplicate wrappers, runtime entitlements preserved |

---

## 8. Issues backlog (non-blocking)

### Medium
_None — all functional drift resolved._

### Low
1. Rename domain files `SaasPlan.cs`, `SaasFeatureDefinition.cs`, `TenantSaasSubscription.cs` to match class names.
2. Rename Application namespace/folder `PlatformSubscribers` → `PlatformSubscribers`.
3. Remove deprecated `PlatformPanelPage` / `PlatformCompaniesShellPage` when no external imports remain.
4. Retire `legacy_usage_*` tables when observability legacy hits zero.

---

## 9. Changes applied in this sweep

- Merged `subscriberService` + `menuService` into **`platformService.ts`**
- Updated `useSubscriberDetailPage`, `useNavigationMenuEditorPanel`
- Extended CI guards + architecture test for single client
- Confirmed backend runtime-only `SubscribersController` (no legacy control-plane routes)
- **2026-05-23:** UI routes `/platform/*`, i18n `platform.*`, `platformAuth.ts`, flujo suscriptor unificado, CI `isPlatformOperator` + JWT literal guard
- **2026-05-23:** API JSON aliases `platformPanelEnabled` / `requirePlatformPanel`; `PlatformPanelLockMiddleware`; `PlatformAuthConstants.cs`

---

## 10. Acceptance checklist

- [x] 1 entity → 1 API resource → 1 frontend service method family
- [x] 1 frontend HTTP client (`platformService.ts`)
- [x] 1 API namespace for control plane (`/api/platform/*`)
- [x] 0 duplication with `/api/subscribers/*` for operador platform
- [x] 0 `/api/superadmin/*`
- [x] ERP runtime untouched (`/api/subscribers/entitlements/me`, tenant Admin APIs)

**Verdict: CLEAN** for Platform Control Plane operational consistency.
