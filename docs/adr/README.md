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
| [ADR-007](./ADR-007-domain-events-foundation.md) | Domain Events Foundation | Accepted |
| [ADR-008](./ADR-008-outbox-pattern-foundation.md) | Outbox Pattern Foundation | Accepted |
| [ADR-009](./ADR-009-ai-layer-separation.md) | AI Layer Separation | Accepted |
| [ADR-010](./ADR-010-event-versioning.md) | Event Versioning Strategy | Accepted |
| [ADR-011](./ADR-011-outbox-retention-strategy.md) | Outbox Retention Strategy | Accepted (impl. diferida) |
| [ADR-012](./ADR-012-ai-read-model-strategy.md) | AI Read Model Strategy | Accepted (impl. diferida) |

**Formato:** Status · Context · Decision · Consequences · Alternatives Considered

**Al proponer cambio arquitectónico:** crear ADR nuevo o actualizar existente antes de ampliar `AI-RULES/*` o checks CI.
