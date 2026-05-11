# ERP SaaS — ZH Technologies

Monorepo: **backend** (.NET 10, Clean Architecture, PostgreSQL) + **frontend** (Vite, React, TypeScript).

**Base local rápida:** en la raíz del repo, `docker compose up -d` levanta PostgreSQL (`postgreszh`, puerto **5435**, DB `dberpsaas`). Luego copiá `backend/src/ERP.API/appsettings.Development.json.example` → `appsettings.Development.json`, ajustá cadena y JWT, y seguí [`docs/DESARROLLO.md`](docs/DESARROLLO.md) para migraciones y `dotnet run` / `npm run dev`.

## Documentación (orden sugerido)

| Documento | Contenido |
|-----------|-------------|
| [**CONTEXT.md**](./CONTEXT.md) | Índice maestro: mapa de documentación y enlaces |
| [**docs/DESARROLLO.md**](./docs/DESARROLLO.md) | Arranque local, Docker, migraciones, API/Front, `curl`, troubleshooting |
| [**docs/ARCHITECTURE.md**](./docs/ARCHITECTURE.md) | Capas, multi-tenant, estructura por módulo, migraciones, tests; notas para escalar a microservicios |
| [**docs/adr/README.md**](./docs/adr/README.md) | ADR: decisiones de arquitectura (monolito modular, multi-tenant, CI) |
| [**docs/ESTADO-PROYECTO.md**](./docs/ESTADO-PROYECTO.md) | Estado y backlog del proyecto (documento canónico) |
| [**docs/REGISTRO-PROYECTO.md**](./docs/REGISTRO-PROYECTO.md) | Diario de trabajo por fecha |
| [**docs/FRONTEND-PANTALLAS.md**](./docs/FRONTEND-PANTALLAS.md) | Inventario de rutas y pantallas |
| [**docs/developer-reference.html**](./docs/developer-reference.html) | Referencia amplia (abrir en el navegador) |
| [**`.cursor/rules/erp-unified-rules.mdc`**](.cursor/rules/erp-unified-rules.mdc) | Reglas para el agente (validación, UI ZH, i18n, nav) |

**CI (GitHub Actions):** [`.github/workflows/ci.yml`](.github/workflows/ci.yml) — SDK fijado en [`backend/src/global.json`](backend/src/global.json) (alineado a los .NET 10 preinstalados en Ubuntu 24.04), caché NuGet, `dotnet test backend/src/ERP.slnx`; frontend con Node 22, `npm ci`, lint, build y **smoke E2E** (`npm run test:e2e`, Playwright). También **workflow_dispatch** para lanzar a mano. Actualización de Actions: [`.github/dependabot.yml`](.github/dependabot.yml).
