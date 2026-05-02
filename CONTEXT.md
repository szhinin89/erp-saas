# CONTEXT.md — ZH Technologies ERP

> **Índice maestro para agentes y desarrolladores.** Revisión del índice: **2026-05-02**.  
> Lee este archivo primero; luego abre solo los enlaces que correspondan a tu tarea.

## Árbol real del monorepo `erp-saas`

```
erp-saas/
├── docker-compose.yml                     ← PostgreSQL local (postgreszh, puerto 5435)
├── .cursor/rules/erp-unified-rules.mdc   ← reglas de producto (arquitectura, validación 4 capas, ZH Form, nav, i18n Kichwa)
├── CONTEXT.md                             ← este índice
├── docs/
│   ├── adr/                               ← decisiones de arquitectura (ADR); índice en README.md
│   ├── DESARROLLO.md                      ← arranque local, Docker, migraciones, curl, troubleshooting
│   ├── ARCHITECTURE.md                    ← capas, multi-tenant, carpetas por módulo, migraciones
│   ├── developer-reference.html           ← referencia larga (abrir en navegador)
│   ├── FRONTEND-PANTALLAS.md              ← inventario de rutas y pantallas
│   └── STATUS-2026-05-ERP.md              ← estado y checklist reciente (mayo 2026)
├── backend/src/
│   ├── global.json            ← versión mínima del SDK .NET (CI + equipo)
│   ├── ERP.slnx
│   ├── ERP.API/
│   ├── ERP.Application/
│   ├── ERP.Domain/
│   └── ERP.Infrastructure/
├── scripts/                               ← INEC geografía, dev-up.ps1 (Docker DB)
└── frontend/                              ← Vite + React + i18n (es, en, qu)
```

## Reglas de implementación (obligatorio)

**`.cursor/rules/erp-unified-rules.mdc`** — incluye jerarquía de lectura, límites de capas, validación en cuatro capas, sistema de formularios ZH, copy de menú/pestañas, locale `qu` (Kichwa de Cañar).

## Arquitectura detallada

**`docs/ARCHITECTURE.md`** — diagrama de capas, estructura `ERP.Domain/Modules/{Modulo}`, query filters, `Result`, auth, CORS, tabla de módulos.

**`docs/adr/`** — decisiones estables (monolito modular, multi-tenant, CI). Ver [`docs/adr/README.md`](docs/adr/README.md).

## Operación local, tests y primer uso

**`docs/DESARROLLO.md`** — prerequisitos, PostgreSQL en Docker (`postgreszh`), `dotnet ef database update`, `dotnet run`, `npm run dev`, ejemplos `curl`, comandos frecuentes, CI en GitHub, **política de ramas** (`main` hoy; `development`, `release/*`, `hotfix/*` después), tabla de endpoints de referencia, solución de problemas (DLL bloqueada, CORS, 401).

## Estado reciente del código

**`docs/STATUS-2026-05-ERP.md`** — qué está verificado (tests, sucursales, clientes, proxy Vite), pendientes menores, scripts INEC.

## Frontend: pantallas y rutas

**`docs/FRONTEND-PANTALLAS.md`** — catálogo de rutas; al añadir pantalla actualizar `frontend/src/App.tsx` y `frontend/src/nav/navConfig.ts`.

## Referencia visual y plantilla ZH

- **HTML plantilla:** `docs/zh-form-template/zh_erp_component_library.html`
- **Notas de mantenimiento:** `docs/zh-form-template/README.md`

## Tercer idioma (UI)

Kichwa de Cañar, Ecuador — locale **`qu`**, archivo **`frontend/src/i18n/locales/qu.json`**. Detalle en **`erp-unified-rules.mdc`** (sección Kichwa).
