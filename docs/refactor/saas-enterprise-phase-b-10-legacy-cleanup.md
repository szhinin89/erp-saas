# Fase B — Iteración 10: limpieza legacy y auditoría de suscripción

## Objetivo

Eliminar `Tenant.EnabledModulesJson` y rutas de lectura legacy; registrar cambios de plan y overrides en tabla de eventos.

## Cambios backend

1. **`Tenant`** — se elimina `EnabledModulesJson` y `SetSubscription`; solo `PlanCode` + `SetPlanCode`.
2. **EF** — migración que elimina columna `tenants.enabled_modules` y crea `tenant_saas_subscription_events`.
3. **`TenantSaasSubscriptionEvent`** — tipos: `plan_assigned`, `plan_changed`, `plan_cancelled`, `module_overrides_applied`, `module_overrides_cleared`.
4. **`ErpDbContext.SyncTenantSubscriptionsFromPlanCodeAsync`** — escribe eventos al crear, reasignar o cancelar suscripción.
5. **`TenantSubscriptionOverridesService`** — escribe eventos al aplicar o limpiar overrides de módulos.
6. **`TenantSubscriptionCatalog`** — eliminados `GetEffectiveEnabledModules` y `TenantAllowsPermission` (sync).
7. **`SessionModulesResolver`** — solo delega a `ITenantEntitlementsService` (sin flag legacy).
8. **`SaasEntitlementsOptions`** — eliminado `PreferLegacyEnabledModulesJsonForSession`.

## Verificación

```powershell
cd c:\ProyectCursor\erp-saas\backend\src\ERP.Domain.Tests
dotnet test

cd c:\ProyectCursor\erp-saas\backend\src\ERP.Application.Tests
dotnet test --filter "FullyQualifiedName~TenantSubscription|FullyQualifiedName~LoginHandler|FullyQualifiedName~UpdateTenantSubscription"

cd c:\ProyectCursor\erp-saas\backend\src\ERP.Infrastructure.Tests
dotnet test --filter "FullyQualifiedName~TenantEntitlements|FullyQualifiedName~TenantSubscription|FullyQualifiedName~IgnoreQueryFilters"
```

## Rollback

- Restaurar rama anterior a la migración; no hay flag de JSON legacy en runtime tras esta iteración.
