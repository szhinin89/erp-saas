# ERP SaaS — ZH Technologies

Monorepo: **backend** (.NET 10, Clean Architecture, PostgreSQL) + **frontend** (Vite, React, TypeScript).

## Arranque rápido

```powershell
docker compose up -d   # PostgreSQL (5435) + Redis (6379)
cd backend/src && dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
dotnet run --project ERP.API --launch-profile http   # http://localhost:5003  /swagger
cd frontend && npm run dev                            # http://localhost:5173
```

> Copiar `backend/src/ERP.API/appsettings.Development.json.example` → `appsettings.Development.json` y ajustar cadena + JWT.

## Documentación oficial (`docs/`)

| Documento | Contenido |
|-----------|-----------|
| [**docs/STATUS.md**](./docs/STATUS.md) | **Estado actual** — única fuente de verdad de delivery |
| [**docs/ARCHITECTURE.md**](./docs/ARCHITECTURE.md) | Arquitectura oficial (Clean + CQRS, scopes) |
| [**docs/ROADMAP.md**](./docs/ROADMAP.md) | Prioridades y fases pendientes |
| [**docs/DEVELOPMENT-RULES.md**](./docs/DEVELOPMENT-RULES.md) | Reglas de desarrollo y verificación |
| [**docs/DATABASE/**](./docs/DATABASE/) | Migraciones, RLS, tablas |
| [**PROJECT.md**](./PROJECT.md) | Modelo de negocio y alcance producto |
| [**CLAUDE.md**](./CLAUDE.md) | Convenciones para agentes |
| [**CONTEXT.md**](./CONTEXT.md) | Índice del monorepo |

> Al cambiar arquitectura o estado de entrega, actualizar `docs/STATUS.md` y `docs/ROADMAP.md` primero.

## CI

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) — SDK fijado en [`backend/src/global.json`](backend/src/global.json); `dotnet test`; frontend Node 22, lint, build, Playwright E2E. Dependabot en [`.github/dependabot.yml`](.github/dependabot.yml).
