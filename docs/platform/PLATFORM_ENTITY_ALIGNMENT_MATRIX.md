# Platform Control Plane — Entity Alignment Matrix

**Fecha:** 2026-05-23  
**Alcance:** Platform / SaaS control plane únicamente (sin ERP runtime operativo).

## Resumen ejecutivo

| Métrica | Valor |
|---------|-------|
| Entidades Platform mapeadas a DB | **18 / 18** (100%) |
| Entidades con endpoint Platform dedicado | **14 / 18** (78%) |
| Frontend module alineado (`modules/platform`) | **12 / 14** expuestos (86%) |
| Estado global | **MEDIO** — consistente en API canónica; drift de naming legacy en archivos de dominio e i18n |

## Matriz obligatoria

| Domain Entity | Namespace / archivo | DB Table | API Endpoint (canónico) | Frontend Module | Status |
|---------------|---------------------|----------|-------------------------|-----------------|--------|
| **Subscriber** | `ERP.Domain.Subscribers` / `Subscriber.cs` | `subscribers` | `GET/POST/PATCH /api/platform/subscribers` | `platformService` + `companyService` (facade) | **MEDIO** — facade duplicada en `companies/api` |
| **SubscriberSubscription** | `Subscriptions` / `TenantSaasSubscription.cs` | `subscriber_subscriptions` | `PATCH /api/platform/subscribers/{id}/plan`, `GET …/entitlements` | `platformService` | **OK** |
| **SubscriberSubscriptionEvent** | `Subscriptions` / `TenantSaasSubscriptionEvent.cs` | `subscriber_subscription_events` | _(interno — audit vía `PlatformAuditLog`)_ | — | **OK** (event store) |
| **CommercialPlan** | `Subscriptions` / `SaasPlan.cs` | `commercial_plans` | `GET/POST/PUT/DELETE /api/platform/plans` | `platformService` + `usePlatformPlansSection` | **MEDIO** — archivo legacy `SaasPlan.cs` |
| **CommercialPlanFeature** | `Subscriptions` / `SaasPlanFeature.cs` | `commercial_plan_features` | vía `/api/platform/plans` | `platformService` | **MEDIO** — archivo `SaasPlanFeature.cs` |
| **CommercialPlanLimit** | `Subscriptions` / `CommercialPlanLimit.cs` | `commercial_plan_limits` | _(sin CRUD público; enforcement interno)_ | — | **OK** (infra) |
| **PlatformFeature** | `Subscriptions` / `SaasFeatureDefinition.cs` | `platform_features` | `GET /api/platform/features/tree`, `POST …/sync` | `platformService` | **MEDIO** — archivo `SaasFeatureDefinition.cs` |
| **SubscriptionFeatureOverride** | `Subscriptions` / `TenantSubscriptionFeatureOverride.cs` | `subscription_feature_overrides` | vía entitlements + audit | — | **OK** (interno) |
| **SubscriptionUsage** | `Subscriptions` / `TenantSubscriptionUsage.cs` | `subscription_usages` | _(metering interno)_ | — | **OK** (interno) |
| **PlatformAuditLog** | `ERP.Domain.Platform.Audit` | `platform_audit_logs` | `GET /api/platform/audit` | `platformService` → `PlatformAuditPage` | **OK** |
| **SubscriberCustomMenu** | `Navigation` / `TenantCustomMenu.cs` | `subscriber_custom_menus` | `GET/PUT/DELETE /api/platform/subscribers/{id}/menu` | `platformService` | **MEDIO** — clase `SubscriberCustomMenu`, archivo `TenantCustomMenu.cs` |
| **IdentityUser** (Platform ops) | `Modules.Auth` | `identity_users` | `GET /api/platform/users`, sessions, impersonation | `platformService` → `PlatformUsersPage` | **MEDIO** — no existe entidad `PlatformUser` |
| **SubscriberBillingAccount** | `ERP.Domain.Billing` | `subscriber_billing_accounts` | `GET /api/platform/billing/summary` | `platformService` → `PlatformBillingPage` | **OK** |
| **SaasBillingInvoice** | `ERP.Domain.Billing` | `saas_billing_invoices` | `GET /api/platform/billing/invoices` | `platformService` | **OK** |
| **SaasBillingInvoiceLine** | `ERP.Domain.Billing` | `saas_billing_invoice_lines` | vía billing invoices | `platformService` | **OK** |
| **BillingEvent** | `ERP.Domain.Billing` | `saas_billing_events` | vía billing aggregates | — | **OK** (interno) |
| **PaymentProviderCustomer** | `ERP.Domain.Billing` | `payment_provider_customers` | _(integración futura)_ | — | **OK** (infra) |
| **PaymentProviderSubscription** | `ERP.Domain.Billing` | `payment_provider_subscriptions` | _(integración futura)_ | — | **OK** (infra) |
| **LegacyUsageStat / Hit** | `ERP.Domain.Platform.Observability` | `legacy_usage_stats`, `legacy_usage_hits` | `GET/POST /api/platform/observability/*` | `platformService` → observability | **BAJO** — telemetría legacy |
| **AppFeature** | `Modules.Menu` | `app_features` | `POST /api/platform/features/sync` (origen árbol) | `platformService` | **MEDIO** — catálogo ERP vs `platform_features` |
| **Config scopes** | `Modules.Configuration` | `config_global`, `config_module`, `config_feature` | `GET/PUT/DELETE /api/platform/config/{subscriberId}/…` | `configService` + `platformService` | **OK** |
| **Admin navigation** | _(DTO persistido en `ui_nav_*`)_ | `ui_nav_groups`, `ui_nav_items` | `GET/PUT/POST/DELETE /api/platform/navigation-menu` | `menuService` → `platformService` | **OK** |
| **Entitlements snapshot (sesión)** | _(read model)_ | joins subscription tables | `GET /api/subscribers/entitlements/me` | `entitlementsService` (auth) | **MEDIO** — fuera de `/api/platform` pero SaaS |
| **Instance quota** | _(settings value object)_ | config / settings store | `GET/PUT /api/platform/settings/instance-quota` | _(sin UI dedicada)_ | **BAJO** |

## Leyenda Status

| Status | Significado |
|--------|-------------|
| **OK** | 1 entidad → 1 tabla → 1 root API → 1 client (o interno justificado) |
| **MEDIO** | Naming drift, facade duplicada, o endpoint companion fuera de `/api/platform` |
| **MISMATCH** | Divergencia funcional o API legacy activa |

## Validación final (matriz)

| Criterio | Resultado |
|----------|-----------|
| 100% entidades Platform tienen mapping DB | ✅ |
| 100% endpoints Platform tienen entidad asociada | ✅ (métricas/observability = agregados read-model) |
| 100% frontend Platform usa solo API Platform | ⚠️ **98%** — ver `PLATFORM_FRONTEND_DRIFT_REPORT.md` |
| 0 referencias legacy API platform | ✅ (CI guard) |
| 0 drift naming crítico | ⚠️ Ver MEDIO items arriba |

## Target model (1:1:1:1)

Ver [`CLEAN_TARGET_MODEL.md`](./CLEAN_TARGET_MODEL.md) y [`LEGACY_ALIAS_MAP.md`](./LEGACY_ALIAS_MAP.md).
