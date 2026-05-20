# ADR-0001: Single source of truth for tenant entitlements

**Estado:** Aceptado (iteración 01 — servicio introducido; consumidores legacy pendientes Fase A)  
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

La **fuente única de verdad** para entitlements comerciales (módulos y features) es el **modelo de suscripción relacional**:

`TenantSaasSubscription` (activa) → `SaasPlanFeature` + `SaasFeatureDefinition` + `TenantSubscriptionFeatureOverride`.

Se introduce `ITenantEntitlementsService` / `TenantEntitlementsService` como API de lectura canónica:

- `GetEnabledModuleKeysAsync` — features con `SaasFeatureKind.Module` efectivamente incluidas; clave = `ResourceRef` (o código en minúsculas si falta ref).
- `HasFeatureAsync` — por `SaasFeatureDefinition.Code` (normalizado uppercase).
- `GetLimitPerPeriodAsync` — límite efectivo (override > plan); solo si feature medida e incluida.

**Reglas fail-closed:** sin suscripción activa → módulos vacíos, `HasFeature` = false, límite null. No se lee `EnabledModulesJson` ni `TenantSubscriptionCatalog` dentro de este servicio.

**Alcance iteración 01:** el servicio existe y está registrado en DI; **no** reemplaza aún `PermissionHandler`, `SubscriptionService` ni JWT. La migración de consumidores es incremental (iteraciones 02–03).

---

## Consecuencias

### Positivas

- Lectura de entitlements centralizada y testeable.
- Comportamiento explícito deny-by-default sin suscripción.
- Base para unificar UI, HTTP y MediatR en iteraciones siguientes.

### Negativas / deuda inmediata

- **Doble lectura temporal:** legacy (`TenantSubscriptionCatalog`, `SubscriptionService` con fallback) sigue activo → drift hasta iteración 03.
- Tenants sin fila en `tenant_saas_subscriptions` quedarán “sin módulos” cuando los consumidores migren (hoy mitigado porque aún no migran).
- `GetEnabledModuleKeys` depende de `ResourceRef` en features `Module`; catálogo incompleto en BD → lista vacía (correcto fail-closed, requiere datos en `saas_feature_definitions`).

### Operativas

- Garantizar suscripción activa al provisionar tenant (sync desde `PlanCode` o alta explícita).
- Poblar `SaasPlanFeature` / definiciones con `Kind=Module` y `ResourceRef` alineado al menú (inglés: `sales`, `inventory`, …).

---

## Alternativas

| Alternativa | Motivo de rechazo |
|-------------|------------------|
| Mantener `EnabledModulesJson` como SoT | Drift con plan; fail-open; no refleja overrides ni límites |
| Extender solo `SubscriptionService` | Mezcla features medidas + módulos + fallback legacy; API poco clara para UI |
| KV `PlanConfiguration` por plan | No existe en el modelo; duplicaría `SaasPlanFeature` |
| Catálogo estático `TenantSubscriptionCatalog` | No persiste overrides ni planes dinámicos SuperAdmin |

---

## Rollback

1. Eliminar registro DI `ITenantEntitlementsService` en `DependencyInjection.cs`.
2. Borrar `ITenantEntitlementsService.cs`, `TenantEntitlementsService.cs` y tests asociados.
3. Revertir ADR a estado “propuesto” o eliminar sección Decisión.
4. Sin cambio de esquema BD — rollback solo de código.

Los consumidores que aún no usen el servicio no se ven afectados al revertir.
