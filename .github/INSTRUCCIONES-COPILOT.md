# ERP SaaS — instrucciones Copilot (stub)

**No duplicar guías aquí.** Este archivo existe para detección automática por GitHub Copilot.

## Orden de lectura (canónico)

1. [`docs/architecture/README.md`](../docs/architecture/README.md) — **fuente única** reglas arquitectura/implementación
2. [`CONTEXT.md`](../CONTEXT.md) — índice maestro del monorepo
3. [`docs/architecture/pr-rules-catalog.md`](../docs/architecture/pr-rules-catalog.md) — reglas normativas bloqueantes (PR)
4. [`CLAUDE.md`](../CLAUDE.md), [`backend/CLAUDE.md`](../backend/CLAUDE.md), [`frontend/CLAUDE.md`](../frontend/CLAUDE.md) — adaptadores → `docs/architecture/`
5. [`docs/DEVELOPMENT.md`](../docs/DEVELOPMENT.md) — arranque, stack, tests
6. [`.cursor/rules/erp-unified-rules.mdc`](../.cursor/rules/erp-unified-rules.mdc) — adaptador Cursor → `docs/architecture/`

## Reglas duras

- Multi-tenant: contexto desde JWT; filtros EF; sin `company_id` del body como autoridad.
- Navegación: sin UUID sensibles en URL; `sessionStorage` con prefijo `erp.saas.*`.
- Estado del producto: **`STATUS.md`** (única fuente de delivery).
- **NO duplicar reglas** — editar solo `docs/architecture/*`.

**Última revisión:** 2026-08-07 (Bloque 16B)
