# Frontend Rules

> Canónico: [`docs/FRONTEND_ARCHITECTURE_BASELINE.md`](docs/FRONTEND_ARCHITECTURE_BASELINE.md) · [`CLAUDE.md`](CLAUDE.md) · [`docs/ARCHITECTURE-RULES.md`](docs/ARCHITECTURE-RULES.md) (sección Frontend)

## Obligatorio

1. **Módulos:** `frontend/src/modules/{dominio}/` con `api/`, `schemas/`, `hooks/`, `pages/`.
2. **Formularios:** ZH Form System + Zod + react-hook-form (validación 4 capas con backend).
3. **i18n:** claves nuevas en `es.json`, `en.json`, `qu.json`.
4. **Sin IDs sensibles en URL** — `sessionStorage` prefijo `erp.saas.*`.
5. **Sin diálogos nativos** — modales ZH.
6. **Tabs entidad:** Datos → extras → `{modulo}.tabList`.
7. **Auth refresh:** solo `authRefreshManager` (Web Locks + BroadcastChannel).

## Estructura

```
frontend/
├── src/
├── e2e/           # Playwright
├── docs/          # Notas frontend
├── scripts/       # Scripts locales (vacío — usar scripts/ raíz)
└── public/
```

## Baseline y QA

- Arquitectura sellada: [`docs/FRONTEND_ARCHITECTURE_BASELINE.md`](docs/FRONTEND_ARCHITECTURE_BASELINE.md)
- Convenciones layout: [`docs/frontend-layout-conventions.md`](docs/frontend-layout-conventions.md)
- Inventario páginas: [`frontend/src/templates/PAGE-AUDIT.md`](frontend/src/templates/PAGE-AUDIT.md)
- Checklist QA: [`docs/FRONTEND_QA_CHECKLIST.md`](docs/FRONTEND_QA_CHECKLIST.md)
- Changelog: [`frontend/CHANGELOG.md`](frontend/CHANGELOG.md)

## CI

`npm run lint`, `npm run build`, Playwright smoke en `.github/workflows/frontend-ci.yml`.
