# docs/decisions — Decisiones históricas (ADRs)

**`docs/decisions/`** contiene las decisiones arquitectónicas históricas del proyecto (ADRs) y, en [`archive-ai-rules/`](./archive-ai-rules/README.md), el snapshot archivado de las reglas `AI-RULES/` previas a la reorganización SSOT (Bloque 16B). Documentan el **por qué** de decisiones arquitectónicas — las reglas ejecutables vigentes viven en [`docs/architecture/`](../architecture/README.md); los checks en [`tools/architecture/`](../../tools/architecture/README.md).

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
| [ADR-013](./ADR-013-cqrs-mediatr.md) | CQRS con MediatR y FluentValidation | Accepted |
| [ADR-014](./ADR-014-refresh-token-rotation.md) | Refresh token rotation enterprise | Accepted |
| [ADR-015](./ADR-015-postgresql-strategy.md) | PostgreSQL como única base relacional | Accepted |
| [ADR-016](./ADR-016-frontend-modularization.md) | Frontend modular por dominio | Accepted |
| [ADR-017](./ADR-017-business-partner-scope.md) | BusinessPartner subscriber-scoped | Frozen |
| [ADR-018](./ADR-018-message-infrastructure.md) | Infraestructura centralizada de mensajes visuales | Frozen |
| [ADR-019](./ADR-019-document-sequence-infrastructure.md) | Infraestructura centralizada de secuencias documentales | Frozen |
| [ADR-020](./ADR-020-entity-tracking-infrastructure.md) | Infraestructura de seguimiento de entidades (EF Core Change Tracking) | Frozen |
| [ADR-021](./ADR-021-pricing-engine-ssot.md) | Motor de Pricing v2 — Item.BaseSalePrice SSOT | Closed |
| [ADR-022](./ADR-022-audit-infrastructure-entity-vs-process.md) | Infraestructura de Auditoría — Entity Audit (Frozen) + Process Audit (diseño futuro) | Frozen |
| [ADR-023](./ADR-023-electronic-documents-v1-closure.md) | ElectronicDocuments v1.0 — Cierre de módulo (facturación electrónica SRI) | Frozen |
| [ADR-024](./ADR-024-electronic-document-diagnostic-infrastructure.md) | Infraestructura de Diagnóstico SRI reutilizable (extensión controlada de ADR-023) | Accepted |
| [ADR-025](./ADR-025-ride-design-freeze.md) | Ride v1.0 — Design Freeze (RIDE, pre-implementación) | Design Frozen |
| [ADR-026](./ADR-026-accounting-core.md) | Accounting Core | Accepted |
| [ADR-027](./ADR-027-error-handling-architecture.md) | Arquitectura Unificada de Manejo de Errores — contrato Backend↔Frontend, categorías, mapeo HTTP, auditoría | Accepted (arquitectura) — migración pendiente |
| [ADR-028](./ADR-028-purchase-reception-to-purchase-flow-freeze.md) | Recepción XML de Compras → Compra — Cierre de flujo (XML evidencia fiscal, Snapshot operativo, Item Matching único) | Frozen |
| [ADR-029](./ADR-029-purchase-approval-workflow-future-evolution.md) | Purchase Approval Workflow — Guía de evolución futura (Direct/Approval/MultiApproval, no implementado) | Accepted — guía, no implementado |
| [ADR-030](./ADR-030-purchase-line-warehouse-mass-apply.md) | Bodega por línea en Compras — selector general como aplicación masiva, no sincronización | Accepted |
| [ADR-031](./ADR-031-credit-note-v1-activation.md) | Activación de Nota de Crédito V1.1.0 (extensión controlada de ADR-023) | Accepted |
| [ADR-032](./ADR-032-tax-line-ssot-ice-irbpnr.md) | ICE e IRBPNR como impuestos de línea — SSOT en `*DetailTax` (propuesta técnica) | Approved (dirección) — pre-implementación |
| [ADR-033](./ADR-033-payment-term-ssot-and-document-schedules.md) | PaymentTerm como SSOT operativo + defaults por empresa/rol + cronograma final por documento (CreditTerm fuera de alcance) | Approved (diseño) — pre-implementación |

Seguimiento de migración de ADRs aceptados con implementación pendiente: [`docs/architecture/ARCHITECTURE-BACKLOG.md`](../architecture/ARCHITECTURE-BACKLOG.md) (iniciativas `GOV-xxx`).

**Formato:** Status · Context · Decision · Consequences · Alternatives Considered

**Al proponer cambio arquitectónico:** crear ADR nuevo o actualizar existente antes de ampliar `docs/architecture/*` o checks CI.
