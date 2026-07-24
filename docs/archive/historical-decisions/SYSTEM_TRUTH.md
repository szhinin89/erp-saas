# SYSTEM TRUTH — ERP SaaS ZH Technologies

> ## ⚠️ HISTÓRICO
>
> Este documento representa una decisión, auditoría o estado anterior del proyecto.
>
> **NO representa la arquitectura actual del ERP.**
>
> La fuente de verdad actual es:
> - [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md)
> - [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md)
> - El código fuente actual (`frontend/src`, `backend/src`)

---

**Versión baseline:** `architecture-v1.0` (2026-05-21)  
**Snapshot congelado** de la estructura y gobernanza al momento del freeze `architecture-v1.0`. La arquitectura **vigente** y su evolución viven en [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) (Nivel 1 — fuente de verdad del producto; ver jerarquía en [`CLAUDE.md`](CLAUDE.md#jerarquía-documental)). Ante cualquier diferencia, **prevalece `docs/ARCHITECTURE.md`**.  
Índice operativo general: [`CONTEXT.md`](CONTEXT.md) · Gates: [`ARCHITECTURE_GATES.md`](ARCHITECTURE_GATES.md)

---

## 1. Estructura oficial del monorepo

```
erp-saas/
├── backend/                 # .NET 10 — Clean Architecture modular
│   └── src/
│       ├── ERP.Domain/
│       ├── ERP.Application/
│       ├── ERP.Infrastructure/
│       ├── ERP.API/
│       └── ERP.*.Tests/     # incl. ERP.Architecture.Tests
├── frontend/                # Vite + React + TypeScript
│   └── src/
│       ├── modules/         # ← features oficiales (vertical)
│       ├── schemas/         # Zod compartidos por dominio
│       ├── components/zh/   # ZH Form System
│       ├── routes/
│       └── i18n/locales/    # es, en, qu
├── infrastructure/          # Docker base, postgres scripts, deployment stubs
├── docs/                    # 7 canónicos + decisions/, deployment/, diagrams/
├── scripts/                 # dev/, ci/, db/, setup/ — operación
├── tools/                   # architecture/, quality/, generators/
├── security/                # Políticas (auth/, stubs compliance)
├── monitoring/              # Preparación observabilidad
├── tests/                   # Índice suites (no duplica código test)
├── RELEASES/                # Baselines formales
├── .github/workflows/       # CI modular
├── docker-compose*.yml      # include → infrastructure/docker/compose.base.yml
├── SYSTEM_TRUTH.md          # ← este archivo
├── ARCHITECTURE_GATES.md
└── CONTEXT.md               # índice humano (no contradice SYSTEM_TRUTH)
```

**Prohibido recrear en raíz:** `scripts/sql/`, scripts sueltos fuera de `scriptsAllowed`, índices Markdown paralelos a `CONTEXT.md`.

---

## 2. Backend — layout real (`backend/src/`)

### 2.1 `ERP.Domain/Modules/` (vertical slices principales)

| Módulo | Propósito | Estado |
|--------|-----------|--------|
| **Access** | Perfiles, permisos por company | Oficial |
| **Audit** | Actividad de usuario | Oficial |
| **Auth** | Refresh tokens, familias, IAM | Oficial |
| **Auxiliary** | Logs WS, utilidades | Oficial |
| **Company** | Empresa operativa, establecimientos | Oficial |
| **Configuration** | Parámetros globales/módulo/feature, settings SRI | Oficial |
| **Fiscal** | Documentos fiscales / eventos electrónicos | Oficial |
| **Inventory** | Stock, kardex, transferencias, ajustes | Oficial |
| **Items** | Catálogo de ítems, variantes, unidades | Oficial |
| **Menu** | Navegación, menús personalizados por tenant | Oficial |
| **Navigation** | Grupos UI navegación | Oficial |
| **Products** | Catálogo de productos | Oficial |
| **Purchases** | Compras | Oficial |
| **Security** | Admin scopes, asignaciones | Oficial |
| **SriCatalogs** | Catálogos SRI | Oficial |
| **Tenants** | Tenant (contrato SaaS) — `SubscriberId → TenantId` consolidado FASE 4 | Oficial |
| **Common / SharedKernel** | Base entities, contratos compartidos | Oficial |
| **Subscribers** | Carpetas vacías (residuo FASE 1, sin tipos `.cs`) | Legacy — no usar |

**BusinessPartner** (Customer + Supplier roles, BP V2) vive en `ERP.Domain/MasterData/`, no en `Modules/`. Módulos de negocio como Ventas, Contabilidad, Caja, Gastos o Documentos Electrónicos descritos en versiones anteriores de este documento **no existen como módulos backend** en el estado actual — ver inventario real de módulos frontend en [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md#frontend) y estado de delivery en [`docs/STATUS.md`](docs/STATUS.md).

### 2.2 `ERP.Domain/` en raíz (fuera de `Modules/`)

| Carpeta | Contenido real | Application espejo |
|---------|----------------|-------------------|
| **Branches/** | Entidad `Branch` (sucursales) | `ERP.Application/Modules/Branches/` |
| **Geography/** | Catálogos geográficos | handlers en `Modules/` / catálogos |
| **MasterData/** | `BusinessPartner` (Customer + Supplier roles, BP V2) | `ERP.Application/MasterData/` |
| **Navigation/** | `TenantCustomMenu` | `ERP.Application/Navigation/` |
| **Sales/**, **Setup/** | Carpetas vacías legacy / setup inicial | no usar para código nuevo |
| **Common/**, **Exceptions/** | Infraestructura de dominio compartida | `ERP.Application/Common/` |

**Regla de lectura:** no asumir que todo el dominio vive bajo `Modules/`. **Branches**, **Geography**, **MasterData** y **Navigation** están en raíz de `ERP.Domain/`; **BusinessPartner** (Customer + Supplier) vive en `ERP.Domain/MasterData/Entities/BusinessPartner.cs` (BP V2, ver [`docs/STATUS.md`](docs/STATUS.md)).

> **Eliminado en FASE 1** (2026-06-05): `Billing/`, `Subscriptions/`, `Application/Admin/`, `Application/SuperAdmin/`, `Application/Platform/` — ya no existen ni en `ERP.Domain/` ni en `ERP.Application/`. Ver [`docs/STATUS.md`](docs/STATUS.md).

### 2.3 `ERP.Application/` (casos de uso)

| Área | Ubicación |
|------|-----------|
| Vertical slices producto | `ERP.Application/Modules/` (espeja la mayoría de `Domain/Modules/`) |
| Master data | `ERP.Application/MasterData/` |
| IAM / Access | `ERP.Application/IAM/`, `ERP.Application/Access/` |
| Navegación sesión | `ERP.Application/Navigation/` |
| Runtime ERP | `ERP.Application/ERPRuntime/` |
| Setup / first-run | `ERP.Application/Setup/` |
| Cross-cutting | `Behaviors/`, `Common/`, `Services/` |

---

## 3. Módulos frontend oficiales (`frontend/src/modules/`)

Features producto viven aquí. Patrón: `api/`, `schemas/`, `hooks/`, `pages/`, CSS con prefijo de página.

| Área | Carpetas representativas |
|------|-------------------------|
| Auth & sesión | `auth/` |
| Catálogo | `catalog/`, `products/`, `customers/` |
| Inventario | `inventario/` |
| Ventas | `ventas/` |
| Compras | `compras/` |
| Contabilidad | `accounting/` |
| Configuración | `configuracion/`, `config/`, `branches/` |
| Gastos | `gastos/` |
| Logística | `logistica/` |
| Acceso | `access/`, `security/` |
| SaaS plataforma | `superadmin/`, `companies/`, `company-management/` |
| Shared | `shared/` (placeholders, utilidades UI) |

---

## 4. En transición (tolerado, no expandir)

| Elemento | Ubicación actual | Dirección |
|----------|------------------|-----------|
| `frontend/src/services/` | Servicios legacy globales | **No añadir** — migrar a `modules/*/api/` |
| `frontend/src/pages/` | Páginas sueltas históricas | **No añadir** — nuevas en `modules/` |
| `Purchasing` vs `Purchases` (backend) | Dos namespaces dominio | Consolidación futura con ADR |
| Unified document schema | Flag `Documents:UseUnifiedSchema` | Opcional; SQL en `scripts/db/sql/` |
| `frontend/src/schemas/` + `modules/*/schemas/` | Duplicación parcial | Nuevos schemas: preferir módulo; no duplicar |
| Postgres integration tests (unified doc) | Seed SQL mínimo | Requiere seed EF completo para CI con Docker |

---

## 5. Legacy tolerado (grandfather)

Registrado en **`tools/architecture/architecture-grandfather.json`**.  
Estado baseline v1.0: listas vacías (sin deuda grandfather activa en handlers/TSX).

Excepciones documentadas de producto (no grandfather):

- ICE (impuesto consumos especiales): diferido hasta requerimiento cliente
- `build-and-deploy.yml`: workflow Azure legacy paralelo a `ci.yml`

---

## 6. Qué NO debe crecer más

- Carpetas en raíz fuera del mapa §1
- Scripts `.ps1` fuera de `scripts/stack-allowlist.json`
- Controllers con reglas de negocio o acceso EF directo
- Validación solo en frontend para persistencia
- IDs tenant en query URL
- Índices de documentación duplicados (usar `CONTEXT.md` + este archivo)
- Dependencias entre módulos Application sin contrato

---

## 7. Reglas de evolución post v1.0

1. **Cambio estructural** (nueva carpeta raíz, mover capas): ADR + actualizar `RELEASES/` + bump baseline menor (`architecture-v1.1`) si aplica.
2. **Nuevo módulo producto**: vertical completo backend + frontend + permisos + menú + planes SaaS.
3. **Nueva herramienta**: aprobación explícita → `docs/HERRAMIENTAS-ERP-SAAS.md` + allowlist.
4. **Excepción a gates**: ADR + grandfather temporal con fecha de remoción.
5. **Breaking API**: versionado documentado en `docs/STATUS.md`; no silent break.

---

## 8. CI/CD y calidad (verdad operativa)

| Workflow | Función |
|----------|---------|
| `ci.yml` | Orquestador |
| `architecture.yml` | Stack + guardrails |
| `backend-ci.yml` | `dotnet test` (299 tests baseline) |
| `frontend-ci.yml` | lint, build, guardrails chunk, Playwright |
| `security.yml` | Identity guardrails |

Docker local: `docker-compose.yml` → `infrastructure/docker/compose.base.yml` (Postgres `:5435`, Redis `:6379`).

---

## 9. Auth (verdad del sistema)

- **Access token:** memoria cliente (no localStorage persistente para refresh)
- **Refresh token:** cookie httpOnly, path `/api`, rotación atómica por **familia**
- **Multi-tab:** Web Locks + BroadcastChannel (`authRefreshManager`)
- **Tenant context:** JWT claims; empresa operativa vía membership + `ICurrentCompany`
- **SuperAdmin plataforma:** rutas `/superadmin/*`, `/companies` — sin UUID en URL

Detalle: [`AUTH_RULES.md`](AUTH_RULES.md), ADR-003.

---

## 10. Multiempresa (verdad del sistema)

- Entidad tenant: **Tenant** (`TenantId`, tabla `tenants`) — `SubscriberId → TenantId` consolidado en FASE 4 (ver [`docs/STATUS.md`](docs/STATUS.md))
- Empresa operativa: **Company** (`CompanyId`) — multi-empresa por tenant
- EF query filters + PostgreSQL RLS (baseline enterprise)
- Soft delete: `IsActive = false`

> Snapshot histórico de `architecture-v1.0`: este documento describía originalmente una capa SaaS (`Subscriber`, billing, commercial plans) eliminada por completo en FASE 1 — ERP Kernel Cleanup (2026-06-05). El estado vigente vive en [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) y [`docs/STATUS.md`](docs/STATUS.md).

Detalle: ADR-004, [`docs/DATABASE.md`](docs/DATABASE.md).

---

## 11. Documentación canónica (jerarquía)

1. **`SYSTEM_TRUTH.md`** — arquitectura y estructura (este archivo)
2. **`ARCHITECTURE_GATES.md`** — reglas bloqueantes
3. **`CONTEXT.md`** — índice navegable
4. **`docs/STATUS.md`** — estado delivery / MVP
5. **`docs/decisions/`** — ADRs
6. **`RELEASES/`** — baselines selladas

Si hay conflicto: **SYSTEM_TRUTH** > ADR más reciente > STATUS > README narrativo.
