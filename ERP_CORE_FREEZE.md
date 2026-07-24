# ERP CORE FREEZE — Acta de congelamiento arquitectónico

> **Estado: ERP CORE FROZEN — GOVERNANCE LOCK ACTIVE**
> Este documento certifica el cierre arquitectónico del ERP Core como producto independiente. Es vinculante para agentes IA, desarrolladores y revisores de PR — referenciado desde [CLAUDE.md](CLAUDE.md) (Nivel 1).

---

## Fecha del freeze

**2026-06-08** — commit base `da76ff9e` (rama `feat/platform-kernel-refactor`), validado en FASE 9 (ERP Core Freeze) y FASE 10 (Freeze Enforcement).

---

## Estado arquitectónico

- **Clean Architecture intacta**: dependencias unidireccionales `API → Application/Infrastructure → Domain`. `ERP.Domain` sin `ProjectReference` externas. `ERP.Application` sin dependencia de `ERP.Infrastructure`. `ERP.Infrastructure` sin lógica de negocio. `ERP.API` solo expone HTTP/DTOs.
- **CQRS (MediatR)** vigente: pipeline `ValidationBehavior → CompanyScopeBehavior → CachingBehavior`; comandos/queries en `Modules/{Module}/UseCases/`.
- **Multi-tenant operativo**: `Tenant`/`tenant_id`, `ICurrentTenant`/`CurrentTenantService`, query filters fail-closed (`EnterpriseQueryFilterConfigurator` sobre `ITenantScopedEntity`/`ICompanyOperationalEntity`), `TenantIsolationInvariantTests` en verde.
- **Build**: 0 errores / 0 warnings. **Tests**: 111/111 (Domain 54, Application 14, Infrastructure 4, API 11, Architecture 28).
- **Cero referencias activas** a `Billing`, `Subscription`, `Marketplace`, `PlatformOperator`, `CommercialPlan`, `Entitlements`, `SaaS Panel` — verificado por búsqueda exhaustiva en código fuente (cero clases, servicios, repositorios o rutas).

---

## Módulos incluidos (ERP Core vigente)

| Dominio | Carpeta / namespace | Scope |
|---------|---------------------|-------|
| Identity / Access / Security / Auth | `Modules/Auth`, `Modules/Access`, `Modules/Security` | `tenant_id` / `(company_id, identity_user_id)` |
| Tenants | `Modules/Tenants` | `tenant_id` |
| Companies / Company (Branches, Establishments, Emission Points) | `Modules/Companies`, `Modules/Company` | `company_id` |
| Master Data (Business Partners) | `MasterData` | `company_id` |
| Products / Items / Inventory / Purchases | `Modules/Products`, `Modules/Items`, `Modules/Inventory`, `Modules/Purchases` | `company_id` |
| Configuration / SRI / Fiscal | `Modules/Configuration`, `Modules/SriCatalogs`, `Modules/Fiscal` | `company_id` |
| Menu / Navigation | `Modules/Menu`, `Modules/Navigation` | `tenant_id` / `company_id` |
| Audit / Auxiliary | `Modules/Audit`, `Modules/Auxiliary` | `company_id` |
| SharedKernel / Common | `Modules/SharedKernel`, `Modules/Common` | base entities, contratos compartidos |
| Integration | `Modules/Integration` | frontera `tenant_id`/`company_id` hacia actores externos |

---

## Módulos excluidos (no existen como código — backlog futuro)

`Sales`, `Accounting`, `HR`, `CRM`, `Production`, `Reporting` — no tienen implementación en este freeze. Se incorporarán como nuevos módulos `company_id`-scoped siguiendo el patrón CQRS existente, sin reintroducir conceptos SaaS.

Excluidos permanentemente del ERP Core (viven en Platform, repositorio futuro separado): `Billing`, `Subscription`, `Marketplace`, `PlatformOperator`, `CommercialPlan`, `Entitlements`, `SaaS Panel`.

---

## Frontera de integración

**Única frontera permitida: `/api/integration/v1/*`**

- Controller: `ERP.API/Controllers/Integration/IntegrationController.cs`, policy `IntegrationApi`.
- Recursos: `tenants` (crear, status, activate, suspend), `companies` (crear, status, activate, suspend).
- Contratos/casos de uso: `ERP.Application/Modules/Integration/` — solo DTOs, comandos/queries MediatR, autorización y versionado. Sin lógica de negocio de Platform.
- Verificado: ningún otro controller expone rutas `/api/platform/*`, `/api/saas/*` ni `/api/subscribers/*`.
- Decisión formalizada en [ADR-ERP-002](docs/architecture/decisions/ADR-ERP-002-platform-separation.md) (**Status: Accepted**), alineada con Clean Architecture (capas y `Dependency Rule` de [ADR-ERP-001](docs/architecture/decisions/ADR-ERP-001-core-independence.md)), CQRS (contratos vía comandos/queries MediatR), y Multi-Tenant (`tenant_id`/`company_id` como claves de los recursos de integración).

---

## Reglas de evolución futura

1. Nuevos módulos ERP operativos (`Sales`, `Accounting`, `HR`, `CRM`, `Production`, `Reporting`, …) se agregan `company_id`-scoped, siguiendo Clean Architecture + CQRS + `CompanyScopeBehavior`.
2. Capacidades comerciales (planes, límites, billing) se implementan exclusivamente en una futura Platform externa, vía `ITenantCapabilities` (punto de extensión definido en ADR-ERP-001) y/o nuevas rutas de integración.
3. Toda nueva necesidad de Platform sobre el ERP se resuelve **extendiendo `/api/integration/v{n}/*`** — nunca abriendo acceso directo a internals.

---

## Cambios prohibidos (BLOQUEANTES)

- **ERP NEVER DEPENDS ON PLATFORM**: ningún proyecto `ERP.*` puede referenciar, importar o compilar contra código `Platform.*` / `ZH.Platform.*` (ni `ProjectReference`, ni `using`, ni DbContext, ni repositorios, ni entidades compartidas).
- **PLATFORM MAY CONSUME ERP APIs ONLY**: prohibido el acceso directo desde Platform a `ErpDbContext`, repositorios ERP, entidades de dominio ERP o query filters ERP. Toda integración pasa por `/api/integration/v1/*`.
- Reintroducir conceptos `Billing`, `Subscription`, `Marketplace`, `PlatformOperator`, `CommercialPlan`, `Entitlements` o `SaaS Panel` dentro de `ERP.*`.
- Compartir DbContexts, tablas o entidades de dominio entre ERP y Platform.
- Cambios en `Tenant` que agreguen campos comerciales (`PlanCode`, `TrialEndsAt`, etc.) sin Architecture Review — son bloqueantes en PR (ver [ADR-ERP-001](docs/architecture/decisions/ADR-ERP-001-core-independence.md)).

## Cambios permitidos

- Nuevos módulos ERP operativos `company_id`-scoped (ver "Reglas de evolución futura").
- Extensión de `ITenantCapabilities` por una futura Platform vía override de DI.
- Nuevas versiones/rutas de `/api/integration/v{n}/*`.
- Housekeeping documental (p. ej. reubicación de docs SaaS-futuras hacia `docs/future-platform/`).
- Eliminación de residuos vacíos (`Modules/Subscribers/*`) sin impacto funcional.

---

## Reglas arquitectónicas obligatorias (BLOQUEANTES)

> Vinculantes — ver [AI-RULES/CORE-ARCHITECTURE.md § Frontera ERP ↔ Platform](AI-RULES/CORE-ARCHITECTURE.md#reglas-de-arquitectura-que-no-se-rompen).

1. **"ERP NEVER DEPENDS ON PLATFORM"**
2. **"PLATFORM MAY CONSUME ERP APIs ONLY"**

---

## Certificado final

**"ERP CORE FROZEN — GOVERNANCE LOCK ACTIVE"**

No existen hallazgos críticos que impidan el freeze. El repositorio queda oficialmente cerrado para evolución independiente del ERP Core: cualquier futura plataforma SaaS deberá integrarse exclusivamente mediante `/api/integration/v1/*`.

## Addendum — API versionada y Platform.Contracts (2026-06-11)

Sin reabrir el freeze, se registran dos evoluciones consistentes con
ADR-ERP-002 y este acta:

- **API versionada**: todas las rutas de `ERP.API.Controllers` (excepto
  `api/integration/v1/*` y `api/dev/*`, sin cambios) pasan a `api/v1/...`.
  Detalle: [AI-RULES/CORE-ARCHITECTURE.md § API versionada](AI-RULES/CORE-ARCHITECTURE.md#api-versionada-apiv1).
- **`Platform.Contracts`** (`backend/src/Platform.Contracts/`): nuevo
  proyecto solo-contratos (interfaces, DTOs, marcador `IIntegrationEvent`,
  `IErpPublicApiClient`) para una futura Platform externa. Sin
  `ProjectReference` hacia/desde `ERP.*`, sin lógica de negocio — cumple
  "ERP NEVER DEPENDS ON PLATFORM" / "PLATFORM MAY CONSUME ERP APIs ONLY".
  Detalle: [backend/src/Platform.Contracts/README.md](backend/src/Platform.Contracts/README.md).

## Referencias

- [README.md](README.md) · [CLAUDE.md](CLAUDE.md) · [docs/STATUS.md](docs/STATUS.md) · [FEATURES.md](FEATURES.md) · [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- [ADR-ERP-001 — ERP Core Independence](docs/architecture/decisions/ADR-ERP-001-core-independence.md)
- [ADR-ERP-002 — Platform Separation](docs/architecture/decisions/ADR-ERP-002-platform-separation.md)
- [AI-RULES/CORE-ARCHITECTURE.md](AI-RULES/CORE-ARCHITECTURE.md)
