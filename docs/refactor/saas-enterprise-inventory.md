# SaaS Enterprise Refactor — Iteración 00: Inventario y mapa de verdad

**Branch:** `refactor/saas-enterprise-00-inventory`  
**Fecha:** 2026-05-20  
**Alcance:** Solo `docs/` (iteración 00). Sin cambios en `.cs` de runtime.  
**Baseline git:** `main` @ `711c405` (post Fase A + Fase B + bump CI artifacts).

**Contrato:** Modo Ultra Estricto del prompt de refactor (adoptado tal cual; no rediseñado). Una iteración → una rama → un entregable acotado → fail-closed por defecto.

---

## Resumen ejecutivo (estado actual en `main`)

El core SaaS opera con **una fuente de verdad relacional** para entitlements comerciales:

| Capa | Autoridad efectiva |
|------|-------------------|
| Módulos / features de plan | `ITenantEntitlementsService` → `TenantEntitlementsService` (suscripción activa + `SaasPlanFeature` + overrides) |
| JWT / sesión / DTOs | `ISessionModulesResolver` → entitlements (fail-closed sin suscripción) |
| HTTP `perm:*` | `PermissionHandler` → `ITenantEntitlementsService.AllowsPermissionAsync` |
| MediatR `[RequireFeature]` | `SubscriptionGateBehavior` → `ISubscriptionService` / entitlements |
| Overrides SuperAdmin | `ITenantSubscriptionOverridesService` → `tenant_subscription_feature_overrides` |
| Sync plan | `Tenant.PlanCode` → `ErpDbContext.SyncTenantSubscriptionsFromPlanCodeAsync` (no destructivo + eventos) |
| Consultas sin filtro tenant | `IPlatformQueryAccessor.Unfiltered` (único `.IgnoreQueryFilters()` en `PlatformQueryAccessor`) |

**Eliminado en Fase B-10:** `Tenant.EnabledModulesJson`, columna `enabled_modules`, `GetEffectiveEnabledModules`, `PreferLegacyEnabledModulesJsonForSession`.

**Drift residual (no bloquea runtime fail-closed):** vocabulario `AllModuleKeys` (español) vs `CanonicalModuleKeys` (inglés); `navConfig.ts` con `moduleKey: 'ventas'`; claim JWT `enabledModules`; sin API de lectura de `tenant_saas_subscription_events`.

---

## Tabla: quién decide qué (post Fase A+B)

| Decisión | Fuente actual | Consumidores principales |
|----------|---------------|-------------------------|
| Módulos habilitados (JWT/UI) | `ITenantEntitlementsService` vía `ISessionModulesResolver` | Login, RefreshToken, Register, SwitchTenant, GetMyPermissions, SuperAdmin, `TenantDto` |
| Permiso HTTP por plan | `ITenantEntitlementsService.AllowsPermissionAsync` | `PermissionHandler` |
| Feature MediatR | `ISubscriptionService.HasFeatureAsync` (delega entitlements) | `SubscriptionGateBehavior`, ~60 commands `[RequireFeature]` |
| Límites medidos | `TenantSubscriptionUsage` + UPSERT atómico | `SubscriptionGateBehavior` |
| Plan comercial | `Tenant.PlanCode` + `TenantSaasSubscription` (sync + eventos) | Handlers tenant, menú, analytics |
| Menú navegación | `TenantCustomMenu` / `SaasPlan.MenuConfigJson` / `navConfig` fallback | `TenantMenuService`, `AppLayout` |
| RBAC perfil | `AccessProfilePermission` | `PermissionHandler`, `GetMyPermissionsHandler` |

---

## Modelo de datos

| Concepto | Entidad / artefacto |
|----------|---------------------|
| Plan comercial | `SaasPlan` |
| Features del plan | `SaasPlanFeature` + `SaasFeatureDefinition` |
| Suscripción activa | `TenantSaasSubscription` |
| Overrides | `TenantSubscriptionFeatureOverride` |
| Usage | `TenantSubscriptionUsage` |
| Auditoría plan/overrides | `TenantSaasSubscriptionEvent` |
| Catálogo validación entrada | `TenantSubscriptionCatalog` (estático; `CanonicalModuleKeys`, mapping permisos) |

---

## Referencias por símbolo (runtime — `main`)

### `ITenantEntitlementsService` / `ISessionModulesResolver`

| Archivo | Uso |
|---------|-----|
| `backend/src/ERP.Application/Subscriptions/ITenantEntitlementsService.cs` | Contrato SoT |
| `backend/src/ERP.Infrastructure/Services/TenantEntitlementsService.cs` | Implementación |
| `backend/src/ERP.Application/Subscriptions/SessionModulesResolver.cs` | Módulos sesión |
| `backend/src/ERP.API/Authorization/PermissionHandler.cs` | Gating HTTP |
| Handlers auth + `GetMyPermissionsHandler` | DTO / JWT `enabledModules` |

### `TenantSubscriptionCatalog`

| Archivo | Uso |
|---------|-----|
| `backend/src/ERP.Application/Common/TenantSubscriptionCatalog.cs` | `CanonicalModuleKeys`, `ResolveEnabledModulesAsync`, `TenantAllowsPermissionAsync`, `ValidateModuleKeysOrThrow`, `AllModuleKeys` (solo SuperAdmin sin tenant) |
| `backend/src/ERP.Application.Tests/TenantSubscriptionCatalogTests.cs` | Contrato fail-closed + mapping |

### `IPlatformQueryAccessor`

| Archivo | Uso |
|---------|-----|
| `backend/src/ERP.Infrastructure/Persistence/PlatformQueryAccessor.cs` | Único `.IgnoreQueryFilters()` |
| `ErpDbContext`, seeders, `TenantEntitlementsService`, repos vía `.Unfiltered(...)` | Cross-tenant controlado |
| `backend/src/ERP.Infrastructure.Tests/Persistence/IgnoreQueryFiltersAuditTests.cs` | Allowlist única |

### `Tenant.PlanCode` + sync + eventos

| Archivo | Uso |
|---------|-----|
| `backend/src/ERP.Domain/Modules/Tenants/Entities/Tenant.cs` | `PlanCode`, `SetPlanCode` |
| `backend/src/ERP.Infrastructure/Persistence/ErpDbContext.cs` | Sync + `TenantSaasSubscriptionEvents` |
| `backend/src/ERP.Infrastructure/Services/TenantSubscriptionOverridesService.cs` | Overrides + eventos módulo |

### Frontend (fail-closed UI — Fase B-07)

| Archivo | Comportamiento |
|---------|----------------|
| `frontend/src/components/AppLayout.tsx` | `mods.length === 0` → **oculta** módulos (`return false`) |
| `frontend/src/pages/SuperAdminPanelPage.tsx` | `hasModuleRestrictions` desde API |
| `frontend/src/nav/navConfig.ts` | Fallback con `moduleKey` español en algunos ítems |

---

## Riesgos / drift abiertos (post refactor)

| # | Tema | Estado | Iteración objetivo |
|---|------|--------|-------------------|
| R1 | `AllModuleKeys` (ES) vs `CanonicalModuleKeys` (EN) en SuperAdmin global | Abierto | Post-B / doc |
| R2 | `navConfig.ts` `moduleKey: 'ventas'` vs API `sales` | Abierto | Post-B |
| R3 | Sin API/UI para `tenant_saas_subscription_events` | Abierto | Post-B |
| R4 | Claim JWT `enabledModules` (tamaño token) | Abierto | Post-B opcional |
| R5 | `Tenant.PlanCode` denormalizado | Abierto | Post-B opcional |
| R6 | ADRs/inventario histórico desactualizado | Abierto | Doc |
| R7 | `ERP.Application.Tests` no compila (tipos ajenos al refactor) | Abierto | Deuda repo |

---

## Orden de ejecución (contrato — no ampliar scope)

| Iteración | Rama | ID | Entregable | Estado en `main` |
|-----------|------|-----|------------|------------------|
| **00** | `refactor/saas-enterprise-00-inventory` | Inventario | Este documento | **En curso (re-baseline)** |
| 01 | `refactor/saas-enterprise-01-entitlements-service` | A1 | `ITenantEntitlementsService` | Implementado |
| 02 | `refactor/saas-enterprise-02-safe-defaults` | A2 | Fail-closed módulos | Implementado |
| 03 | `refactor/saas-enterprise-03-unified-gates` | A3 | Gates HTTP + MediatR | Implementado |
| 04 | `refactor/saas-enterprise-04-usage-upsert` | A4 | Usage UPSERT | Implementado |
| 05 | `refactor/saas-enterprise-05-platform-query` | A5 | `IPlatformQueryAccessor` + audit | Implementado |
| 06 | `refactor/saas-enterprise-06-phase-a-closeout` | Cierre A | Docs + flags | Implementado |
| 07 | `refactor/saas-enterprise-07-fail-closed-ui` | B7 | Frontend fail-closed | Implementado |
| 08 | `refactor/saas-enterprise-08-stop-legacy-json` | B8 | Overrides relacionales | Implementado |
| 09 | `refactor/saas-enterprise-09-platform-query-migration` | B9 | Migración IQF | Implementado |
| 10 | `refactor/saas-enterprise-10-legacy-cleanup` | B10 | Drop JSON + eventos | Implementado |

**Nota operativa:** `main` ya contiene 01–10. A partir de 01, cada iteración en Modo Ultra Estricto debe ser **verificación + gap mínimo** (sin reimplementar ni ampliar scope), salvo reset explícito de `main`.

---

## Open Questions

Registradas aquí; no bloquean continuar con fail-closed.

1. **¿Re-ejecutar 01–10 como ramas nuevas o solo auditoría?** `main` ya mergeó el refactor completo. Default: auditoría por iteración sin duplicar código.
2. **¿`moduleKey` oficial en UI/menú: inglés canónico (`sales`) o español alias (`ventas`)?** Hoy API entrega inglés; `navConfig` mezcla español.
3. **¿Deprecar `Tenant.PlanCode` y leer plan solo desde `TenantSaasSubscription`?** Hoy columna + sync en `SaveChanges`.
4. **¿Eliminar claim `enabledModules` del JWT en etapa separada?** Sigue poblado desde entitlements.
5. **¿Exponer `tenant_saas_subscription_events` en SuperAdmin?** Tabla y escritura existen; sin endpoint.
6. **¿Unificar `AllModuleKeys` → `CanonicalModuleKeys` para sesión SuperAdmin global?** Hoy `AllModuleKeys` solo en login/switch SuperAdmin sin tenant.
7. **¿Actualizar ADR-0001…0006 e inventario pre-refactor como histórico o reescribir?** Evitar confusión con estado actual.

---

## Verificación iteración 00

- [x] Solo archivos bajo `docs/refactor/` modificados en esta iteración.
- [x] Sin cambios en `.cs` / `.tsx` de aplicación.
- [x] Open Questions registradas arriba.
- [ ] Rama `refactor/saas-enterprise-00-inventory` commit + listo para iteración 01.

Referencias de cierre: [phase-a-closeout](./saas-enterprise-phase-a-closeout.md), [phase-b-closeout](./saas-enterprise-phase-b-closeout.md).
