# CONTEXT.md — ZH Technologies ERP

Índice maestro de navegación. La jerarquía y las lecturas obligatorias están en
[`CLAUDE.md`](CLAUDE.md#jerarquía-documental) y [`AGENTS.md`](AGENTS.md); este índice no las sustituye.

---

## Mapa de documentación

| Qué necesito | Archivo | Contenido |
|--------------|---------|-----------|
| **Reglas de implementación (canónico)** | [`docs/architecture/README.md`](./docs/architecture/README.md) | Fuente única: arquitectura, FE/BE, SaaS, enforcement, PR |
| **Arquitectura y acta de freeze** | [`ERP_CORE_FREEZE.md`](./ERP_CORE_FREEZE.md), [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) | Acta fechada del freeze, descripción arquitectónica vigente |
| **Gates bloqueantes** | [`ARCHITECTURE_GATES.md`](./ARCHITECTURE_GATES.md) | Reglas prohibidas/obligatorias + CI |
| **Baselines históricas** | [`RELEASES/`](./RELEASES/) | Tags `architecture-v1.0`, `frontend-governance-v1.0`; no definen comportamiento vigente |
| **Frontend baseline (UI)** | [`docs/FRONTEND_ARCHITECTURE_BASELINE.md`](./docs/FRONTEND_ARCHITECTURE_BASELINE.md) | Shells, templates, CSS, governance |
| **Frontend QA checklist** | [`docs/FRONTEND_QA_CHECKLIST.md`](./docs/FRONTEND_QA_CHECKLIST.md) | Validación manual pre-release |
| **Entrada GitHub / visión producto** | [`README.md`](./README.md) | Monorepo, stack, CI, troubleshooting |
| **Reglas globales y entradas de agentes** | [`CLAUDE.md`](./CLAUDE.md), [`AGENTS.md`](./AGENTS.md), [`backend/CLAUDE.md`](./backend/CLAUDE.md), [`frontend/CLAUDE.md`](./frontend/CLAUDE.md) | Jerarquía global común y referencias al [índice técnico](./docs/architecture/README.md) |
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

> Reglas completas en [`docs/architecture/`](docs/architecture/README.md);
> decisiones en [`docs/decisions/`](docs/decisions/README.md). Los índices y las
> descripciones generales no sustituyen las SSOT.

---

## Estructura monorepo

```
erp-saas/
├── backend/          → backend/README.md
├── frontend/         → baseline UI: `docs/FRONTEND_ARCHITECTURE_BASELINE.md`, `docs/frontend-layout-conventions.md`
├── infrastructure/   → Docker, postgres, deployment
├── docs/             → architecture/ (reglas canónicas), decisions/, diagrams/, …
├── scripts/          → dev/, ci/, db/
├── tools/            → architecture/, quality/, generators/
├── monitoring/       → preparación observabilidad
├── tests/            → índice suites
└── .github/workflows/
```

Compose: `docker-compose.yml` (infra local, include → `infrastructure/docker/compose.base.yml`) · `docker-compose.app.yml` (stack app, vía `scripts/app-stack.ps1`).

---

## Arranque local

Atajo: **`.\scripts\dev-launcher.ps1`** · First Run: [`docs/architecture/first-run.md`](./docs/architecture/first-run.md)

Manual: [`docs/DEVELOPMENT.md`](./docs/DEVELOPMENT.md#arranque-local).

---

## Scripts y tooling (.ps1 canónicos — mapa completo en [`scripts/README.md`](./scripts/README.md))

| Script | Rol |
|--------|-----|
| [`scripts/dev-launcher.ps1`](./scripts/dev-launcher.ps1) | Dev stack |
| [`scripts/app-stack.ps1`](./scripts/app-stack.ps1) | Stack de aplicación |
| [`scripts/backup-restore.ps1`](./scripts/backup-restore.ps1) | Backup / restore drill |
| [`scripts/ci/run-e2e.ps1`](./scripts/ci/run-e2e.ps1) | Playwright E2E |
| [`scripts/ci/verify-stack-allowlist.ps1`](./scripts/ci/verify-stack-allowlist.ps1) | CI stack audit |
| [`scripts/tests/`](./scripts/tests/) | Cuatro pruebas focales del launcher: [First Run](./docs/architecture/first-run.md) |
| [`tools/architecture/check-identity-guardrails.ps1`](./tools/architecture/check-identity-guardrails.ps1) | Auth legacy |
| [`tools/quality/check-handler-size.ps1`](./tools/quality/check-handler-size.ps1) | Handler size |
| [`tools/architecture/check-architecture-guardrails.ps1`](./tools/architecture/check-architecture-guardrails.ps1) | Architecture |
| [`tools/architecture/check-staged-guardrails.ps1`](./tools/architecture/check-staged-guardrails.ps1) | Precommit sobre staging (stack + architecture) |
| [`tools/generators/new-master-module.ps1`](./tools/generators/new-master-module.ps1) | Scaffolding |

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
3. Avance → [sincronización documental](docs/architecture/enforcement.md#sincronización-docs-de-avance); estado y cierres en **`STATUS.md`**.
4. Stack → **`docs/DEVELOPMENT.md#stack-oficial`** + allowlist.

Copilot: [`.github/INSTRUCCIONES-COPILOT.md`](./.github/INSTRUCCIONES-COPILOT.md).

---

## Otros

| Tipo | Dónde |
|------|--------|
| Reglas Cursor | `.cursor/rules/*.mdc` |
| Ops postgres | `infrastructure/postgres/` |
| Spec SRI PDF | `docs/*.pdf` |

---
