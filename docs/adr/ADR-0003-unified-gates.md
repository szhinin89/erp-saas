# ADR-0003: Unified permission and feature gates

**Estado:** Aceptado (iteración 03)  
**Fecha:** 2026-05-20

---

## Contexto

Tras A1 (`ITenantEntitlementsService`) y A2 (fail-closed en sesión/JWT), seguían **dos rutas** para gating comercial:

1. **`PermissionHandler`** — `TenantSubscriptionCatalog.TenantAllowsPermission` sobre `Tenant.EnabledModulesJson` (sync), con prefijos solo en español; permisos API en inglés (`sales.*`) no mapeaban → **allow implícito**.
2. **`SubscriptionGateBehavior`** — `ISubscriptionService.HasFeatureAsync` con lógica duplicada (plan DB + fallback módulos + `TryMapFeatureToModule` hardcoded).

El menú y el modelo SaaS usan `moduleKey` / `ResourceRef` en **inglés** (`sales`, `inventory`); el catálogo legacy SuperAdmin sigue en español (`ventas`, `inventario`).

---

## Decisión

1. **Mapping único** en `TenantSubscriptionCatalog.TryGetModuleKeyForPermission`: prefijos inglés (API) y español (legacy) → clave canónica de módulo (inglés, alineada con `SaasFeatureDefinition.ResourceRef`).
2. **`TenantAllowsPermissionAsync(tenantId, entitlements, permissionKey)`** — autoridad async vía `GetEnabledModuleKeysAsync`; sin JSON legacy. Prefijos no comerciales (p. ej. `reports.*`) siguen sin restricción por plan.
3. **`PermissionHandler`** y **`GetMyPermissionsHandler`** usan el path async.
4. **`SubscriptionGateBehavior`** usa `ITenantEntitlementsService.HasFeatureAsync` para `[RequireFeature]`; límites/uso siguen en `ISubscriptionService`.
5. **`SubscriptionService.HasFeatureAsync`** delega solo a `ITenantEntitlementsService` (sin fallback `EnabledModulesJson` ni mapa duplicado).

---

## Consecuencias

### Positivas

- Gating HTTP y MediatR leen la misma SoT relacional.
- Permisos `sales.*` / `inventory.*` respetan módulos del plan.
- Menos lógica duplicada en infraestructura.

### Negativas

- Tenants sin filas `SaasFeatureDefinition` de tipo Module pueden perder acceso hasta seed/plan correcto (comportamiento fail-closed deseado).
- `TenantAllowsPermission` sync permanece para tests/compat visual JSON con normalización canónica.

---

## Alternativas consideradas

- Tabla `permission_module_map` en BD — rechazado en Fase A (overhead; mapping estable en código).
- Mantener fail-open en prefijos desconocidos para inglés — rechazado (riesgo de seguridad).

---

## Rollback

Revertir rama `refactor/saas-enterprise-03-unified-gates`; restaurar `PermissionHandler` sync y `SubscriptionService.HasFeatureAsync` con fallback legacy.
