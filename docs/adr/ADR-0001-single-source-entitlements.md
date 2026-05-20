# ADR-0001: Single source of truth for tenant entitlements

**Estado:** Propuesto (contexto documentado en iteración 00; decisión pendiente iteración 01)  
**Fecha:** 2026-05-20

---

## Contexto

El ERP SaaS en `erp-saas/backend` controla qué módulos y features comerciales puede usar cada tenant mediante varios mecanismos que evolucionaron en paralelo:

1. **Modelo legacy en `Tenant`:** `PlanCode` y `EnabledModulesJson`, interpretados por la clase estática `TenantSubscriptionCatalog`. Si el JSON es null, vacío o inválido, el sistema devuelve `AllModuleKeys` (lista fija de 9 módulos en español: `ventas`, `inventario`, etc.) — comportamiento **fail-open**.

2. **Modelo relacional SaaS:** `TenantSaasSubscription` (suscripción activa por tenant), `SaasPlan` / `SaasPlanFeature`, `SaasFeatureDefinition`, `TenantSubscriptionFeatureOverride` y `TenantSubscriptionUsage` para cuotas medidas. `SubscriptionService` consulta este modelo pero, cuando no resuelve una feature en plan, **vuelve** a `EnabledModulesJson` y a un mapa hardcoded `TryMapFeatureToModule`.

3. **Capas de consumo distintas:**
   - **UI / JWT / permisos de sesión:** auth handlers y `GetMyPermissionsHandler` usan `TenantSubscriptionCatalog`.
   - **Autorización HTTP:** `PermissionHandler` usa `TenantAllowsPermission` sobre el tenant cargado desde repositorio (misma fuente JSON).
   - **MediatR:** `SubscriptionGateBehavior` usa `ISubscriptionService` (modelo mixto).

Además existe **drift de vocabulario**: los permisos reales del API usan prefijos en inglés (`sales.*`, `inventory.*`), mientras el catálogo legacy usa prefijos en español (`ventas.*`, `inventario.*`). Para claves desconocidas, `TenantAllowsPermission` devuelve **true** por diseño, de modo que el gating por plan en la capa HTTP no restringe la mayoría de endpoints actuales.

El frontend replica fail-open: si `enabledModules` llega vacío, muestra todos los grupos de menú (`AppLayout.moduleEntitled`).

No hay entidad `Subscriber`; `Tenant` concentra identidad operativa y comercial. No hay KV de límites por plan; los límites viven en `SaasPlanFeature.LimitPerPeriod` y overrides.

La sincronización `ErpDbContext.SyncTenantSubscriptionsFromPlanCodeAsync` mantiene `TenantSaasSubscription` alineada con `PlanCode` de forma **destructiva** (remove + insert), sin historial.

**Inventario detallado:** [saas-enterprise-inventory.md](../refactor/saas-enterprise-inventory.md).

---

## Decisión

*(Pendiente — iteración 01.)*

---

## Consecuencias

*(Pendiente — iteración 01.)*

---

## Alternativas

*(Pendiente — iteración 01.)*

---

## Rollback

*(Pendiente — iteración 01.)*
