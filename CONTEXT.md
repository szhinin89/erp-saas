# CONTEXT.md — ZH Technologies ERP

Índice maestro. Lee primero este archivo; luego abre **solo** el documento de tu tarea.

---

## Mapa de documentación

| Qué necesito | Archivo | Contenido |
|--------------|---------|-----------|
| **Reglas IA (canónico)** | [`AI-RULES/README.md`](./AI-RULES/README.md) | Fuente única: arquitectura, FE/BE, SaaS, enforcement, PR |
| **Verdad arquitectónica (baseline)** | [`SYSTEM_TRUTH.md`](./SYSTEM_TRUTH.md) | Estructura oficial, módulos, evolución |
| **Gates bloqueantes** | [`ARCHITECTURE_GATES.md`](./ARCHITECTURE_GATES.md) | Reglas prohibidas/obligatorias + CI |
| **Baseline sellada** | [`RELEASES/`](./RELEASES/) | `architecture-v1.0`, `frontend-governance-v1.0` |
| **Frontend baseline (UI)** | [`docs/FRONTEND_ARCHITECTURE_BASELINE.md`](./docs/FRONTEND_ARCHITECTURE_BASELINE.md) | Shells, templates, CSS, governance |
| **Frontend QA checklist** | [`docs/FRONTEND_QA_CHECKLIST.md`](./docs/FRONTEND_QA_CHECKLIST.md) | Validación manual pre-release |
| **Entrada GitHub / visión producto** | [`README.md`](./README.md) | Monorepo, stack, CI, troubleshooting |
| **Reglas de código (agentes)** | [`CLAUDE.md`](./CLAUDE.md) | Adaptador → [`AI-RULES/`](./AI-RULES/README.md) |
| **Arquitectura (entrada)** | [`ARCHITECTURE.md`](./ARCHITECTURE.md) | → [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) |
| **Estado delivery** | [`docs/STATUS.md`](./docs/STATUS.md) | Fuente de verdad MVP |
| **Reglas PR (entrada)** | [`ARCHITECTURE_RULES.md`](./ARCHITECTURE_RULES.md) | → [`AI-RULES/PR-RULES-CATALOG.md`](./AI-RULES/PR-RULES-CATALOG.md) |
| **Backend / Frontend / Auth / DB** | [`BACKEND_RULES.md`](./BACKEND_RULES.md), [`FRONTEND_RULES.md`](./FRONTEND_RULES.md), [`AUTH_RULES.md`](./AUTH_RULES.md), [`DATABASE_RULES.md`](./DATABASE_RULES.md) | Adaptadores → [`AI-RULES/`](./AI-RULES/README.md) |
| **Contribución** | [`CONTRIBUTING.md`](./CONTRIBUTING.md) | PR, tests, prohibiciones |
| **Features** | [`FEATURES.md`](./FEATURES.md) | Módulos producto |
| **Prioridades** | [`docs/ROADMAP.md`](./docs/ROADMAP.md) | Fases pendientes |
| **Desarrollo** | [`docs/DEVELOPMENT.md`](./docs/DEVELOPMENT.md) | Arranque, stack, tests |
| **Platform (equipo — leer primero)** | [`docs/platform/TEAM-NAMING-GUIDE.md`](./docs/platform/TEAM-NAMING-GUIDE.md) | Naming, rutas, prohibiciones `SuperAdmin` |
| **Platform docs** | [`docs/platform/README.md`](./docs/platform/README.md) | Índice control plane |
| **Platform — rutas canónicas** | [`docs/platform/CANONICAL-ROUTES.md`](./docs/platform/CANONICAL-ROUTES.md) | API `/api/platform/*`, UI `/platform/*` |
| **Platform — modelo limpio** | [`docs/platform/CLEAN_TARGET_MODEL.md`](./docs/platform/CLEAN_TARGET_MODEL.md) | Entidad → API → frontend |
| **SaaS comercial** | [`docs/SAAS-COMMERCIAL.md`](./docs/SAAS-COMMERCIAL.md) | Planes, billing |
| **Base de datos** | [`docs/DATABASE.md`](./docs/DATABASE.md) | EF, RLS |
| **ADRs** | [`docs/decisions/`](./docs/decisions/) | Decisiones arquitectura |

> Los **7 archivos canónicos** siguen en `docs/` raíz. Subcarpetas (`decisions/`, `diagrams/`, …) amplían sin reemplazar.

---

## Estructura monorepo

```
erp-saas/
├── AI-RULES/         → fuente canónica reglas IA (README.md)
├── backend/          → backend/README.md
├── frontend/         → baseline UI: `docs/FRONTEND_ARCHITECTURE_BASELINE.md`, `docs/frontend-layout-conventions.md`
├── infrastructure/   → Docker, postgres, deployment
├── docs/             → 7 canónicos + decisions/, diagrams/, …
├── scripts/          → dev/, ci/, db/, setup/
├── tools/            → architecture/, quality/, generators/
├── security/         → auth/, tenant-isolation/, …
├── monitoring/       → preparación observabilidad
├── tests/            → índice suites
└── .github/workflows/
```

Compose: `docker-compose.yml` (include → `infrastructure/docker/compose.base.yml`).

---

## Arranque local

Atajo: **`.\scripts\dev\dev-restart.ps1`** · Operador platform first-run: **`.\scripts\setup\Crear-PlatformOperator.ps1`** (ver [`docs/platform/TEAM-NAMING-GUIDE.md`](./docs/platform/TEAM-NAMING-GUIDE.md))

Manual: [`docs/DEVELOPMENT.md`](./docs/DEVELOPMENT.md#arranque-local).

---

## Scripts y tooling (9 .ps1 canónicos)

| Script | Rol |
|--------|-----|
| [`scripts/setup/Crear-PlatformOperator.ps1`](./scripts/setup/Crear-PlatformOperator.ps1) | Operador platform first-run |
| [`scripts/dev/dev-restart.ps1`](./scripts/dev/dev-restart.ps1) | Dev stack |
| [`scripts/ci/run-e2e.ps1`](./scripts/ci/run-e2e.ps1) | Playwright E2E |
| [`scripts/ci/verify-stack-allowlist.ps1`](./scripts/ci/verify-stack-allowlist.ps1) | CI stack audit |
| [`tools/architecture/check-identity-guardrails.ps1`](./tools/architecture/check-identity-guardrails.ps1) | Auth legacy |
| [`tools/quality/check-handler-size.ps1`](./tools/quality/check-handler-size.ps1) | Handler size |
| [`tools/architecture/check-architecture-guardrails.ps1`](./tools/architecture/check-architecture-guardrails.ps1) | Architecture |
| [`tools/generators/new-master-module.ps1`](./tools/generators/new-master-module.ps1) | Scaffolding |
| [`scripts/db/import_inec_ecuador_geography.ps1`](./scripts/db/import_inec_ecuador_geography.ps1) | Geografía INEC |

Grandfather JSON: [`tools/architecture/architecture-grandfather.json`](./tools/architecture/architecture-grandfather.json)

Índices: [`scripts/README.md`](./scripts/README.md) · [`tools/README.md`](./tools/README.md)

No añadir `.ps1` sin `scripts/stack-allowlist.json` (`scriptsAllowed`).

---

## CI

Orquestador: [`.github/workflows/ci.yml`](./.github/workflows/ci.yml) → `architecture.yml`, `backend-ci.yml`, `frontend-ci.yml`.

---

## Reglas para agentes

1. **`CLAUDE.md`** + **`docs/DEVELOPMENT.md`** antes de implementar.
2. PR bloqueantes → **`docs/ARCHITECTURE-RULES.md`** / reglas raíz `*_RULES.md`.
3. Estado → **`docs/STATUS.md`** + **`PROGRESS.html`** al cerrar tareas.
4. Stack → **`docs/DEVELOPMENT.md#stack-oficial`** + allowlist.

Copilot: [`.github/INSTRUCCIONES-COPILOT.md`](./.github/INSTRUCCIONES-COPILOT.md).

---

## Otros

| Tipo | Dónde |
|------|--------|
| Reglas Cursor | `.cursor/rules/*.mdc` |
| SQL auxiliar | `scripts/db/sql/` |
| Ops postgres | `infrastructure/postgres/` |
| Spec SRI PDF | `docs/*.pdf` |

---
