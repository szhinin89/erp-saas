# SYSTEM TRUTH — ERP SaaS ZH Technologies

**Versión baseline:** `architecture-v1.0` (2026-05-21)  
**Este archivo es la fuente única de verdad arquitectónica del sistema.**  
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

## 2. Módulos backend oficiales (`ERP.Domain/Modules/`)

| Módulo | Propósito | Estado |
|--------|-----------|--------|
| **Accounting** | Plan de cuentas, asientos | Oficial · referencia vertical |
| **Access** | Perfiles, permisos tenant | Oficial |
| **Auth** | Refresh tokens, familias, IAM | Oficial |
| **Auxiliary** | Logs WS, utilidades | Oficial |
| **Branches** | Sucursales | Oficial |
| **Cash** | Caja, conciliación | Oficial |
| **Company** | Empresa operativa, establecimientos | Oficial |
| **Configuration** | Parámetros, billing settings tenant | Oficial |
| **Customers** | (legacy naming) — ver Sales/Customers | Oficial |
| **ElectronicDocuments** | Modelo documentos electrónicos unificado | Oficial |
| **Expenses** | Gastos | Oficial |
| **Inventory** | Stock, kardex, transferencias, ajustes | Oficial |
| **Logistics** | Transportistas | Oficial |
| **Menu** | Navegación, features SaaS | Oficial |
| **Products** | Catálogo productos | Oficial |
| **Purchasing** / **Purchases** | Compras (conviven; unificar naming es deuda) | Oficial · naming en transición |
| **Sales** | Ventas, facturas, notas | Oficial |
| **Security** | Admin scopes plataforma | Oficial |
| **SriCatalogs** | Catálogos SRI | Oficial |
| **Subscriptions** | Planes comerciales, entitlements | Oficial |
| **Tenants** | Subscriber (SaaS tenant) | Oficial |

**Application** espeja la misma estructura bajo `ERP.Application/Modules/`.

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

## 10. Multi-tenant (verdad del sistema)

- Entidad SaaS: **Subscriber** (`SubscriberId`)
- Empresa operativa: **Company** (`CompanyId`) — multi-empresa por subscriber
- EF query filters + PostgreSQL RLS (baseline enterprise)
- Soft delete: `IsActive = false`

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
