# Fase B — Closeout (SaaS enterprise entitlements)

## Resumen

La Fase B completa la transición del modelo legacy (`EnabledModulesJson`) al modelo relacional (`TenantSaasSubscription`, plan features, overrides) en UI, API y persistencia.

| Iteración | Entregable |
|-----------|------------|
| 07 | Frontend fail-closed; `hasModuleRestrictions` desde módulos efectivos |
| 08 | Dejar de escribir JSON; overrides en `tenant_subscription_feature_overrides` |
| 09 | `IgnoreQueryFilters` → `IPlatformQueryAccessor` |
| 10 | Eliminar columna/propiedad JSON; eventos de auditoría; limpieza de catálogo y sesión |

## Fuente de verdad actual

- **Módulos y features:** `ITenantEntitlementsService` / `TenantEntitlementsService`
- **Sesión y JWT:** `ISessionModulesResolver` → entitlements (fail-closed sin suscripción activa)
- **Overrides SuperAdmin:** `ITenantSubscriptionOverridesService`
- **Sync plan:** `Tenant.PlanCode` → `ErpDbContext` crea/actualiza `TenantSaasSubscription` + eventos
- **Consultas cross-tenant:** `IPlatformQueryAccessor` con `PlatformQueryReason`

## Pendiente opcional (post Fase B)

- API de lectura de `tenant_saas_subscription_events` para panel SuperAdmin
- Retirar claim `enabledModules` del JWT si se desea reducir tamaño de token (hoy sigue poblado desde entitlements)
- Actualizar ADRs/inventory históricos que describen el estado pre–Fase B

## Prueba local rápida

```powershell
cd c:\ProyectCursor\erp-saas\backend\src\ERP.API
dotnet run --launch-profile http
# Login: admin@erp.com / Admin123! / tenant-demo → plan starter, 6 módulos canónicos
```
