# SaaS Enterprise Refactor — Fase B iteración 08

**Branch:** `refactor/saas-enterprise-08-stop-legacy-json`

## Entregables

1. **`Tenant.SetPlanCode`** — solo actualiza `PlanCode` y limpia `EnabledModulesJson` (deja de escribir caché JSON).
2. **`ITenantSubscriptionOverridesService`** — restricciones explícitas de módulos vía `tenant_subscription_feature_overrides`.
3. **Validación** — `ValidateModuleKeysOrThrow` usa `CanonicalModuleKeys`; acepta alias español (`ventas` → `sales`).
4. **Handlers** — `UpdateTenantSubscription`, `CreateTenant`, `SuperAdminCreateTenantWithAdmin` aplican overrides tras `SaveChanges` (sync de suscripción).

## Contrato API (sin breaking)

- Request sigue enviando `enabledModules`; el backend ya no persiste JSON en `tenants.enabled_modules`.
- Response `enabledModules` sigue siendo la lista **efectiva** desde entitlements.

## Pendiente (09–10)

- Migrar IQF residual.
- Retirar columna `enabled_modules` y flag `PreferLegacyEnabledModulesJsonForSession`.
