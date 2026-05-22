# Frontend Rules (adaptador)

> **Canónico:** [`AI-RULES/FRONTEND-RULES.md`](AI-RULES/FRONTEND-RULES.md) · Baseline UI: [`docs/FRONTEND_ARCHITECTURE_BASELINE.md`](docs/FRONTEND_ARCHITECTURE_BASELINE.md)

## Resumen

1. Módulos: `frontend/src/modules/{dominio}/` con `api/`, `schemas/`, `hooks/`, `pages/`
2. ZH Form + Zod + react-hook-form (4 capas con backend)
3. i18n: es, en, qu
4. Sin IDs sensibles en URL — `sessionStorage` `erp.saas.*`
5. Sin diálogos nativos — modales ZH
6. Tabs: Datos → extras → `{modulo}.tabList`

## Baseline y QA

- [`docs/FRONTEND_ARCHITECTURE_BASELINE.md`](docs/FRONTEND_ARCHITECTURE_BASELINE.md)
- [`docs/frontend-layout-conventions.md`](docs/frontend-layout-conventions.md)
- [`docs/FRONTEND_QA_CHECKLIST.md`](docs/FRONTEND_QA_CHECKLIST.md)

CI: `npm run lint`, `npm run build`, Playwright — ver [`AI-RULES/ENFORCEMENT.md`](AI-RULES/ENFORCEMENT.md).
