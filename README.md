# ERP SaaS — ZH Technologies

Monorepo: **backend** (.NET 10, Clean Architecture, PostgreSQL) + **frontend** (Vite, React, TypeScript).

## Producto

ERP **SaaS multi-tenant** para empresas ecuatorianas: facturación electrónica **SRI**, inventario y compras, contabilidad integrada y administración multi-empresa desde un panel **SuperAdmin**. ZH Technologies opera la instancia; cada cliente tiene plan, módulos y datos aislados.

**Segmentos:** PYME (ventas/catálogo) → PYME con inventario → empresa mediana (contabilidad, sucursales) → operador (SuperAdmin).

**Diferenciadores:** integración SRI nativa (XML, firma P12, RIDE), i18n **es / en / Kichwa de Cañar (`qu`)**, permisos granulares y menú por plan.

Estado de entrega y MVP: **[`docs/STATUS.md`](./docs/STATUS.md)** · Prioridades: **[`docs/ROADMAP.md`](./docs/ROADMAP.md)** · Checklist: **`PROGRESS.html`**.

## Arranque rápido

```powershell
docker compose up -d   # PostgreSQL (5435) + Redis (6379)
# Atajo: .\scripts\dev-restart.ps1
cd backend/src && dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
dotnet run --project ERP.API --launch-profile http   # http://localhost:5003  /swagger
cd frontend && npm run dev                            # http://localhost:5173
```

Copiar `backend/src/ERP.API/appsettings.Development.json.example` → `appsettings.Development.json`. Detalle, scripts y tests: **[`docs/DEVELOPMENT.md`](./docs/DEVELOPMENT.md)**.

## Documentación

**Índice maestro:** **[`CONTEXT.md`](./CONTEXT.md)** — mapa de los 7 archivos en `docs/` + reglas para agentes (`CLAUDE.md`, `.cursor/rules/`).

| Documento | Contenido |
|-----------|-----------|
| [`docs/STATUS.md`](./docs/STATUS.md) | Estado de delivery (fuente de verdad) |
| [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) | Arquitectura, scopes, platform vs ERP |
| [`docs/DEVELOPMENT.md`](./docs/DEVELOPMENT.md) | Stack, scripts, tests, reglas dev |
| [`docs/IDENTITY.md`](./docs/IDENTITY.md) | Auth, JWT, seguridad |
| [`docs/SAAS-COMMERCIAL.md`](./docs/SAAS-COMMERCIAL.md) | Planes, billing, empresas |
| [`docs/DATABASE.md`](./docs/DATABASE.md) | PostgreSQL, migraciones, RLS |
| [`docs/ROADMAP.md`](./docs/ROADMAP.md) | Prioridades pendientes |

Al cambiar arquitectura o avance, actualizar **`docs/STATUS.md`** y **`docs/ROADMAP.md`** (ver `.cursor/rules/docs-progress-status-sync.mdc`).

## CI

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) — SDK en [`backend/src/global.json`](backend/src/global.json); `dotnet test`; frontend Node 22, lint, build, Playwright E2E. Dependabot: [`.github/dependabot.yml`](.github/dependabot.yml).
