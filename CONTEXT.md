# CONTEXT.md — ZH Technologies ERP

Índice maestro. Lee primero este archivo; luego abre **solo** el documento de tu tarea.

---

## Mapa de documentación

| Qué necesito | Archivo | Contenido |
|--------------|---------|-----------|
| **Reglas de implementación (canónico)** | [`docs/architecture/README.md`](./docs/architecture/README.md) | Fuente única: arquitectura, FE/BE, SaaS, enforcement, PR |
| **Verdad arquitectónica (baseline)** | [`ERP_CORE_FREEZE.md`](./ERP_CORE_FREEZE.md), [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) | Congelamiento ERP Core, arquitectura vigente |
| **Gates bloqueantes** | [`ARCHITECTURE_GATES.md`](./ARCHITECTURE_GATES.md) | Reglas prohibidas/obligatorias + CI |
| **Baseline sellada** | [`RELEASES/`](./RELEASES/) | `architecture-v1.0`, `frontend-governance-v1.0` |
| **Frontend baseline (UI)** | [`docs/FRONTEND_ARCHITECTURE_BASELINE.md`](./docs/FRONTEND_ARCHITECTURE_BASELINE.md) | Shells, templates, CSS, governance |
| **Frontend QA checklist** | [`docs/FRONTEND_QA_CHECKLIST.md`](./docs/FRONTEND_QA_CHECKLIST.md) | Validación manual pre-release |
| **Entrada GitHub / visión producto** | [`README.md`](./README.md) | Monorepo, stack, CI, troubleshooting |
| **Reglas de código (agentes)** | [`CLAUDE.md`](./CLAUDE.md), [`backend/CLAUDE.md`](./backend/CLAUDE.md), [`frontend/CLAUDE.md`](./frontend/CLAUDE.md) | Adaptadores → [`docs/architecture/`](./docs/architecture/README.md) |
| **Estado delivery** | [`STATUS.md`](./STATUS.md) | Fuente de verdad MVP |
| **Reglas PR** | [`docs/architecture/pr-rules-catalog.md`](./docs/architecture/pr-rules-catalog.md) | Catálogo B-xx/F-xx |
| **Backend / Frontend / Auth / DB** | [`docs/architecture/backend.md`](./docs/architecture/backend.md), [`docs/architecture/frontend.md`](./docs/architecture/frontend.md), [`docs/architecture/security.md`](./docs/architecture/security.md), [`docs/DATABASE.md`](./docs/DATABASE.md) | Reglas canónicas por dominio |
| **Contribución** | [`CONTRIBUTING.md`](./CONTRIBUTING.md) | PR, tests, prohibiciones |
| **Features** | [`FEATURES.md`](./FEATURES.md) | Módulos producto |
| **Prioridades** | [`docs/ROADMAP.md`](./docs/ROADMAP.md) | Fases pendientes |
| **Desarrollo** | [`docs/DEVELOPMENT.md`](./docs/DEVELOPMENT.md) | Arranque, stack, tests |
| **Platform (futuro, no implementado)** | [`docs/future-platform/README.md`](./docs/future-platform/README.md) | Posible plataforma externa futura — no forma parte del ERP actual |
| **SaaS comercial (histórico)** | [`docs/archive/SAAS-COMMERCIAL.md`](./docs/archive/SAAS-COMMERCIAL.md) | Planes, billing — eliminado FASE 1, ver [`ERP_CORE_FREEZE.md`](./ERP_CORE_FREEZE.md) |
| **Base de datos** | [`docs/DATABASE.md`](./docs/DATABASE.md) | EF, RLS |
| **ADRs** | [`docs/decisions/`](./docs/decisions/) | Decisiones arquitectura |

> Los **7 archivos canónicos** siguen en `docs/` raíz. Subcarpetas (`adr/`, `diagrams/`, …) amplían sin reemplazar.

---

## Estructura monorepo

```
erp-saas/
├── backend/          → backend/README.md
├── frontend/         → baseline UI: `docs/FRONTEND_ARCHITECTURE_BASELINE.md`, `docs/frontend-layout-conventions.md`
├── infrastructure/   → Docker, postgres, deployment
├── docs/             → 7 canónicos + architecture/ (reglas canónicas), decisions/, diagrams/, …
├── scripts/          → dev/, ci/, db/
├── tools/            → architecture/, quality/, generators/
├── monitoring/       → preparación observabilidad
├── tests/            → índice suites
└── .github/workflows/
```

Compose: `docker-compose.yml` (include → `infrastructure/docker/compose.base.yml`).

---

## Arranque local

Atajo: **`.\scripts\dev\dev-restart.ps1`** · First-run admin: banner en consola API (`GET /api/setup/status` + `POST /api/setup/admin`)

Manual: [`docs/DEVELOPMENT.md`](./docs/DEVELOPMENT.md#arranque-local).

---

## Scripts y tooling (8 .ps1 canónicos)

| Script | Rol |
|--------|-----|
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
2. PR bloqueantes → **`docs/architecture/pr-rules-catalog.md`** / **`docs/ARCHITECTURE-RULES.md`**.
3. Estado → **`STATUS.md`** + **`PROGRESS.html`** al cerrar tareas.
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
