# SaaS Enterprise Refactor — Fase B iteración 07

**Branch:** `refactor/saas-enterprise-07-fail-closed-ui`  
**Fecha:** 2026-05-20

## Entregables

1. **Frontend fail-closed:** `AppLayout` oculta módulos cuando `enabledModules` está vacío (sin suscripción activa).
2. **`hasModuleRestrictions` derivado:** de módulos efectivos vía `TenantSubscriptionCatalog.HasModuleRestrictionsFromModules` (no `EnabledModulesJson`).
3. **Claves canónicas en UI:** `TENANT_MODULE_KEYS` alineadas a inglés (`CanonicalModuleKeys`).
4. **Sync no destructivo:** `ErpDbContext` usa `ReassignPlan` / `Cancel` en lugar de `Remove` + `Add` en `tenant_saas_subscriptions`.

## Pendiente (08+)

- Dejar de escribir `EnabledModulesJson` en flujos SuperAdmin.
- Migrar IQF residual a `IPlatformQueryAccessor`.
- Unificar `AllModuleKeys` con `CanonicalModuleKeys` en validación de entrada.
