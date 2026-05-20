# SaaS Enterprise Refactor — Iteración 00: Inventario y mapa de verdad

**Branch:** `refactor/saas-enterprise-00-inventory`  
**Fecha:** 2026-05-20  
**Alcance:** Solo documentación. Sin cambios de lógica ni comportamiento en runtime.

---

## Resumen ejecutivo

El core SaaS del backend mantiene **tres fuentes de verdad** para entitlements (módulos/features), más un **fallback fail-open** (`AllModuleKeys` / lista vacía = todo permitido) y un **desalineamiento de vocabulario** (español vs inglés) entre catálogo legacy, permisos API y menú. El modelo relacional nuevo (`TenantSaasSubscription` + `SaasPlanFeature` + overrides) existe y `SubscriptionService` lo consulta, pero **sigue delegando módulos** a `Tenant.EnabledModulesJson` vía `TenantSubscriptionCatalog`.

---

## Tabla: quién decide qué

| Decisión | Fuente actual (autoridad efectiva) | Consumidores | Modelo objetivo (Fase A) |
|----------|-----------------------------------|--------------|---------------------------|
| Módulos habilitados (lista para JWT/UI) | `Tenant.EnabledModulesJson` → `TenantSubscriptionCatalog.GetEffectiveEnabledModules` (null/invalid → **AllModuleKeys**) | Login, RefreshToken, Register, SwitchTenant, GetMyPermissions, SuperAdmin APIs, TenantDto | `ITenantEntitlementsService` ← subscription + plan features (`Kind=Module`) |
| Permiso HTTP `[Authorize(Policy = "perm:…")]` — capa plan | `TenantSubscriptionCatalog.TenantAllowsPermission` (prefijo módulo en catálogo **español**; prefijo desconocido → **allow**) | `PermissionHandler` | Mismo servicio + mapping permission↔feature/module unificado |
| Comando MediatR — feature comercial | `ISubscriptionService.HasFeatureAsync` (plan DB + overrides; fallback `EnabledModulesJson` + `TryMapFeatureToModule` hardcoded) | `SubscriptionGateBehavior` + ~60 commands con `[RequireFeature]` | `ITenantEntitlementsService` (sin fallback legacy) |
| Límites medidos (clientes, documentos, …) | `TenantSubscriptionUsage` + `CheckLimitAsync`; incremento con `SaveChangesAsync` **dentro** del servicio | `SubscriptionGateBehavior` post-handler | UPSERT atómico; UoW solo en handler |
| Plan comercial del tenant | `Tenant.PlanCode` (columna) + sync destructivo en `ErpDbContext.SaveChangesAsync` → `TenantSaasSubscription` | Handlers de tenant, menú por plan, `SubscriptionService.ResolveEffectivePlan…` | Subscription como SoT; `PlanCode` compat/derivado |
| Menú navegación | `TenantCustomMenu` → `SaasPlan.MenuConfigJson` (por `PlanCode`) → menú global; `moduleKey` en JSON (**inglés** en bootstrap) | `TenantMenuService`, frontend `AppLayout` | Entitlements + nav ligado a `SaasFeatureDefinition` |
| Config operativa KV | `ConfigGlobal` / `ConfigModule` / `ConfigFeature` por tenant | `ConfigService` | Sin cambio en Fase A (no es entitlement comercial) |
| Permisos de perfil (RBAC) | `AccessProfilePermission` | `PermissionHandler`, `GetMyPermissionsHandler` | Sin cambio; capa separada de entitlements |

**Nota:** No existe entidad `Subscriber`; `Tenant` es pagador + operativo.

---

## Modelo de datos (nuevo vs legacy)

| Concepto roadmap | Entidad / artefacto real |
|------------------|-------------------------|
| CommercialPlan | `SaasPlan` |
| PlanFeature | `SaasPlanFeature` + `SaasFeatureDefinition` |
| Subscription activa | `TenantSaasSubscription` (`TenantSubscriptionStatus.Active`) |
| Overrides | `TenantSubscriptionFeatureOverride` |
| Usage | `TenantSubscriptionUsage` (periodo `yyyy-MM`) |
| Legacy módulos | `Tenant.EnabledModulesJson` |
| Legacy catálogo estático | `TenantSubscriptionCatalog` (clase estática, **no** tabla) |
| Plan KV por plan | **No existe** — límites en `SaasPlanFeature.LimitPerPeriod` |

---

## Referencias por símbolo (archivos con ruta exacta)

### `Tenant.EnabledModulesJson`

| Archivo | Uso |
|---------|-----|
| `backend/src/ERP.Domain/Modules/Tenants/Entities/Tenant.cs` | Propiedad, `SetSubscription` / serialización JSON |
| `backend/src/ERP.Infrastructure/Persistence/Configurations/TenantConfiguration.cs` | Columna `enabled_modules` |
| `backend/src/ERP.Application/Common/TenantSubscriptionCatalog.cs` | **Autoridad efectiva** de módulos vía `GetEffectiveEnabledModules` |
| `backend/src/ERP.Application/Modules/Tenants/DTOs/TenantDto.cs` | Proyección + flag restricciones |
| `backend/src/ERP.Application/Modules/Access/UseCases/SuperAdminTenants/SuperAdminTenantHandlers.cs` | CRUD tenant (lectura flag) |
| `backend/src/ERP.API/Controllers/SuperAdminController.cs` | API listado `hasModuleRestrictions` |
| `backend/src/ERP.Application.Tests/TenantSubscriptionCatalogTests.cs` | Tests fallback AllModuleKeys |
| `backend/src/ERP.Domain.Tests/TenantSubscriptionTests.cs` | Tests dominio `SetSubscription` |
| `backend/src/ERP.Infrastructure/Migrations/*` | Solo schema (múltiples Designer + Snapshot) |

### `Tenant.PlanCode`

| Archivo | Uso |
|---------|-----|
| `backend/src/ERP.Domain/Modules/Tenants/Entities/Tenant.cs` | Propiedad comercial |
| `backend/src/ERP.Infrastructure/Persistence/ErpDbContext.cs` | `SyncTenantSubscriptionsFromPlanCodeAsync` en cada `SaveChanges` |
| `backend/src/ERP.Infrastructure/Persistence/TenantMenuService.cs` | Resolución menú por plan |
| `backend/src/ERP.Infrastructure/Services/SubscriptionService.cs` | Fallback plan si no hay `TenantSaasSubscription` activa |
| `backend/src/ERP.Infrastructure/Persistence/GrowthAnalyticsReader.cs` | Analytics |
| Handlers auth: `LoginHandler`, `RefreshTokenHandler`, `RegisterHandler`, `SwitchTenantHandler` (Application + Access), `ClaimInitialSuperAdminHandler`, `SuperAdminLoginHandler` | Claims / DTO sesión |
| `backend/src/ERP.Application/Modules/Access/UseCases/Permissions/GetMyPermissionsHandler.cs` | DTO `planCode` |
| `backend/src/ERP.Application/Modules/Tenants/UseCases/*Subscription*`, `CreateTenant*`, `SuperAdminTenantHandlers` | Escritura |
| `backend/src/ERP.API/Controllers/TenantsController.cs`, `SuperAdminController.cs` | API |

### `TenantSubscriptionCatalog` (clase estática — “catálogo legacy”)

| Archivo | Uso |
|---------|-----|
| `backend/src/ERP.Application/Common/TenantSubscriptionCatalog.cs` | **Definición:** `AllModuleKeys`, `GetEffectiveEnabledModules`, `TryGetModuleKeyForPermission`, `TenantAllowsPermission`, `ValidateModuleKeysOrThrow` |
| `backend/src/ERP.API/Authorization/PermissionHandler.cs` | Gating HTTP por plan |
| `backend/src/ERP.Infrastructure/Services/SubscriptionService.cs` | Fallback módulos en `IsFeatureAllowedByTenantModuleAsync` |
| `backend/src/ERP.Application/Modules/Access/UseCases/Permissions/GetMyPermissionsHandler.cs` | Lista módulos + filtro permisos |
| Todos los handlers auth y SuperAdmin listados arriba | JWT `enabledModules` |
| `backend/src/ERP.Application.Tests/TenantSubscriptionCatalogTests.cs` | Contrato actual (incl. fail-open) |
| `backend/src/ERP.Application.Tests/UpdateTenantSubscriptionHandlerTests.cs` | Expectativa AllModuleKeys al limpiar módulos |

### `PermissionHandler` / `TenantAllowsPermission`

| Archivo | Uso |
|---------|-----|
| `backend/src/ERP.API/Authorization/PermissionHandler.cs` | `TenantSubscriptionCatalog.TenantAllowsPermission` antes de RBAC perfil |
| `backend/src/ERP.API/Program.cs` | Registro `IAuthorizationHandler` |
| `backend/src/ERP.Application/Common/Permissions.cs` | Documentación claves (`sales.*`, `inventory.*` — **inglés**) |

### `SubscriptionGateBehavior`

| Archivo | Uso |
|---------|-----|
| `backend/src/ERP.Application/Behaviors/SubscriptionGateBehavior.cs` | Pipeline MediatR: `HasFeatureAsync`, `CheckLimitAsync`, `IncrementUsageAsync` |
| `backend/src/ERP.Application/DependencyInjection.cs` | Registro behavior |
| `backend/src/ERP.Application/Common/SubscriptionAttributes.cs` | `[RequireFeature]`, `[ConsumeSubscriptionUnits]` |
| `backend/src/ERP.Application/Common/SubscriptionFeatureCodes.cs` | Códigos feature (`SALES`, `INVENTORY`, …) |
| ~60 `*Command.cs` / `*Query.cs` en Sales, Inventory, Purchasing, Expenses, Accounting, Branches | Atributos en requests |

### `TryMapFeatureToModule` / `TryGetModuleKeyForPermission`

| Archivo | Símbolo |
|---------|---------|
| `backend/src/ERP.Application/Common/TenantSubscriptionCatalog.cs` | `TryGetModuleKeyForPermission` — prefijos **español** (`ventas`, `inventario`, …) |
| `backend/src/ERP.Infrastructure/Services/SubscriptionService.cs` | `TryMapFeatureToModule` — switch hardcoded feature code → módulo español |
| `backend/src/ERP.Application.Tests/TenantSubscriptionCatalogTests.cs` | Tests mapping español |

### `HasFeatureAsync` / `SubscriptionService` / `ConsumeSubscriptionUnits`

| Archivo | Uso |
|---------|-----|
| `backend/src/ERP.Domain/Subscriptions/Interfaces/ISubscriptionService.cs` | Contrato |
| `backend/src/ERP.Infrastructure/Services/SubscriptionService.cs` | Implementación + **`SaveChangesAsync` en `IncrementUsageAsync`** |
| `backend/src/ERP.Infrastructure/DependencyInjection.cs` | DI |
| `backend/src/ERP.API.Tests/Support/AllowAllSubscriptionService.cs` | Stub integración (siempre true) |
| `backend/src/ERP.API.Tests/Support/IntegrationTestWebAppFactory.cs` | Reemplaza servicio en tests API |
| Commands con `[ConsumeSubscriptionUnits]`: `CreateCustomerCommand.cs`, `CreateJournalEntryCommand.cs` | Consumo medido |

### `GetEffectiveEnabledModules` / `AllModuleKeys`

| Archivo | Comportamiento |
|---------|----------------|
| `backend/src/ERP.Application/Common/TenantSubscriptionCatalog.cs` | null / vacío / JSON inválido / solo keys desconocidas → **AllModuleKeys** |
| Auth handlers + `GetMyPermissionsHandler` | Propagan lista completa o derivada |
| `backend/src/ERP.Application.Tests/TenantSubscriptionCatalogTests.cs` | Codifica fail-open como contrato |
| `frontend/src/components/AppLayout.tsx` | `mods.length === 0` → **muestra todo** (fail-open UI) |

### `IgnoreQueryFilters()` (47 llamadas en 11 archivos .cs de producción)

| Archivo | # | Contexto |
|---------|---|----------|
| `backend/src/ERP.Infrastructure/Persistence/Repositories/UserRepository.cs` | 13 | Queries legacy cross-tenant con filtro manual |
| `backend/src/ERP.Infrastructure/Services/ConfigService.cs` | 13 | Config por tenant con filtro explícito |
| `backend/src/ERP.Infrastructure/Persistence/Repositories/AccessRepository.cs` | 4 | Memberships / acceso |
| `backend/src/ERP.API/Extensions/DevDatabaseSeeder.cs` | 4 | Solo dev seed |
| `backend/src/ERP.Infrastructure/Persistence/Repositories/CarrierRepository.cs` | 3 | Carriers |
| `backend/src/ERP.Infrastructure/Persistence/Repositories/ProductCatalogRepository.cs` | 3 | Catálogo productos |
| `backend/src/ERP.Infrastructure/Seeding/TenantOnboardingService.cs` | 3 | Onboarding |
| `backend/src/ERP.Infrastructure/Persistence/ErpDbContext.cs` | 1 | Sync `TenantSaasSubscription` al cambiar `PlanCode` |
| `backend/src/ERP.API/Hangfire/SriRetryJob.cs` | 1 | Job cross-tenant (comentado) |
| `backend/src/ERP.Infrastructure/Seeding/DefaultProfileSeeder.cs` | 1 | Seed perfiles |
| `backend/src/ERP.Infrastructure/BackgroundServices/KardexReporteProcessor.cs` | 1 | Background |
| `backend/src/ERP.Infrastructure/Persistence/GrowthAnalyticsReader.cs` | 1 | Analytics |
| `backend/src/ERP.Infrastructure/Deployment/FirstRunSetupService.cs` | 2 | Setup inicial |

**Riesgo R6:** Ningún wrapper central; motivo no obligatorio; sin logging unificado.

### Modelo subscription (consulta parcial — no legacy)

| Archivo | Rol |
|---------|-----|
| `backend/src/ERP.Domain/Subscriptions/Entities/*.cs` | Entidades |
| `backend/src/ERP.Infrastructure/Persistence/Configurations/SubscriptionsConfiguration.cs` | EF mapping |
| `backend/src/ERP.Infrastructure/Persistence/SaasPlansBootstrap.cs` | Seed planes/menú |
| `backend/src/ERP.Infrastructure/Persistence/SaasPlansAdminService.cs` | Admin catálogo |
| `backend/src/ERP.API/Controllers/SaasPlansAdminController.cs` | API admin |

### Frontend (drift con backend — fuera de branch backend, referencia)

| Archivo | Notas |
|---------|-------|
| `frontend/src/components/AppLayout.tsx` | Filtra menú por `enabledModules`; vacío = allow all |
| `frontend/src/nav/navConfig.ts` | `moduleKey` español (`ventas`, `inventario`) |
| `backend/.../SaasPlansBootstrap.cs` | Menú plan con `moduleKey` **inglés** (`sales`, `inventory`) |
| `frontend/src/store/permissionsStore.ts` | Snapshot permisos + módulos |

---

## Drift crítico detectado

### 1. Tres fuentes de verdad (A1)

1. **`TenantSubscriptionCatalog` + `EnabledModulesJson`** — UI, JWT, `PermissionHandler`, fallback de `SubscriptionService`.
2. **`TenantSaasSubscription` + `SaasPlanFeature`** — `SubscriptionService.HasFeatureAsync` (primario si hay filas).
3. **`Tenant.PlanCode`** — sync en `SaveChanges`, menú, fallback plan sin fila subscription.

### 2. Vocabulario módulos: español vs inglés (bloqueante para A3)

| Capa | Ejemplo clave |
|------|----------------|
| `TenantSubscriptionCatalog.AllModuleKeys` | `ventas`, `inventario`, `compras`, `accounting` |
| Permisos API reales (`perm:`) | `sales.invoices.view`, `inventory.products.view` |
| Menú bootstrap plan | `moduleKey: "sales"`, `"inventory"` |
| `navConfig.ts` (fallback FE) | `moduleKey: 'ventas'` con `permissionKey: 'inventory.products.view'` |

**Efecto:** `TryGetModuleKeyForPermission("sales.invoices.view")` → `false` → `TenantAllowsPermission` → **true** (permiso no restringido por plan en API). El gating HTTP por suscripción está **efectivamente desactivado** para permisos en inglés.

### 3. Fail-open simétrico backend + frontend (A2)

- Backend: `EnabledModulesJson` null → `AllModuleKeys` (9 módulos).
- Frontend: `enabledModules.length === 0` → `moduleEntitled` retorna `true`.
- Tests documentan el comportamiento como correcto (`TenantSubscriptionCatalogTests`).

### 4. `IncrementUsageAsync` + doble persistencia (A4)

`SubscriptionGateBehavior` ejecuta handler (que suele llamar `UnitOfWork.SaveChangesAsync`) y después `IncrementUsageAsync` → **`SaveChangesAsync` adicional** en el mismo `DbContext` → riesgo de transacciones anidadas, condiciones de carrera en usage (read-modify-write sin UPSERT).

### 5. Sync destructivo de subscription (B1 — fuera Fase A pero riesgo)

`ErpDbContext.SyncTenantSubscriptionsFromPlanCodeAsync`: al cambiar `PlanCode`, **Remove** subscription existente y recrea; no historial de eventos.

---

## Top 5 riesgos

| # | Riesgo | Impacto | Mitigación (roadmap) |
|---|--------|---------|----------------------|
| 1 | Fail-open null/invalid → todos los módulos | Tenant sin restricción ve todo el producto | A2: fail-closed + entitlements service |
| 2 | Permission gating no aplica a claves inglés | Bypass de plan en capa HTTP | A3: mapping unificado desde `SaasFeatureDefinition` / catálogo central |
| 3 | UI (módulos ES) vs menú plan (módulos EN) vs API (permisos EN) | Menú incoherente, falsa sensación de seguridad | A1+A3: una proyección; normalizar `moduleKey` |
| 4 | Usage no atómico + SaveChanges en servicio | Límites incorrectos bajo concurrencia; UoW inconsistente | A4 |
| 5 | `IgnoreQueryFilters` disperso (47×) | Fugas cross-tenant si filtro manual falla | A5 wrapper + audit test |

---

## Orden recomendado de ejecución (Fase A)

| Iteración | ID | Dependencias | Entregable |
|-----------|-----|--------------|------------|
| 00 | Inventario | — | Este documento + ADR contexto |
| 01 | A1 | 00 | `ITenantEntitlementsService` (SoT subscription model) |
| 02 | A2 | 01 | Eliminar AllModuleKeys fallback; fail-closed |
| 03 | A3 | 01, 02 | Unificar `PermissionHandler` + `SubscriptionGateBehavior` + mapping permisos |
| 04 | A4 | 01 | Usage UPSERT; quitar SaveChanges del servicio |
| 05 | A5 | — | `PlatformQueryService` + test anti-patrón |
| 06 | Cierre | 01–05 | Legacy marcado, docs rollback, flags |

**Open Questions (no bloquean 01 con enfoque fail-closed)**

1. ¿Normalización oficial de `moduleKey`: inglés (menú bootstrap / rutas) o español (catálogo legacy / SuperAdmin UI)?
2. ¿`Tenant.PlanCode` se mantiene como denormalización durante Fase A o solo lectura desde subscription?
3. ¿`EnabledModulesJson` se depreca en API pública en 2 etapas (JWT claim) o se mantiene como caché derivada en la misma iteración 06?
4. ¿Feature flags por entorno (`appsettings`) para activar `ITenantEntitlementsService` en lectura dual antes de cortar legacy?

---

## Verificación iteración 00

- Solo archivos bajo `docs/` modificados.
- Build y tests sin cambios de código de aplicación.
- `git diff` sin alteración de `.cs` de runtime.
