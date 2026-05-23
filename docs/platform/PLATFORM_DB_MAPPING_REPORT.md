# Platform Control Plane — DB Mapping Report

**Fecha:** 2026-05-23  
**Fuente:** `ErpDbContext`, `Persistence/Configurations/*`, `ErpDbContextModelSnapshot`.

## Tablas Platform (PostgreSQL snake_case)

| DB Table | EF Entity | Configuration | DbSet |
|----------|-----------|---------------|-------|
| `subscribers` | `Subscriber` | `TenantConfiguration` | `Subscribers` |
| `subscriber_subscriptions` | `SubscriberSubscription` | `SubscriberSubscriptionConfiguration` | `SubscriberSubscriptions` |
| `subscriber_subscription_events` | `SubscriberSubscriptionEvent` | `SubscriberSubscriptionEventConfiguration` | `SubscriberSubscriptionEvents` |
| `commercial_plans` | `CommercialPlan` | `CommercialPlanConfiguration` | `CommercialPlans` |
| `commercial_plan_features` | `CommercialPlanFeature` | `CommercialPlanFeatureConfiguration` | `CommercialPlanFeatures` |
| `commercial_plan_limits` | `CommercialPlanLimit` | `CommercialPlanLimitConfiguration` | `CommercialPlanLimits` |
| `platform_features` | `PlatformFeature` | `PlatformFeatureConfiguration` | `PlatformFeatures` |
| `subscription_feature_overrides` | `SubscriptionFeatureOverride` | `SubscriptionFeatureOverrideConfiguration` | `SubscriptionFeatureOverrides` |
| `subscription_usages` | `SubscriptionUsage` | `SubscriptionUsageConfiguration` | `SubscriptionUsages` |
| `platform_audit_logs` | `PlatformAuditLog` | `PlatformAuditLogConfiguration` | `PlatformAuditLogs` |
| `subscriber_custom_menus` | `SubscriberCustomMenu` | `TenantCustomMenuConfiguration` | `SubscriberCustomMenus` |
| `subscriber_billing_accounts` | `SubscriberBillingAccount` | `BillingConfiguration` | `SubscriberBillingAccounts` |
| `saas_billing_invoices` | `SaasBillingInvoice` | `BillingConfiguration` | `SaasBillingInvoices` |
| `saas_billing_invoice_lines` | `SaasBillingInvoiceLine` | `BillingConfiguration` | `SaasBillingInvoiceLines` |
| `saas_billing_events` | `BillingEvent` | `BillingConfiguration` | `BillingEvents` |
| `payment_provider_customers` | `PaymentProviderCustomer` | `BillingConfiguration` | `PaymentProviderCustomers` |
| `payment_provider_subscriptions` | `PaymentProviderSubscription` | `BillingConfiguration` | `PaymentProviderSubscriptions` |
| `legacy_usage_stats` | `LegacyUsageStat` | `LegacyUsageStatConfiguration` | `LegacyUsageStats` |
| `legacy_usage_hits` | `LegacyUsageHit` | `LegacyUsageStatConfiguration` | `LegacyUsageHits` |
| `app_features` | `AppFeature` | `AppFeatureConfiguration` | `AppFeatures` |
| `identity_users` | `IdentityUser` | _(Auth config)_ | `IdentityUsers` |
| `ui_nav_groups` / `ui_nav_items` | _(navigation rows)_ | Navigation configs | _(DbSets nav)_ |

## Renombres inconsistentes (archivo ↔ clase ↔ tabla)

| Severidad | Archivo legacy | Clase canónica | Tabla | Notas |
|-----------|----------------|----------------|-------|-------|
| **MEDIO** | `SaasPlan.cs` | `CommercialPlan` | `commercial_plans` | Migración naming Phase 4–5 incompleta en filesystem |
| **MEDIO** | `SaasPlanFeature.cs` | `CommercialPlanFeature` | `commercial_plan_features` | Idem |
| **MEDIO** | `SaasFeatureDefinition.cs` | `PlatformFeature` | `platform_features` | Idem |
| **MEDIO** | `TenantSaasSubscription.cs` | `SubscriberSubscription` | `subscriber_subscriptions` | “Tenant” vs “Subscriber” |
| **MEDIO** | `TenantSaasSubscriptionEvent.cs` | `SubscriberSubscriptionEvent` | `subscriber_subscription_events` | Idem |
| **MEDIO** | `TenantCustomMenu.cs` | `SubscriberCustomMenu` | `subscriber_custom_menus` | Idem |
| **MEDIO** | `TenantSubscriptionFeatureOverride.cs` | `SubscriptionFeatureOverride` | `subscription_feature_overrides` | Prefijo Tenant eliminado en clase |
| **MEDIO** | `TenantSubscriptionUsage.cs` | `SubscriptionUsage` | `subscription_usages` | Idem |

**Tablas:** naming **consistente** snake_case plural.  
**Dominio:** drift en **nombres de archivo** vs clase (no en schema DB).

## Tablas huérfanas / sin API Platform directa

| Tabla | Uso | API | Status |
|-------|-----|-----|--------|
| `commercial_plan_limits` | Enforcement límites plan | Interno (`CommercialPlanLimitsBootstrap`) | **OK** infra |
| `subscription_feature_overrides` | Overrides entitlements | Vía `ISubscriberEntitlementsService` | **OK** interno |
| `subscription_usages` | Metering | Interno | **OK** interno |
| `payment_provider_*` | Stripe/etc. futuro | Sin controller | **OK** reservado |
| `saas_billing_events` | Event sourcing billing | Sin listado público | **OK** interno |
| `legacy_usage_*` | Telemetría migración | `/api/platform/observability/legacy-*` | **BAJO** — candidato a retirada post-migración |
| `billing_settings` | Config ERP tenant | ERP runtime (excluido scope) | N/A |

## Tablas legacy SaaS eliminadas (Phase 4)

| Antes (legacy) | Ahora | Status |
|----------------|-------|--------|
| Rutas `/api/superadmin/*` | `/api/platform/*` | ✅ Eliminado |
| `superAdminService.ts` | `platformService.ts` | ✅ Eliminado |

## FK / relaciones principales

```
subscribers (1) ──< subscriber_subscriptions (N) >── commercial_plans (1)
commercial_plans (1) ──< commercial_plan_features >── platform_features (1)
subscriber_subscriptions (1) ──< subscription_feature_overrides
subscribers (1) ──< subscriber_custom_menus
subscribers (1) ──< subscriber_billing_accounts ──< saas_billing_invoices ──< saas_billing_invoice_lines
subscribers (1) ──< platform_audit_logs (TargetSubscriberId nullable)
```

## Validación DB

| Criterio | Resultado |
|----------|-----------|
| 100% entidades Platform con mapping EF | ✅ 18/18 |
| 0 tablas `superadmin_*` | ✅ |
| Renombres DB inconsistentes | ✅ Ninguno crítico |
| Tablas legacy sin uso | ⚠️ `legacy_usage_*` (observabilidad activa) |
