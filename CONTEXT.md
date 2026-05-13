# CONTEXT.md — ZH Technologies ERP

> **Índice maestro para agentes y desarrolladores.** Revisión del índice: **2026-05-09**.  
> Lee este archivo primero; luego abre solo los enlaces que correspondan a tu tarea.
>
> **Estado actual del proyecto → [`docs/ESTADO-PROYECTO.md`](docs/ESTADO-PROYECTO.md)**  
> (qué está hecho, qué falta, cómo retomar, comandos frecuentes)

## Mapa de documentación (una fuente por tema)

| Tema | Documento que manda | Qué cubre |
|------|----------------------|-----------|
| Índice y enlaces | **`CONTEXT.md`** (este archivo) | Dónde buscar según la tarea |
| Estado, backlog, retomar | **`docs/ESTADO-PROYECTO.md`** | Hecho / pendiente / comandos, **backlog de refactor** y **plan de sprints** modular |
| Diario de sesiones y decisiones | **`docs/REGISTRO-PROYECTO.md`** | Entradas por fecha; notas de continuación |
| Arranque, Docker, EF, curl, CI, checklist API (histórico) | **`docs/DESARROLLO.md`** | Operación local, troubleshooting y homologación de contratos HTTP |
| Capas, multi-tenant, módulos | **`docs/ARCHITECTURE.md`** | Arquitectura de producto |
| Decisiones estables | **`docs/adr/README.md`** | ADR |
| Rutas UI | **`docs/FRONTEND-PANTALLAS.md`** | Inventario de pantallas |
| SaaS planes y menú | **`docs/SAAS-PLAN-TENANT-FLOW.md`**, **`docs/COMPANIES-PLAN-MENU-ADMIN.md`** | Flujo comercial y admin de menú |
| Histórico de correcciones arquitectónicas | **`docs/HISTORIAL-ARQUITECTURA-RIESGOS.md`** | Informes antes en la raíz (riesgos) |
| Reglas del agente (Cursor) | **`.cursor/rules/erp-unified-rules.mdc`** y mapa **`.cursor/rules/rules-consolidated-map.mdc`** | Implementación obligatoria |
| Guía larga Copilot / resumen técnico | **`.github/INSTRUCCIONES-COPILOT.md`** | Detalle en español (evitar duplicar en otros `.md` de raíz) |

**Redirecciones:** `docs/STATUS-2026-05-ERP.md` y `docs/CONTINUAR-sesion.md` solo enlazan al documento canónico correspondiente (no duplicar contenido).

## Árbol real del monorepo `erp-saas`

```
erp-saas/
├── docker-compose.yml                     ← PostgreSQL local (postgreszh, puerto 5435)
├── .cursor/rules/erp-unified-rules.mdc     ← reglas de producto (arquitectura, validación 4 capas, ZH Form, nav, i18n Kichwa)
├── .cursor/rules/saas-navigation-no-sensitive-url.mdc  ← tenant UUID y contexto sensible: sessionStorage, no query en URL
├── CONTEXT.md                             ← este índice
├── docs/
│   ├── adr/                               ← decisiones de arquitectura (ADR); índice en README.md
│   ├── DESARROLLO.md                      ← arranque local, Docker, migraciones, curl, troubleshooting
│   ├── ARCHITECTURE.md                    ← capas, multi-tenant, carpetas por módulo, migraciones
│   ├── ESTADO-PROYECTO.md                 ← ★ estado y backlog (actualizar aquí)
│   ├── REGISTRO-PROYECTO.md               ← log de trabajo por fecha
│   ├── HISTORIAL-ARQUITECTURA-RIESGOS.md  ← histórico riesgos/correcciones (solo lectura)
│   ├── developer-reference.html           ← referencia larga (abrir en navegador)
│   └── FRONTEND-PANTALLAS.md              ← inventario de rutas y pantallas
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

**`.cursor/rules/erp-unified-rules.mdc`** — incluye jerarquía de lectura, límites de capas, validación en cuatro capas, sistema de formularios ZH, copy de menú/pestañas, locale `qu` (Kichwa de Cañar), y enlace a navegación SaaS sin IDs en URL.

**`docs/SAAS-PLAN-TENANT-FLOW.md`** — flujo comercial: empresa → plan comercial → features del plan (módulos, formularios) → restricción opcional por módulos de producto en el tenant; alineado con la pantalla **Empresas → Plan y módulos**.

**`docs/COMPANIES-PLAN-MENU-ADMIN.md`** — pantalla **Empresas → Plan ↔ menú**: árbol del menú + acciones incluir/quitar para armar qué definiciones SaaS pertenecen a un plan comercial (y bloque de definiciones sin enlace al menú).

**`.cursor/rules/saas-navigation-no-sensitive-url.mdc`** — obligatorio al pasar contexto de tenant entre rutas: `sessionStorage` (`erp.saas.*`), helpers en `frontend/src/navigation/companiesTenantDetailNav.ts`; no `?tenantId=` / `?data=` / `?subscription=` con UUID en código nuevo (migración si hay legacy).

## Arquitectura detallada

**`docs/ARCHITECTURE.md`** — diagrama de capas, estructura `ERP.Domain/Modules/{Modulo}`, query filters, `Result`, auth, CORS, tabla de módulos.

**`docs/adr/`** — decisiones estables (monolito modular, multi-tenant, CI). Ver [`docs/adr/README.md`](docs/adr/README.md).

## Operación local, tests y primer uso

**`docs/DESARROLLO.md`** — prerequisitos, PostgreSQL en Docker (`postgreszh`), `dotnet ef database update`, `dotnet run`, `npm run dev`, ejemplos `curl`, comandos frecuentes, CI en GitHub, **política de ramas** (`main` hoy; `development`, `release/*`, `hotfix/*` después), tabla de endpoints de referencia, solución de problemas (DLL bloqueada, CORS, 401).

## Frontend: pantallas y rutas

**`docs/FRONTEND-PANTALLAS.md`** — catálogo de rutas; al añadir pantalla actualizar `frontend/src/App.tsx` y `frontend/src/nav/navConfig.ts`.

**Menú en BD vs SuperAdmin:** el menú por tenant (`ui_nav_*`) no debe incluir rutas de plataforma (`/superadmin/*`, `/companies`); esas van solo en **`navConfig.ts`** (`getSuperAdminPanelNavExtras`) para no mezclar módulos de cliente con el panel global. Migración **`UiNavRemovePlatformAdminFromMenu`** desactiva filas heredadas.

## Referencia visual y plantilla ZH

Copia canónica **solo** dentro de `erp-saas` (no depender de plantillas en otras carpetas del disco).

- **HTML plantilla:** `docs/zh-form-template/zh_erp_component_library.html`
- **Notas de mantenimiento:** `docs/zh-form-template/README.md`

## Tercer idioma (UI)

Kichwa de Cañar, Ecuador — locale **`qu`**, archivo **`frontend/src/i18n/locales/qu.json`**. Detalle en **`erp-unified-rules.mdc`** (sección Kichwa).
