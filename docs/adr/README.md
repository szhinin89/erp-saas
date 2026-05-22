# Architecture Decision Records (ADR)

Documentan el **por qué** de decisiones arquitectónicas. Las reglas ejecutables viven en [`AI-RULES/`](../../AI-RULES/README.md); los checks en [`tools/architecture/`](../../tools/architecture/README.md).

| ADR | Título | Status |
|-----|--------|--------|
| [ADR-001](./ADR-001-modular-monolith.md) | Modular monolith | Accepted |
| [ADR-002](./ADR-002-no-erp-shared.md) | Sin proyecto ERP.Shared | Accepted |
| [ADR-003](./ADR-003-pages-wrapper-only.md) | Pages wrapper only | Accepted |
| [ADR-004](./ADR-004-clean-architecture-enforcement.md) | Clean Architecture + enforcement | Accepted |
| [ADR-005](./ADR-005-multi-tenant-query-filters.md) | Multi-tenant query filters | Accepted |
| [ADR-006](./ADR-006-multi-agent-governance.md) | Multi-agent governance | Accepted |

**Formato:** Status · Context · Decision · Consequences · Alternatives Considered

**Al proponer cambio arquitectónico:** crear ADR nuevo o actualizar existente antes de ampliar `AI-RULES/*` o checks CI.
