# CONTEXT.md — ZH Technologies ERP

Índice maestro. Lee primero este archivo; luego abre solo el enlace que corresponde a tu tarea.

---

## Documentación (6 archivos, sin solapamiento)

| Qué necesito saber | Archivo |
|--------------------|---------|
| **Objetivo y visión** del producto — qué es, a quién va dirigido, modelo de negocio | **`PROJECT.md`** (raíz) |
| **Reglas y convenciones** de código, patrones obligatorios, CSS, i18n, SaaS | **`CLAUDE.md`** (raíz) |
| **Arquitectura** del sistema — capas, stack, multi-tenant, auth, SuperAdmin, ADRs | **`docs/ARCHITECTURE.md`** |
| **Estado de desarrollo** — qué está hecho, pendientes MVP, flujos de estado, tests | **`docs/STATUS.md`** |
| **Checklist de avance** — ítems por sección, % y próximas acciones | **`PROGRESS.html`** (raíz) |
| **Funcionalidades** — todas las pantallas, módulos backend, endpoints, permisos | **`docs/FEATURES.md`** |

> Al actualizar **`PROGRESS.html`** o **`docs/STATUS.md`**, sincronizar también `PROJECT.md`, `FEATURES.md`, `CONTEXT.md` y `README.md` según `.cursor/rules/docs-progress-status-sync.mdc`.

---

## Árbol real del monorepo

```
erp-saas/
├── CLAUDE.md                          ← reglas del proyecto (leer siempre)
├── CONTEXT.md                         ← este índice
├── docker-compose.yml                 ← PostgreSQL (5435) + Redis (6379)
├── PROGRESS.html                      ← checklist de avance (detalle por ítem)
├── docs/
│   ├── ARCHITECTURE.md               ← arquitectura completa
│   ├── STATUS.md                     ← estado de desarrollo consolidado
│   ├── FEATURES.md                   ← funcionalidades y endpoints
│   └── adr/                          ← decisiones de arquitectura (referencia histórica)
├── .cursor/rules/                     ← reglas para Cursor IDE
│   ├── erp-unified-rules.mdc         ← reglas transversales
│   └── ...
├── backend/src/
│   ├── global.json                   ← versión mínima SDK .NET
│   ├── ERP.API/
│   ├── ERP.Application/
│   ├── ERP.Domain/
│   └── ERP.Infrastructure/
├── frontend/                          ← Vite + React + TypeScript
│   └── src/
│       ├── styles/                   ← design-tokens.css, zh-ui.css, page-template.css
│       ├── components/zh/            ← ZHForm.tsx, ZHPageNotice.tsx
│       ├── modules/                  ← módulos por dominio
│       └── i18n/locales/             ← es.json, en.json, qu.json
└── scripts/                          ← dev-up.ps1, create-superadmin.ps1, SQL
```

---

## Arranque rápido

```powershell
docker compose up -d
cd backend/src
dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
dotnet run --project ERP.API --launch-profile http   # http://localhost:5003
cd ../../frontend && npm run dev                      # http://localhost:5173
```

---

## Reglas para agentes / IA

1. Leer **`CLAUDE.md`** antes de implementar cualquier cosa.
2. Para contexto arquitectónico → **`docs/ARCHITECTURE.md`**.
3. Para saber qué está hecho y qué falta → **`docs/STATUS.md`** (+ detalle en **`PROGRESS.html`**).
4. Para rutas, endpoints o permisos → **`docs/FEATURES.md`**.
5. Si actualizas avance → regla **`docs-progress-status-sync.mdc`** (sincronizar todos los docs involucrados).
6. Nunca generar código sin haber verificado si el archivo ya existe.
