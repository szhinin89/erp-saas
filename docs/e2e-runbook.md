# E2E Runbook (Playwright)

Guía para ejecutar pruebas end-to-end de forma reproducible en local y CI manual.

## Requisitos

- Docker Desktop (Postgres en `localhost:5435`, Redis en `6379`)
- .NET SDK (ver `backend/src/global.json`)
- Node.js 22+
- Playwright Chromium: `cd frontend && npx playwright install chromium`

## Script único (recomendado)

Desde la raíz del repo:

```powershell
pwsh -File scripts/run-e2e.ps1
```

El script:

1. `docker compose up -d`
2. `dotnet ef database update` (Infrastructure + API startup)
3. Levanta **ERP.API** en `http://localhost:5003`
4. Espera **`GET /health/live`** (200)
5. `npm ci`, `npm run build`, `npx playwright test` (preview en `:4173` vía `playwright.config.ts`)

### Variantes

```powershell
# Solo smoke (sin API enterprise)
pwsh -File scripts/run-e2e.ps1 -PlaywrightArgs "e2e/smoke.spec.ts"

# Postgres ya levantado
pwsh -File scripts/run-e2e.ps1 -SkipDocker

# Sin re-aplicar migraciones
pwsh -File scripts/run-e2e.ps1 -SkipMigrations
```

## Credenciales demo (seed Development)

Con `Development:SeedDemoTenant: true` en `backend/src/ERP.API/appsettings.Development.json`:

| Variable       | Default           |
|----------------|-------------------|
| `E2E_EMAIL`    | `admin@erp.com`   |
| `E2E_PASSWORD` | `Admin123!`       |
| `E2E_API_URL`  | `http://localhost:5003` |

Los specs `e2e/enterprise-*.spec.ts` hacen **skip** automático si la API no responde en `/health/live`.

## Manual (paso a paso)

```powershell
# 1. Infra
docker compose up -d

# 2. Migraciones
cd backend/src
dotnet ef database update --project ERP.Infrastructure/ERP.Infrastructure.csproj --startup-project ERP.API/ERP.API.csproj

# 3. API
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5003"
dotnet run --project ERP.API/ERP.API.csproj

# 4. Frontend + Playwright (otra terminal)
cd frontend
npm ci
npm run build
$env:E2E_API_URL = "http://localhost:5003"
npm run test:e2e
```

Verificar salud:

```powershell
Invoke-WebRequest http://localhost:5003/health/live -UseBasicParsing
Invoke-WebRequest http://localhost:5003/health/ready -UseBasicParsing
```

## CI manual (GitHub Actions)

Workflow opcional: `.github/workflows/e2e-manual.yml` (`workflow_dispatch`).

Dispara E2E completo con Postgres en servicio, migraciones, API en background y Playwright.

## Troubleshooting

| Síntoma | Acción |
|---------|--------|
| `login failed: 401` | Confirmar seed demo; revisar `appsettings.Development.json` y migraciones aplicadas |
| `API no disponible` en enterprise tests | API no en `:5003` o `/health/live` falla — usar `run-e2e.ps1` |
| Puerto 5435 ocupado | `docker compose ps`; otro Postgres local |
| Playwright timeout en preview | `npm run build` antes; puerto `4173` libre |
| Migraciones fallan | `docker compose logs postgres`; credenciales en appsettings vs `docker-compose.yml` |

## Suites

| Archivo | Requiere API |
|---------|--------------|
| `e2e/smoke.spec.ts` | No (solo UI login) |
| `e2e/enterprise-auth.spec.ts` | Sí |
| `e2e/enterprise-sales-company.spec.ts` | Sí |
