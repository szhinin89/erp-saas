# CONTEXT.md — ZH Technologies ERP

Índice maestro. Lee primero este archivo; luego abre **solo** el documento de tu tarea.

---

## Mapa de documentación

| Qué necesito | Archivo | Contenido |
|--------------|---------|-----------|
| **Entrada GitHub / visión producto** | [`README.md`](./README.md) (raíz) | Resumen negocio, arranque, enlaces |
| **Reglas de código (agentes)** | [`CLAUDE.md`](./CLAUDE.md) (raíz) | Convenciones implementación |
| **Estado y delivery** | [`docs/STATUS.md`](./docs/STATUS.md) | Única fuente de avance MVP |
| **Prioridades** | [`docs/ROADMAP.md`](./docs/ROADMAP.md) | Fases pendientes |
| **Cómo está construido** | [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) | Capas, scopes, platform vs ERP, API |
| **Cómo desarrollar** | [`docs/DEVELOPMENT.md`](./docs/DEVELOPMENT.md) | Arranque, scripts, stack, tests, reglas |
| **Login, JWT, seguridad** | [`docs/IDENTITY.md`](./docs/IDENTITY.md) | IAM, auth backend/frontend |
| **Planes, billing, empresas** | [`docs/SAAS-COMMERCIAL.md`](./docs/SAAS-COMMERCIAL.md) | Límites, billing SaaS, companies |
| **PostgreSQL, EF, RLS** | [`docs/DATABASE.md`](./docs/DATABASE.md) | Migraciones, tablas, RLS |

> No crear `.md` de producto fuera de esta lista (salvo stubs mínimos en `.github/` o README operativos en `Migrations/` / `InstallData/`).

---

## Árbol `docs/` (oficial — 7 archivos)

```
docs/
├── STATUS.md
├── ROADMAP.md
├── ARCHITECTURE.md
├── DEVELOPMENT.md
├── IDENTITY.md
├── SAAS-COMMERCIAL.md
└── DATABASE.md
```

Referencia SRI (PDF): `docs/FICHA TECNICA COMPROBANTES ELECTRONICOS ESQUEMA OFFLINE Versio232.pdf`

---

## Arranque local

Atajo: **`.\scripts\dev-restart.ps1`** · SuperAdmin first-run: **`.\Crear-SuperAdmin.ps1`**

Manual completo: [`docs/DEVELOPMENT.md`](./docs/DEVELOPMENT.md#arranque-local).

---

## Scripts PowerShell (7 canónicos)

| Script | Rol |
|--------|-----|
| [`Crear-SuperAdmin.ps1`](./Crear-SuperAdmin.ps1) | SuperAdmin first-run |
| [`scripts/dev-restart.ps1`](./scripts/dev-restart.ps1) | Dev: Docker + EF + API + Vite |
| [`scripts/run-e2e.ps1`](./scripts/run-e2e.ps1) | Playwright E2E |
| [`scripts/verify-stack-allowlist.ps1`](./scripts/verify-stack-allowlist.ps1) | CI: stack + scripts |
| [`scripts/check-identity-guardrails.ps1`](./scripts/check-identity-guardrails.ps1) | CI: auth legacy |
| [`scripts/new-master-module.ps1`](./scripts/new-master-module.ps1) | Scaffolding módulo |
| [`scripts/import_inec_ecuador_geography.ps1`](./scripts/import_inec_ecuador_geography.ps1) | SQL geografía INEC |

Detalle y flags: [`docs/DEVELOPMENT.md`](./docs/DEVELOPMENT.md#scripts-powershell-canónicos) · Índice carpeta: [`scripts/README.md`](./scripts/README.md).

No añadir `.ps1` sin actualizar `scripts/stack-allowlist.json` (`scriptsAllowed`).

---

## Reglas para agentes

1. **`CLAUDE.md`** + **`docs/DEVELOPMENT.md`** antes de implementar.
2. Arquitectura / scopes → **`docs/ARCHITECTURE.md`**.
3. Auth / permisos → **`docs/IDENTITY.md`**.
4. Planes / billing → **`docs/SAAS-COMMERCIAL.md`**.
5. Schema / EF → **`docs/DATABASE.md`**.
6. Estado → **`docs/STATUS.md`**; prioridades → **`docs/ROADMAP.md`**.
7. Stack nuevo → **`docs/DEVELOPMENT.md#stack-oficial`** + `scripts/stack-allowlist.json`.
8. Scripts `.ps1` → solo los 7 canónicos; mapa en **`scripts/README.md`**.

Copilot / IDE: [`copilot-instructions.md`](./copilot-instructions.md) → [`.github/INSTRUCCIONES-COPILOT.md`](./.github/INSTRUCCIONES-COPILOT.md).

---

## Otros archivos (no duplicar)

| Tipo | Dónde | Nota |
|------|--------|------|
| Reglas Cursor | `.cursor/rules/*.mdc` | Fuente agente; no copiar a `docs/` |
| Stub Copilot | `copilot-instructions.md`, `.cursorrules`, `.github/instructions/` | Punteros mínimos |
| SQL auxiliar | `scripts/sql/` | Ver [`scripts/sql/README.md`](./scripts/sql/README.md) |
| InstallData | `backend/.../Seeding/InstallData/` | Scripts inmutables de arranque |
| Checklist MVP | `PROGRESS.html` | Sincronizar con `docs/STATUS.md` |
| Spec SRI | `docs/*.pdf` | Referencia normativa |
| Artefactos locales | `_verify_build_out/`, `*.log`, `backend/scripts/` | `.gitignore`; no versionar |

No crear carpetas paralelas (`database/`, docs sueltos en `src/`, caches `.lscache`).

---
