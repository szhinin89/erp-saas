# Desarrollo — reglas, stack y operación

Reglas oficiales para contribuidores y agentes. Violaciones rompen aislamiento multi-tenant o límites de billing.

**Stack permitido:** sección [Stack oficial](#stack-oficial) (verificado por `scripts/ci/verify-stack-allowlist.ps1` y `scripts/stack-allowlist.json`).

---

## Convenciones de código

### Naming

| Área | Convención |
|------|------------|
| Tablas / columnas | `snake_case` |
| Índices | `ix_*`, `ux_*`, `uq_*` |
| Foreign keys | `fk_*` con `_tenant_` (no `_subscriber_`) |
| Tipos dominio | PascalCase; mapeo EF en configurations |
| Retirado | `Subscriber`, `subscriber_id` |

### Alcance de entidades

Antes de crear tablas, declarar scope — ver [ARCHITECTURE.md](./ARCHITECTURE.md#scopes).

### NEVER

- `IgnoreQueryFilters()` sin `PlatformQueryReason`
- `company_id` del body como autoridad
- Límites `MAX_*` hardcodeados en handlers
- Mezclar billing SaaS con facturas ERP
- Stripe SDK dentro de handlers MediatR
- Migraciones `.cs` a mano sin `dotnet ef migrations add`
- `Subscriber` como concepto ERP Core (retirado — usar `Tenant`)

### ALWAYS

- `ICurrentTenant` / `ICurrentCompany` para contexto
- `ICompanyAccessGuard` o `CompanyScopeBehavior` en ERP
- `dotnet ef migrations add <Name>` para schema

> **Fuera del ERP Core (histórico / futura Platform externa):** `ICommercialPlanLimitService`, `IBillingGovernanceService` e invalidación de cache de entitlements pertenecían al Control Plane SaaS eliminado en FASE 1 (ver [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md), [`docs/STATUS.md`](./STATUS.md)). No son reglas ALWAYS vigentes del ERP Core — se conservan como referencia histórica y como posible contrato de una futura Platform externa, no como requisito de desarrollo actual.

### Checklist caso de uso ERP nuevo

1. Handler en namespace acotado (Sales, Inventory, …) **o** `ICompanyScopedRequest`
2. Pasar `companyId` desde `ICurrentCompany` a factories (Wave 1+)
3. `ITenantOnlyRequest` solo en endpoints platform reales
4. Seed de permiso si aplica nuevo `perm:*`

---

## Arranque local

```powershell
# Recomendado: Docker + migraciones + API + Vite
.\scripts\dev-restart.ps1

# Solo Docker (Postgres + Redis)
.\scripts\dev-restart.ps1 -DockerUp

# Migraciones sin abrir ventanas
.\scripts\dev-restart.ps1 -NoStart

# Diagnóstico
.\scripts\dev-restart.ps1 -Doctor
```

Manual:

```powershell
docker compose up -d
cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
cd ../ERP.API
dotnet run
cd ../../../frontend
npm run dev
```

- First-run admin: banner en consola API (`GET /api/setup/status` + `POST /api/setup/admin`, token-gated)
- Tras cambios de esquema (pre-producción): recrear BD local — `dotnet ef database drop --project backend/src/ERP.Infrastructure --startup-project backend/src/ERP.API -f` y luego `dotnet ef database update`
- Copiar `appsettings.Development.json.example` → `appsettings.Development.json`
- PostgreSQL: `Host=localhost;Port=5435;Database=dberpsaas`

---

## Scripts PowerShell (canónicos)

Solo **8** scripts `.ps1` en el repo (+ `tools/architecture/architecture-grandfather.json`). CI los valida vía `scriptsAllowed` en [`scripts/stack-allowlist.json`](../scripts/stack-allowlist.json) (`scripts/ci/verify-stack-allowlist.ps1`).

| Script | Uso |
|--------|-----|
| `scripts/dev/dev-restart.ps1` | Entorno dev (Docker, EF, API, Vite) |
| `scripts/ci/run-e2e.ps1` | Playwright E2E |
| `scripts/ci/verify-stack-allowlist.ps1` | CI: NuGet, npm, Docker, Actions, patrones, **scripts** |
| `tools/architecture/check-identity-guardrails.ps1` | CI: sin referencias auth legacy `users` |
| `tools/quality/check-handler-size.ps1` | CI: MediatR `Handle` ≤ 150 líneas |
| `tools/architecture/check-architecture-guardrails.ps1` | CI: capas, patrones frontend, límites TSX, chunk Vite |
| `tools/generators/new-master-module.ps1` | Scaffolding vertical módulo master |
| `scripts/db/import_inec_ecuador_geography.ps1` | Generar SQL geografía INEC (ArcGIS → `geo_*`) |

Índice breve: [`scripts/README.md`](../scripts/README.md).

### `dev-restart.ps1` — flags útiles

| Flag | Efecto |
|------|--------|
| *(ninguno)* | Docker up + migraciones + ventanas API y Vite |
| `-DockerUp` | Solo `docker compose up -d` |
| `-SkipDocker` | Omite Docker (Postgres/Redis ya corriendo) |
| `-NoStart` | Migraciones sin abrir ventanas |
| `-NoMigrate` | Solo libera puertos y arranca apps |
| `-Doctor` | Diagnóstico (puertos, toolchain, EF pending) |
| `-StrictMigrate` | Falla si EF update falla |

### `run-e2e.ps1` — flags útiles

| Flag | Efecto |
|------|--------|
| `-SkipDocker` | Postgres ya en `:5435` |
| `-SkipMigrations` | Omite `dotnet ef database update` |
| `-PlaywrightArgs` | P. ej. `"e2e/smoke.spec.ts"` |

Scripts SQL puntuales: [`scripts/db/sql/`](../scripts/db/sql/) (ver [`scripts/db/sql/README.md`](../scripts/db/sql/README.md)). Datos de instalación inmutables: `InstallData/*.sql` — ver [DATABASE.md](./DATABASE.md).

**Regla:** no crear `.ps1` adicionales sin ampliar `scriptsAllowed`, este mapa y [`CONTEXT.md`](../CONTEXT.md).

---

## Stack oficial

Fuente de verdad para agentes: [`AI-RULES/STACK.md`](../AI-RULES/STACK.md) · `.cursor/rules/stack-tools-source-of-truth.mdc`.

### Runtime

| Área | Herramienta | Nota |
|------|-------------|------|
| Backend | .NET **10.0.201** | `backend/src/global.json` |
| ORM | EF Core + Npgsql | PostgreSQL |
| CQRS | MediatR | |
| Validación | FluentValidation | Pipeline MediatR |
| Auth | JWT Bearer | |
| Cache | Redis 7 | puerto 6379 |
| Jobs | Hangfire + PostgreSQL | |
| Logs | Serilog | |
| PDF | QuestPDF, RazorLight | |
| Excel | ClosedXML | |
| API docs | Swashbuckle | `/swagger` |
| BD | PostgreSQL **16** | Docker `postgreszh`, puerto **5435** |
| Contenedores | Docker Compose | raíz repo |

### Frontend

React 19, TypeScript, Vite, React Router v7, React Hook Form + Zod, Axios, Zustand, Recharts, i18next (`es`, `en`, `qu`), ESLint, Vitest, Playwright.

### Tests backend

xUnit, Moq, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing`, EF InMemory cuando aplique.

### CI

GitHub Actions (`.github/workflows/ci.yml`), Dependabot, EF CLI.

### Fuera del stack (consultar antes)

Kubernetes, Terraform, RabbitMQ, Kafka, Prometheus, Datadog, SQL Server — bloqueados en `stack-allowlist.json`.

---

## Migraciones (dev)

Política completa: [DATABASE.md](./DATABASE.md#migraciones).

Fresh DB (solo dev):

```sql
DROP SCHEMA public CASCADE;
CREATE SCHEMA public;
GRANT ALL ON SCHEMA public TO postgres;
GRANT ALL ON SCHEMA public TO public;
```

Luego `dotnet ef database update`.

---

## Tests

```powershell
cd backend/src
dotnet test ERP.Domain.Tests
dotnet test ERP.Infrastructure.Tests
dotnet test ERP.Application.Tests
dotnet test ERP.API.Tests
```

### E2E (Playwright)

Requisitos: Docker, .NET SDK, Node 22+, `npx playwright install chromium`.

```powershell
pwsh -File scripts/ci/run-e2e.ps1
pwsh -File scripts/ci/run-e2e.ps1 -SkipDocker
pwsh -File scripts/ci/run-e2e.ps1 -PlaywrightArgs "e2e/smoke.spec.ts"
```

Primer usuario del sistema: `dev-launcher.ps1` detecta automáticamente `isInitialized = false`,
recoge los datos del administrador inicial y llama a `POST /api/setup/admin` con el Setup Token
que el backend genera e imprime en su consola al arrancar (single-use, expira a los 15 min).
No existen credenciales sembradas por defecto ni tokens configurables vía appsettings/env.

---

## Verificación manual

| # | Check |
|---|--------|
| 1 | `GET /health/live` → 200 |
| 2 | `dotnet ef migrations has-pending-model-changes` → false |
| 3 | Login → JWT con `tenant_id` |
| 4 | Switch company → `AuthResponseDto.CompanyId` actualizado |
| 5 | ERP sin header `X-Company-Id` → 403 |
| 6 | Crear company sobre `MAX_COMPANIES` → 403 |

---

## Patrones futuros (no implementados)

- Transactional outbox
- Distributed locks para cuotas
- Concurrencia optimista en agregados calientes
