# Arquitectura

Monolito modular: **Clean Architecture + CQRS (MediatR)**.

Documentos relacionados: [IDENTITY.md](./IDENTITY.md), [DATABASE.md](./DATABASE.md), [STATUS.md](../STATUS.md), [ROADMAP.md](./ROADMAP.md).

---

## Capas

| Capa | Proyecto | Responsabilidad |
|------|----------|-----------------|
| Domain | `ERP.Domain` | Entidades, enums, excepciones, interfaces |
| Application | `ERP.Application` | Casos de uso, behaviors, DTOs, ports |
| Infrastructure | `ERP.Infrastructure` | EF Core, Redis, guards, billing, limits |
| API | `ERP.API` | HTTP, JWT, middleware, policies |

Dependencias: API → Application → Domain; Infrastructure implementa ports.

---

## Jerarquía multiempresa

```
Tenant (contenedor multiempresa: nombre, slug, idioma)
  └── Company (entidad fiscal / operativa)
        └── CompanyUserMembership
              └── Módulos ERP (Ventas, Inventario, Contabilidad, …)
```

| Actor | Clave scope | Opera ERP |
|-------|-------------|-----------|
| Tenant | `tenant_id` | vía companies |
| Company | `company_id` | sí (header `X-Company-Id`, validado contra membership) |

**Regla:** Tenant agrupa companies (entidad `Tenant`, tabla `tenants` — sin plan/billing/límites, ver [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md)). Company opera el ERP.

> **Nota de naming:** la consolidación `SubscriberId → TenantId` (FASE 4, ver [`STATUS.md`](../STATUS.md)) está completa en código, verificado directamente contra el modelo EF real (`ErpDbContextModelSnapshot.cs`) y las entidades de `ERP.Domain`/`ERP.Application` (2026-07-23). `Tenant`/`tenant_id`/`ICurrentTenant`/`ITenantRepository` son los nombres **vigentes**. Los marcadores de scope vigentes son `ITenantScopedEntity` (Domain) e `ITenantScopedRequest`/`ITenantOnlyRequest` (Application — `ITenantOnlyRequest` es alias legacy, preferir `ITenantScopedRequest`). `ISubscriberScopedEntity`/`ISubscriberScopedRequest` **no existen en el código** — no citarlos como nombres sellados. Toda referencia histórica a `Subscriber` corresponde exclusivamente al Control Plane SaaS eliminado — ver [`docs/archive/SUBSCRIBER-SCOPE-SEALED.md`](./archive/SUBSCRIBER-SCOPE-SEALED.md).

---

## Capas IAM vs ERP runtime

| Capa | Clave | Responsabilidad | API (canónica) |
|------|-------|-----------------|----------------|
| **IAM** | `identity_user_id` | Auth, sesión, perfiles, permisos | `/api/auth/*`, `/api/admin/iam/*` |
| **ERP Runtime** | `company_id` | Ventas, inventario, compras, SRI, contabilidad | `/api/sales/*`, `/api/inventory/*`, … |

Separación:

- IAM **no** ejecuta lógica ERP operativa.
- ERP Runtime **siempre** filtra por `company_id`.

### Ownership matrix

| Concepto | Capa | Clave |
|--------|------|-------|
| Tenant (contenedor multiempresa) | Tenants | `tenant_id` |
| Empresa fiscal | Company / ERP | `company_id` |
| Usuario login | IAM | `identity_users.id` |
| Membership | IAM | `(company_id, identity_user_id)` |

---

## Scopes

Toda entidad nueva declara **un** scope primario.

### Tenant (`tenant_id`)

Tablas: `tenants`, `tenant_custom_menus`, `config_feature`, `config_global`, `config_module`.

JWT: `tenant_id` identifica el tenant del usuario autenticado.

> Tablas `commercial_plans*`, `subscriber_subscriptions`, `saas_billing_*`, `subscriber_custom_menus` **no existen** en el esquema actual — eliminadas en FASE 1 ([`STATUS.md`](../STATUS.md)).

### ERP operativo (`company_id`)

Master data, inventario, compras, configuración, fiscal/SRI — todas filtran por `company_id` vía `CompanyScopeBehavior` + `ICompanyOperationalEntity` / EF global filters. Migración Wave 1 (inventario core con `company_id`) **completada y congelada** — ver [`STATUS.md`](../STATUS.md) (ERP CORE BASELINE v1.0, frozen 2026-06-05). RLS a nivel PostgreSQL no está implementado — ver [DATABASE.md#rls](./DATABASE.md#rls).

JWT: `company_id` obligatorio para operaciones ERP (`CompanyScopeBehavior`).

### IAM

`company_user_memberships` solo `company_id` + `identity_user_id`. Permisos por `(companyId, userId)`.

---

## Multiempresa — aislamiento

| Capa | Mecanismo |
|------|-----------|
| JWT | `tenant_id`, `company_id` |
| MediatR | `BillingGateBehavior`, `SubscriptionGateBehavior`, `CompanyScopeBehavior` |
| Application | `ICompanyAccessGuard`, `ICurrentTenant`, `ICurrentCompany` |
| EF Core | Filtro global `ITenantScopedEntity` (scope = tenant compartido) |
| PostgreSQL | Sin RLS — no implementado, ver [DATABASE.md](./DATABASE.md#rls) |

### Cambio de contexto

1. Login → `tenant_id`
2. Una company → auto `company_id`
3. Varias → `/select-company` → `POST /api/auth/switch-company`
4. Handlers leen `ICurrentCompany` — **nunca** `company_id` del body como autoridad

### Background jobs

Antes de BD: `JobTenantContext`, `JobCompanyContext` para interceptor PostgreSQL.

**Terminología retirada:** `Subscriber` (entidad), `subscriber_id` (claim/columna), `ISubscriberScopedEntity`, `ISubscriberScopedRequest` (interfaces — nunca existieron en el código, no fueron solo renombradas) — consolidados en `Tenant`/`tenant_id`/`ITenantScopedEntity`/`ITenantScopedRequest` (FASE 4).

---

## Bounded contexts

> **FASE 1 — ERP Kernel Cleanup (2026-06-05)** eliminó por completo las capas SaaS (Billing domain, Subscriptions domain, Platform entities, Commercial plans, Entitlements y sus controllers/middleware/jobs/services/behaviors — ver [`STATUS.md`](../STATUS.md)). El backend actual es **ERP Core puro**: no existen módulos `Billing`, `Subscriptions` ni `Platform` con lógica activa.

| Contexto | Carpeta / namespace | Scope |
|----------|---------------------|-------|
| Identity / Access | `Modules/Auth`, `Modules/Access`, `Modules/Security` | `tenant_id` / `(company_id, identity_user_id)` |
| Tenant | `Modules/Tenants` | `tenant_id` |
| Company | `Modules/Company` | `company_id` |
| Master Data (Business Partners, Products, Items) | `MasterData`, `Modules/Products`, `Modules/Items` | `company_id` |
| Inventory / Purchases | `Modules/Inventory`, `Modules/Purchases` | `company_id` |
| Configuration / SRI | `Modules/Configuration`, `Modules/SriCatalogs`, `Modules/Fiscal` | `company_id` |
| Menu / Navigation | `Modules/Menu`, `Modules/Navigation` | `tenant_id` / `company_id` |
| Audit / Auxiliary | `Modules/Audit`, `Modules/Auxiliary` | `company_id` |
| SharedKernel / Common | `Modules/SharedKernel`, `Modules/Common` | base entities, contratos compartidos |

> Carpetas `Modules/Subscribers/*` permanecen como directorios vacíos (residuo de FASE 1, sin tipos `.cs`); no representan un contexto activo.

---

## Pipeline MediatR

1. `ValidationBehavior`
2. `CompanyScopeBehavior`
3. `CachingBehavior`

Registro: [`ERP.Application/DependencyInjection.cs`](../backend/src/ERP.Application/DependencyInjection.cs).

---

## Servicios core

| Concern | Interface | Ubicación |
|---------|-----------|-----------|
| Contexto tenant | `ICurrentTenant` | `ERP.Application/Modules/Common/` |
| Contexto company | `ICurrentCompany` | `ERP.Application/Modules/Common/` |
| Acceso a company | `ICompanyAccessGuard` | `ERP.Application/Modules/Companies/` |

---

## CQRS

- Commands/queries en `ERP.Application/Modules/{Module}/UseCases/`
- Marcadores de scope (`ERP.Application/Common/RequestScopeMarkers.cs`): `ICompanyScopedRequest` / `IRequiresCompanyContext` (cubiertos por `CompanyScopeBehavior`), `ITenantScopedRequest` / `ITenantOnlyRequest` (scope tenant, sin company activa — `ITenantOnlyRequest` es alias legacy, preferir `ITenantScopedRequest`), `IPlatformScopedRequest` (operación de plataforma).
- Nombres `ISubscriberScopedRequest`/`ISubscriberOnlyRequest` citados en revisiones anteriores de este documento no existen en el código — corregido tras verificación directa contra `RequestScopeMarkers.cs` (2026-07-23). Ver nota en [Jerarquía multiempresa](#jerarquía-multiempresa).

---

## API (rutas reales)

| Área | Base | Controller |
|------|------|------------|
| Auth | `/api/auth` | `AuthController` |
| Sesión actual | `/api/me` | `MeController` |
| Setup / first-run | `/api/setup` | `SetupController` |
| Companies | `/api/companies` | `CompaniesController` |
| Branches / settings | `/api/settings/branches`, `/api/settings/geography` | `BranchesController`, `GeographyController` |
| Master data — Business Partners | `/api/master/business-partners*` | `BusinessPartnersController` y relacionados |
| Catálogo / Items / Productos | `/api/catalog`, `/api/items`, `/api/inventory/*` | `CatalogController`, `ItemsController`, `Product*Controller`, `BrandsController`, `TariffsController` |
| IAM / Actividad admin | `/api/admin/iam`, `/api/admin/activity` | `AccessProfilesController`, `AccessSessionController`, `ActivityController` |
| Seguridad | `/api/security` | `SecurityController` |
| Dashboard | `/api/dashboard` | `DashboardController` |
| Menú SPA | `/api/internal/spa-menu-catalog` | `SpaMenuCatalogController` |
| Público | `/api/public` | `PublicController` |
| **Integration (frontera Platform)** | `/api/integration/v1` | `IntegrationController` (policy `IntegrationApi`) |

No existen rutas `/api/platform/*`, `/api/saas/*` ni `/api/superadmin/*` — fueron eliminadas en FASE 1. Lista completa: [`ERP.API/Controllers/`](../backend/src/ERP.API/Controllers/).

> **Frontera ERP ↔ Platform:** `/api/integration/v1/*` es la **única** vía de integración permitida para una futura Platform externa — ver [ADR-ERP-002](adr/ADR-ERP-002-platform-separation.md) y [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md). Reglas obligatorias: *ERP never depends on Platform* / *Platform may consume ERP APIs only*.

---

## Frontend

- Claims JWT relevantes: `tenant_id`, `company_id` (ver [IDENTITY.md](./IDENTITY.md)).
- Flujo: login → selección/auto-asignación de company (`/select-company` si hay varias) → `CompanySwitcher`.
- Módulos activos en `frontend/src/modules/`: `auth`, `access`, `admin`, `branches`, `company-management`, `config`, `configuracion`, `dashboard`, `inventory`, `items`, `logistica`, `masterData`, `reportes`, `security`, `settings`, `shared`. Detalle funcional: [FEATURES.md](../FEATURES.md).
- Detalle auth UI: [IDENTITY.md](./IDENTITY.md#frontend).

### App Launcher

Modal de navegación (`frontend/src/components/zh/header/launcher/`), 100% data-driven desde
`GET /api/v1/me/menu` (`NavMenuGroupDto[]`, ya filtrado por permisos). Jerarquía visual fija
de 5 niveles:

| Nivel | Componente | Origen del dato |
|-------|-----------|------------------|
| 0 — Favoritos | `LauncherFavoritesSection` | `NavItem.id` marcados como favorito (`zh-favorites`) |
| 1 — Buscador | `LauncherSearchBar` | filtra recursivamente por `NavItem.label` |
| 2 — Módulos | `LauncherModuleGroup` | `MainMenuGroup` (= `NavGroup`, `id` = código de grupo del backend) |
| 3 — Categorías | `LauncherCategoryGroup` | `NavItem` con `children` |
| 4 — Formularios | `LauncherMenuItem` + `FavoriteButton` | `NavItem` hoja (`to`, `label`, `icon`, `id`) |

**Identidad estable (`NavItem.id`)**: todo `NavItem` proveniente del backend trae
`id: string` (= `NavMenuItemDto.id`, obligatorio). Es la única clave válida para estado
persistido — favoritos (`zh-favorites`, `string[]` de ids) y expand/collapse
(`zh-launcher-expanded`, claves `module:<groupId>` / `category:<NavItem.id>`). No se usa
`to` (ruta) ni `label` (traducido) como identidad: ambos cambian con el tiempo sin afectar
favoritos ni preferencias de expansión. Los pocos `NavItem` sintéticos creados en
`navConfig.ts` (p. ej. `ensureSubscriberHomeOverview`) declaran un `id` fijo con prefijo
`synthetic-`.

**Profundidad**: la jerarquía de producto es Módulo → Categoría → Formulario
(`LauncherCategoryGroup` puede anidarse recursivamente para sub-categorías). El indent
visual de `LauncherMenuItem` cubre depth 0–2 (`zh-launcher__item--depth-{0,1,2}`); una
cuarta capa de anidamiento sigue siendo funcional pero reutiliza el indent de depth 2.

**Escalabilidad**: agregar un módulo nuevo (CRM, RRHH, Producción, …) es 100% datos —
registros en la tabla de navegación + permisos. Ningún componente del Launcher contiene
comparaciones por nombre/código de módulo; los helpers de `navConfig.ts` con ids
hardcodeados (`MAIN_NAV_GROUP_ORDER`, `flattenAccessIntoSecurity`, `flattenSaaSIntoHome`,
`expandPlanCustomRootsToBarGroups`) son shims de compatibilidad para grupos legacy
específicos (`home`, `access`, `security`, `saas`, `plan-custom`) y no afectan módulos
nuevos, que caen al orden por defecto.

---

## Caching

| Cache | Patrón clave | Invalidación |
|-------|--------------|--------------|
| `CachingBehavior` (MediatR) | por request/handler | ver `ERP.Application/Behaviors/CachingBehavior.cs` |
| Permisos | por `(companyId, userId)` | perfil/membership |

Redis opcional; fallback in-memory si está deshabilitado.
