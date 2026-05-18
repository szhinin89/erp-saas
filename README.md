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

## Documentación

| Documento | Contenido |
|-----------|-----------|
| [**PROJECT.md**](./PROJECT.md) | Objetivo, modelo de negocio, segmentos y alcance del producto |
| [**CLAUDE.md**](./CLAUDE.md) | Reglas y convenciones del proyecto (leer siempre primero) |
| [**docs/ARCHITECTURE.md**](./docs/ARCHITECTURE.md) | Arquitectura completa — capas, multi-tenant, auth, SuperAdmin, SaaS, ADRs |
| [**docs/STATUS.md**](./docs/STATUS.md) | Estado de desarrollo — qué está hecho, pendientes MVP, tests |
| [**PROGRESS.html**](./PROGRESS.html) | Checklist de avance por sección (% y próximas acciones) |
| [**docs/FEATURES.md**](./docs/FEATURES.md) | Funcionalidades — pantallas, módulos, endpoints, permisos |
| [**CONTEXT.md**](./CONTEXT.md) | Índice maestro y árbol del monorepo |

> Al editar `STATUS.md` o `PROGRESS.html`, actualizar también `PROJECT.md`, `FEATURES.md`, `CONTEXT.md` y este README (regla `.cursor/rules/docs-progress-status-sync.mdc`).

## CI

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) — SDK fijado en [`backend/src/global.json`](backend/src/global.json); `dotnet test`; frontend Node 22, lint, build, Playwright E2E. Dependabot en [`.github/dependabot.yml`](.github/dependabot.yml).
