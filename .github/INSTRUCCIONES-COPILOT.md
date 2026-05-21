# ERP SaaS — instrucciones Copilot (stub)

**No duplicar guías aquí.** Este archivo existe para detección automática por GitHub Copilot.

## Orden de lectura (canónico)

1. [`CONTEXT.md`](../CONTEXT.md) — índice maestro del monorepo
2. [`CLAUDE.md`](../CLAUDE.md) — reglas de implementación para agentes
3. [`docs/DEVELOPMENT-RULES.md`](../docs/DEVELOPMENT-RULES.md) — arranque, tests, convenciones
4. [`docs/HERRAMIENTAS-ERP-SAAS.md`](../docs/HERRAMIENTAS-ERP-SAAS.md) — stack permitido
5. [`.cursor/rules/erp-unified-rules.mdc`](../.cursor/rules/erp-unified-rules.mdc) — validación 4 capas, ZH Form, i18n `qu`, navegación SaaS

## Reglas duras

- Multi-tenant: contexto desde JWT; filtros EF; sin `company_id` del body como autoridad.
- Navegación: sin UUID sensibles en URL; `sessionStorage` con prefijo `erp.saas.*`.
- Estado del producto: **`docs/STATUS.md`** (única fuente de delivery).
- No crear documentos fuera de `docs/` salvo stubs mínimos en `.github/` o `.cursor/`.

**Última revisión:** 2026-05-21
