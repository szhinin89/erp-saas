# Herramientas oficiales — ERP SaaS ZH Technologies

**Fuente de verdad del stack permitido.** Las reglas Cursor en `.cursor/rules/stack-tools-source-of-truth.mdc` apuntan aquí.

Verificación automática: `scripts/verify-stack-allowlist.ps1` (lista en `scripts/stack-allowlist.json`).

---

## Runtime y plataforma

| Área | Herramienta | Versión / nota |
|------|-------------|----------------|
| Backend SDK | .NET | **10.0.201** (`backend/src/global.json`) |
| ORM | Entity Framework Core + Npgsql | PostgreSQL |
| CQRS / mediación | MediatR | Commands/Queries |
| Validación backend | FluentValidation | Pipeline MediatR |
| Auth API | JWT Bearer | `Microsoft.AspNetCore.Authentication.JwtBearer` |
| Cache / sesión | Redis | StackExchange.Redis |
| Jobs en background | Hangfire + Hangfire.PostgreSQL | |
| Logs | Serilog | Console + File |
| PDF / RIDE | QuestPDF, RazorLight | |
| Excel export | ClosedXML | |
| API docs | Swashbuckle (Swagger) | `/swagger` en dev |
| Base de datos | **PostgreSQL 16** | Docker `postgreszh`, puerto **5435** |
| Cache local | **Redis 7** | Docker, puerto **6379** |
| Contenedores | Docker Compose | Raíz del repo |

---

## Frontend

| Herramienta | Uso |
|-------------|-----|
| React 19 + TypeScript | UI |
| Vite | Dev server y build |
| React Router v7 | Rutas SPA |
| React Hook Form + Zod | Formularios |
| Axios | HTTP |
| Zustand | Estado cliente |
| Recharts | Gráficos |
| i18next (locales `es`, `en`, `qu`) | i18n |
| ESLint | Lint |
| Vitest | Tests unitarios |
| Playwright | E2E (`frontend/e2e/`) |

---

## Tests backend

| Herramienta | Proyecto |
|-------------|----------|
| xUnit | `ERP.*.Tests` |
| Moq | Mocks |
| FluentAssertions | Aserciones |
| Microsoft.AspNetCore.Mvc.Testing | Integración API |
| EF InMemory | Tests aislados cuando aplica |

---

## CI / operación

| Herramienta | Ubicación |
|-------------|-----------|
| GitHub Actions | `.github/workflows/ci.yml` |
| Dependabot | `.github/dependabot.yml` |
| PowerShell | Scripts en `scripts/` |
| EF CLI | Migraciones en `ERP.Infrastructure/Migrations/` |

---

## Scripts SQL de mantenimiento

| Archivo | Propósito |
|---------|-----------|
| `scripts/sql/refactor_rename.sql` | Migración idempotente de rutas/permisos (v1) |
| `scripts/sql/refactor_rename_v2.sql` | Idem v2 |
| `scripts/sql/refactor_rename_v3.sql` | Idem v3 (más reciente) |
| `scripts/sql/002_unified_documents_schema_and_migration.sql` | Esquema documentos unificado |
| `backend/src/ERP.Infrastructure/Seeding/InstallData/*.sql` | Bootstrap en arranque API |

---

## Fuera del stack (no proponer sin autorización)

Patrones bloqueados en `scripts/stack-allowlist.json`:

- Kubernetes, Terraform
- RabbitMQ, Kafka
- Prometheus, Datadog
- SQL Server / `UseSqlServer`

Si hace falta otra herramienta: confirmar con el equipo y actualizar **este archivo** + `stack-allowlist.json` + `verify-stack-allowlist.ps1`.

---

## Documentación relacionada

| Tema | Archivo |
|------|---------|
| Arranque local | `CLAUDE.md`, `docs/DEVELOPMENT-RULES.md` |
| Arquitectura | `docs/ARCHITECTURE.md` |
| Índice maestro | `CONTEXT.md` |
| Reglas de agentes | `.cursor/rules/erp-unified-rules.mdc` |
