# ERP SaaS — ZH Technologies

Monorepo enterprise: **backend** (.NET 10, Clean Architecture, PostgreSQL) + **frontend** (Vite, React, TypeScript).

## Visión

ERP **SaaS multi-tenant** para Ecuador: facturación electrónica **SRI**, inventario, contabilidad, ventas, compras, RBAC e i18n **es / en / Kichwa (`qu`)**.

## Documentación

| Necesito | Documento |
|----------|-----------|
| Índice maestro | [`CONTEXT.md`](CONTEXT.md) |
| Arquitectura vigente | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — ver jerarquía documental en [`CLAUDE.md`](CLAUDE.md#jerarquía-documental) |
| Reglas IA canónicas (Cursor, Claude, PR) | [`docs/architecture/README.md`](docs/architecture/README.md) |
| Gates bloqueantes PR / CI / agentes | [`ARCHITECTURE_GATES.md`](ARCHITECTURE_GATES.md) |
| Congelamiento arquitectura ERP Core | [`ERP_CORE_FREEZE.md`](ERP_CORE_FREEZE.md) |
| Estado delivery | [`STATUS.md`](STATUS.md) |
| Módulos del producto | [`FEATURES.md`](FEATURES.md) |
| Arranque, stack, tests | [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) |
| Contribución (PR, tests, estándares) | [`CONTRIBUTING.md`](CONTRIBUTING.md) |
| Decisiones de arquitectura (ADRs) | [`docs/decisions/`](docs/decisions/) |
| Release baseline arquitectura (histórico) | [`docs/archive/RELEASE-ARCHITECTURE-v1.0.md`](docs/archive/RELEASE-ARCHITECTURE-v1.0.md) |

## Estructura del repositorio

```
erp-saas/
├── backend/           # .NET — src/, tests en ERP.*.Tests
├── frontend/          # React SPA — src/, e2e/
├── infrastructure/    # Docker, postgres, deploy templates
├── docs/              # Documentación producto + ADRs + security
├── scripts/           # dev/, ci/, db/
├── tools/             # Guardrails, generators, quality
├── qa-engine/         # Motor QA black-box (gate CI: auth, permisos, aislamiento tenant)
└── .github/workflows/ # CI modular (architecture, backend, frontend, qa-regression)
```

## Stack

PostgreSQL 16 · Redis 7 · EF Core · MediatR · FluentValidation · JWT + refresh rotation · Vite · Playwright · Docker Compose · GitHub Actions.

Stack permitido y auditoría: [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md#stack-oficial) · `scripts/ci/verify-stack-allowlist.ps1`.

## Arranque rápido

```powershell
docker compose up -d                    # Postgres :5435, Redis :6379
.\scripts\dev\dev-restart.ps1           # atajo: Docker + migraciones + API + Vite
# Manual EF:
cd backend/src && dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
dotnet run --project ERP.API --launch-profile http   # http://localhost:5003
cd frontend && npm run dev                             # http://localhost:5173
```

First-run admin: banner en consola API (`GET /api/setup/status` + `POST /api/setup/admin`)

Config: copiar `backend/src/ERP.API/appsettings.Development.json.example` → `appsettings.Development.json`.

## Auth

Access token en memoria · refresh en cookie httpOnly · rotación por familia · multi-tab (Web Locks + BroadcastChannel). Ver [`docs/architecture/security.md`](docs/architecture/security.md) · [`docs/IDENTITY.md`](docs/IDENTITY.md).

## CI/CD

Orquestador: [`.github/workflows/ci.yml`](.github/workflows/ci.yml)

| Workflow | Job |
|----------|-----|
| `architecture.yml` | Stack allowlist + guardrails |
| `backend-ci.yml` | `dotnet test` |
| `frontend-ci.yml` | lint, build, Playwright |
| `security.yml` | Identity guardrails (reusable) |
| `e2e.yml` | E2E manual dispatch |

## Baseline arquitectura

Tag Git: **`architecture-v1.0`** — estructura y gobernanza congeladas. Evolución: ver [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) y [`RELEASES/README.md`](RELEASES/README.md).

## Troubleshooting

| Problema | Acción |
|----------|--------|
| Puerto 5435 ocupado | `docker compose down` o cambiar puerto en compose |
| Migraciones EF | `.\scripts\dev\dev-restart.ps1 -Doctor` |
| Refresh 401 tras F5 | Verificar cookie `Path=/api`; ver [`docs/IDENTITY.md`](docs/IDENTITY.md) |
| CI stack audit falla | Revisar `scripts/stack-allowlist.json` |

Detalle: [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md).
