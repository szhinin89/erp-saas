# ERP SaaS — instrucciones Copilot (stub)

**No duplicar guías aquí.** Este archivo existe para detección automática por GitHub Copilot.

## Orden de lectura (canónico)

1. [`AI-RULES/README.md`](../AI-RULES/README.md) — **fuente única** reglas arquitectura/implementación
2. [`CONTEXT.md`](../CONTEXT.md) — índice maestro del monorepo
3. [`AI-RULES/PR-RULES-CATALOG.md`](../AI-RULES/PR-RULES-CATALOG.md) — reglas normativas bloqueantes (PR)
4. [`CLAUDE.md`](../CLAUDE.md) — adaptador Claude → `AI-RULES/`
5. [`docs/DEVELOPMENT.md`](../docs/DEVELOPMENT.md) — arranque, stack, tests
6. [`.cursor/rules/erp-unified-rules.mdc`](../.cursor/rules/erp-unified-rules.mdc) — adaptador Cursor → `AI-RULES/`

## Reglas duras

- Multi-tenant: contexto desde JWT; filtros EF; sin `company_id` del body como autoridad.
- Navegación: sin UUID sensibles en URL; `sessionStorage` con prefijo `erp.saas.*`.
- Estado del producto: **`docs/STATUS.md`** (única fuente de delivery).
- **NO duplicar reglas** — editar solo `AI-RULES/*`.

**Última revisión:** 2026-05-21
