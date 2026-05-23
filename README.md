# ERP SaaS — ZH Technologies

Monorepo enterprise: **backend** (.NET 10, Clean Architecture, PostgreSQL) + **frontend** (Vite, React, TypeScript).

## Visión

ERP **SaaS multi-tenant** para Ecuador: facturación electrónica **SRI**, inventario, contabilidad, panel **platform**, planes comerciales e i18n **es / en / Kichwa (`qu`)**.

| Documento | Contenido |
|-----------|-----------|
| [`SYSTEM_TRUTH.md`](SYSTEM_TRUTH.md) | **Fuente única verdad arquitectónica** (baseline `architecture-v1.0`) |
| [`ARCHITECTURE_GATES.md`](ARCHITECTURE_GATES.md) | Gates bloqueantes PR / CI / agentes |
| [`RELEASES/RELEASE-ARCHITECTURE-v1.0.md`](RELEASES/RELEASE-ARCHITECTURE-v1.0.md) | Release baseline arquitectura |
| [`CONTEXT.md`](CONTEXT.md) | Índice maestro |
| [`AI-RULES/README.md`](AI-RULES/README.md) | **Reglas IA canónicas** (Cursor, Claude, PR) |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Arquitectura (entrada) |
| [`docs/STATUS.md`](docs/STATUS.md) | Estado delivery |
| [`FEATURES.md`](FEATURES.md) | Módulos producto |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | PR, tests, estándares |

## Estructura del repositorio

```
erp-saas/
├── AI-RULES/          # Reglas canónicas multi-agente IA
├── backend/           # .NET — src/, tests en ERP.*.Tests
├── frontend/          # React SPA — src/, e2e/
├── infrastructure/    # Docker, postgres, deploy templates
├── docs/              # Documentación producto (7 archivos canónicos + ADRs)
├── scripts/           # dev/, ci/, db/, setup/
├── tools/             # Guardrails, generators, quality
├── security/          # Políticas auth, tenant, compliance
├── monitoring/        # Preparación observabilidad
├── tests/             # Índice suites
└── .github/workflows/ # CI modular (architecture, backend, frontend)
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

Operador platform first-run: **`.\scripts\setup\Crear-PlatformOperator.ps1`**

Config: copiar `backend/src/ERP.API/appsettings.Development.json.example` → `appsettings.Development.json`.

## Auth

Access token en memoria · refresh en cookie httpOnly · rotación por familia · multi-tab (Web Locks + BroadcastChannel). Ver [`AUTH_RULES.md`](AUTH_RULES.md).

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

Tag Git: **`architecture-v1.0`** — estructura y gobernanza congeladas. Evolución: ver [`SYSTEM_TRUTH.md`](SYSTEM_TRUTH.md) §7.

## Reglas (obligatorias)

| Archivo | Ámbito |
|---------|--------|
| [`AI-RULES/README.md`](AI-RULES/README.md) | **Fuente canónica** reglas IA |
| [`SYSTEM_TRUTH.md`](SYSTEM_TRUTH.md) | Estructura y módulos oficiales |
| [`ARCHITECTURE_GATES.md`](ARCHITECTURE_GATES.md) | Gates CI / review |
| [`CLAUDE.md`](CLAUDE.md) | Adaptador Claude → `AI-RULES/` |
| [`ARCHITECTURE_RULES.md`](ARCHITECTURE_RULES.md) | PR bloqueantes → `AI-RULES/PR-RULES-CATALOG.md` |
| [`BACKEND_RULES.md`](BACKEND_RULES.md) | .NET |
| [`FRONTEND_RULES.md`](FRONTEND_RULES.md) | React |
| [`AUTH_RULES.md`](AUTH_RULES.md) | Sesión / tokens |
| [`DATABASE_RULES.md`](DATABASE_RULES.md) | PostgreSQL / EF |

ADRs: [`docs/decisions/`](docs/decisions/)

## Troubleshooting

| Problema | Acción |
|----------|--------|
| Puerto 5435 ocupado | `docker compose down` o cambiar puerto en compose |
| Migraciones EF | `.\scripts\dev\dev-restart.ps1 -Doctor` |
| Refresh 401 tras F5 | Verificar cookie `Path=/api`; ver `AUTH_RULES.md` |
| CI stack audit falla | Revisar `scripts/stack-allowlist.json` |

Detalle: [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md).
