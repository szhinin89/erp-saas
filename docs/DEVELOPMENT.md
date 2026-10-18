# Desarrollo — arranque, stack y operación

Guía operativa para contribuidores y agentes (arranque local, Docker, tests, versiones de stack). Reglas normativas (naming, multi-tenant, migraciones, capas): [`docs/architecture/`](architecture/README.md) — no repetidas aquí, ver Bloque 16A/16B.

**Stack permitido:** sección [Stack oficial](#stack-oficial) (verificado por `scripts/ci/verify-stack-allowlist.ps1` y `scripts/stack-allowlist.json`). Reglas: [`docs/architecture/stack.md`](architecture/stack.md).

---

## Convenciones de código

Naming (tablas, índices, FKs, tipos): [`docs/architecture/naming.md`](architecture/naming.md).

Alcance de entidades: antes de crear tablas, declarar scope — ver [ARCHITECTURE.md](./ARCHITECTURE.md#scopes) y [`docs/architecture/architecture.md § Canonical Model Map`](architecture/architecture.md#canonical-model-map).

Reglas multi-tenant (`IgnoreQueryFilters()`, `company_id` del body, `ICurrentTenant`/`ICurrentCompany`), migraciones EF, y billing/SaaS fuera de ERP Core: [`docs/architecture/security.md`](architecture/security.md), [`docs/architecture/backend.md`](architecture/backend.md).

> **Fuera del ERP Core (histórico / futura Platform externa):** `ICommercialPlanLimitService`, `IBillingGovernanceService` e invalidación de cache de entitlements pertenecían al Control Plane SaaS eliminado en FASE 1 (ver [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md), [`STATUS.md`](../STATUS.md)). No son reglas vigentes del ERP Core — se conservan como referencia histórica y como posible contrato de una futura Platform externa, no como requisito de desarrollo actual.

### Checklist caso de uso ERP nuevo

1. Handler en namespace acotado (Sales, Inventory, …) **o** `ICompanyScopedRequest`
2. Pasar `companyId` desde `ICurrentCompany` a factories (Wave 1+)
3. `ITenantOnlyRequest` solo en endpoints platform reales
4. Seed de permiso si aplica nuevo `perm:*`

---

## Arranque local

```powershell
# Recomendado: menú interactivo (Docker + migraciones + first-run + API + Vite, reset, tests)
.\scripts\dev-launcher.ps1
```

| Opción | Efecto |
|--------|--------|
| `[1] Iniciar ERP` | Docker up + migraciones + API + Vite (uso diario) |
| `[2] Reset completo` | **Destructivo**: drop de `erp_zh_dev` + migraciones + alta first-run exclusivamente manual por el propietario |
| `[3] Solo Backend API` | Migraciones + API |
| `[4] Tests completos` | Suites backend/frontend |

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
- Reset local: usar exclusivamente la opcion `[2]` del launcher; valida el destino EF efectivo (Development, loopback:5435, `erp_zh_dev`) y exige confirmacion textual exacta antes del borrado. No sustituir la guarda por un drop directo.
- `dotnet-ef` es herramienta local versionada en `.config/dotnet-tools.json` (alineada con EF Core del proyecto): ejecutar `dotnet tool restore` una vez tras clonar o al cambiar su versión. CI y los scripts (`dev-launcher.ps1`, `run-e2e.ps1`) lo hacen automáticamente.
- `dotnet ef` siempre con `--startup-project ERP.API`: la factory de diseño (`ERP.API/Hosting/ErpDesignTimeDbContextFactory.cs`) usa las mismas fuentes que el runtime.

### Configuración y bases por entorno

| Entorno | Base | Fuente de la conexión |
|---------|------|------------------------|
| Desarrollo | `erp_zh_dev` (`postgreszh`, puerto 5435) | user-secrets de ERP.API: `ConnectionStrings:DefaultConnection` y `Hangfire:StorageConnectionString` (misma base) |
| Tests automatizados | Testcontainers (efímera) | cada test |
| E2E / ensayo del stack app | `erp_zh_test` | `run-e2e.ps1` (deriva la conexión y cambia solo la base) / `.env.app` |
| Producción | `erp_zh_prod` (solo en su host; nunca se crea en local) | entorno / gestor de secretos |

- `appsettings.json` no trae connection strings. Precedencia: appsettings < user-secrets (**solo Development**) < variables de entorno < línea de comandos. Contrato de entorno: `ConnectionStrings__DefaultConnection` (sin alias).
- Configurar user-secrets (una vez):

  ```powershell
  cd backend/src/ERP.API
  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5435;Database=erp_zh_dev;Username=postgres;Password=<password>"
  dotnet user-secrets set "Hangfire:StorageConnectionString" "Host=localhost;Port=5435;Database=erp_zh_dev;Username=postgres;Password=<password>"
  ```
- Infraestructura local: `docker-compose.yml` (PostgreSQL + Redis; `POSTGRES_PASSWORD` desde `.env`, plantilla `.env.example`). Stack de aplicación: `docker-compose.app.yml` vía `scripts/app-stack.ps1` — ver [`docs/deployment/README.md`](deployment/README.md).

---

## Scripts PowerShell (canónicos)

CI valida cada `.ps1` contra `scriptsAllowed` (lista explícita) y `scriptsAllowedRoots` (subsistemas `tools/dashboard/`, `print-agent/scripts/`) en [`scripts/stack-allowlist.json`](../scripts/stack-allowlist.json) (`scripts/ci/verify-stack-allowlist.ps1`).

| Script | Uso |
|--------|-----|
| `scripts/dev-launcher.ps1` | Entorno dev (Docker, EF, first-run, API, Vite, reset, tests) |
| `scripts/app-stack.ps1` | Stack de aplicación `docker-compose.app.yml` (Config/Build/Up/Down/Status/Logs/Health); rechaza `erp_zh_dev` |
| `scripts/backup-restore.ps1` | Backup y restore drill aislado (`-Action Backup\|RestoreCheck -Database <db>` obligatorio) |
| `scripts/backend-check.ps1` / `scripts/frontend-check.ps1` | Revisión/compilación/pruebas locales por capa |
| `scripts/ci/run-e2e.ps1` | Playwright E2E sobre `erp_zh_test` |
| `scripts/ci/verify-stack-allowlist.ps1` | CI: NuGet, npm, Docker, Actions, patrones, **scripts** |
| `scripts/ci/check-pages-wrapper-rule.ps1` | CI: regla de wrappers de páginas |
| `scripts/ds/*.ps1` | Auditoría y guard del Design System |
| `scripts/qa/diagnose-pos-warehouse.ps1` | Diagnóstico POS/bodega |
| `scripts/db/import_inec_ecuador_geography.ps1` | Generar SQL geografía INEC (ArcGIS → `geo_*`) |
| `tools/architecture/check-identity-guardrails.ps1` | CI: sin referencias auth legacy `users` |
| `tools/quality/check-handler-size.ps1` | CI: MediatR `Handle` ≤ 150 líneas |
| `tools/architecture/check-architecture-guardrails.ps1` | CI: capas, patrones frontend, límites TSX, chunk Vite |
| `tools/generators/new-master-module.ps1` | Scaffolding vertical módulo master |

Índice breve: [`scripts/README.md`](../scripts/README.md).

### `run-e2e.ps1` — flags útiles

| Flag | Efecto |
|------|--------|
| `-Database` | Base de pruebas (por defecto `erp_zh_test`; `erp_zh_dev` rechazada) |
| `-SkipDocker` | Postgres ya en `:5435` |
| `-SkipMigrations` | Omite `dotnet ef database update` |
| `-PlaywrightArgs` | P. ej. `"e2e/smoke.spec.ts"` |

Scripts SQL puntuales: [`scripts/db/sql/`](../scripts/db/sql/) (ver [`scripts/db/sql/README.md`](../scripts/db/sql/README.md)). Datos de instalación inmutables: `InstallData/*.sql` — ver [DATABASE.md](./DATABASE.md).

**Regla:** no crear `.ps1` adicionales sin ampliar `scriptsAllowed`, este mapa y [`CONTEXT.md`](../CONTEXT.md).

---

## Stack oficial

Fuente de verdad para agentes: [`docs/architecture/stack.md`](architecture/stack.md) · `.cursor/rules/stack-tools-source-of-truth.mdc`.

### Runtime

| Área | Herramienta | Nota |
|------|-------------|------|
| Backend | .NET SDK **≥ 10.0.204** (`rollForward: latestFeature`) | `global.json` (raíz) — fuente única, aplica a todo subdirectorio. CI: `setup-dotnet` con `global-json-file: global.json` (instala la última 10.0.x). `backend/Dockerfile`: `sdk:10.0.402` exacto; runtime `aspnet:10.0-noble` independiente |
| ORM | EF Core + Npgsql | PostgreSQL |
| CQRS | MediatR | |
| Validación | FluentValidation | Pipeline MediatR |
| Auth | JWT Bearer | |
| Cache | Redis 7 | puerto 6379 |
| Jobs | Hangfire + PostgreSQL | |
| Logs | Serilog | |
| PDF | QuestPDF, RazorLight | |
| Excel | ClosedXML | |
| Códigos QR / barras | QRCoder, ZXing.Net + SkiaSharp | `SkiaSharp.NativeAssets.Linux.NoDependencies` aporta el nativo musl del runtime Docker Alpine |
| HTTP saliente | `Microsoft.Extensions.Http` | `IHttpClientFactory` (cliente SOAP SRI) |
| Redis directo | `StackExchange.Redis` | rate limiting distribuido / health probe |
| API docs | Swashbuckle | `/swagger` |
| BD | PostgreSQL **16** | Docker `postgreszh`, puerto **5435** |
| Contenedores | Docker Compose | raíz repo |

### Frontend

React 19, TypeScript, Vite, React Router v7, React Hook Form + Zod, Axios, Zustand, Recharts, i18next (`es`, `en`, `qu`), ESLint, Vitest, Playwright, `decimal.js` (aritmética decimal de `lib/sanitizers.ts` y cálculos de compras/ventas). Tests unitarios: Vitest + `jsdom` + `@testing-library/react` (con su peer `@testing-library/dom`).

### Tests backend

xUnit, Moq, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing`, EF InMemory cuando aplique, Testcontainers (PostgreSQL). ZXing.Net/SkiaSharp en `ERP.Infrastructure.Tests` solo para decodificar los QR/códigos generados.

### Pins de seguridad transitivos

Referencias directas sin consumo en código, que fijan una versión no vulnerable de una dependencia transitiva (`NU1902`/`NU1903` son error en `backend/Directory.Build.props`). Retirarlas solo cuando el paquete padre resuelva ya una versión segura:

| Paquete | Proyecto(s) | Padre transitivo |
|---------|-------------|------------------|
| `Newtonsoft.Json` | `ERP.API` | `Hangfire.Core` (resuelve 11.0.1) |
| `SSH.NET` | `*.Tests` | `Testcontainers` (resuelve 2024.2.0) |

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

Primer usuario del sistema: el launcher solo consulta el estado de first-run. El propietario
abre manualmente `/setup` en el frontend y completa el formulario existente, que llama a
`POST /api/v1/setup/admin`. El token obligatorio se obtiene personalmente de la consola API;
es temporal (15 minutos por defecto) y de un solo uso. El launcher no recoge datos ni realiza el alta.
No existen credenciales administrativas predefinidas ni tokens configurables via appsettings/env.

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

### FIRST-RUN-SECURITY-01

El launcher solo consulta el estado: no recoge tokens o credenciales, no crea administradores y no verifica login. El propietario completa manualmente el flujo existente con el token temporal obligatorio y de un solo uso de la consola API. La salida API no se copia a `.api-startup.log`. No autoriza a agentes a leer la consola ni completar el alta.
