# Decisiones arquitectónicas vigentes

Cada ADR conserva una decisión y su justificación; el comportamiento se define en su SSOT.
Gates/cierres: [STATUS](../../STATUS.md). Futuro: [ROADMAP](../ROADMAP.md).

| ADR | Responsabilidad |
|---|---|
| [ADR-001](ADR-001-modular-monolith.md) | ADR-001: Modular monolith |
| [ADR-002](ADR-002-no-erp-shared.md) | ADR-002: No ERP.Shared |
| [ADR-003](ADR-003-pages-wrapper-only.md) | ADR-003: Pages wrapper only (frontend) |
| [ADR-004](ADR-004-clean-architecture-enforcement.md) | ADR-004: Clean Architecture enforcement |
| [ADR-005](ADR-005-multi-tenant-query-filters.md) | ADR-005: Multi-tenant query filters |
| [ADR-006](ADR-006-multi-agent-governance.md) | ADR-006: Multi-agent governance (Cursor + Claude) |
| [ADR-007](ADR-007-domain-events-foundation.md) | ADR-007: Domain Events Foundation |
| [ADR-008](ADR-008-outbox-pattern-foundation.md) | ADR-008: Outbox Pattern Foundation |
| [ADR-009](ADR-009-ai-layer-separation.md) | ADR-009: AI Layer Separation |
| [ADR-010](ADR-010-event-versioning.md) | ADR-010: Event Versioning Strategy |
| [ADR-011](ADR-011-outbox-retention-strategy.md) | ADR-011: Outbox Retention Strategy |
| [ADR-012](ADR-012-ai-read-model-strategy.md) | ADR-012: AI Read Model Strategy |
| [ADR-013](ADR-013-cqrs-mediatr.md) | ADR-013: CQRS con MediatR y FluentValidation |
| [ADR-014](ADR-014-refresh-token-rotation.md) | ADR-014: Refresh token rotation enterprise |
| [ADR-015](ADR-015-postgresql-strategy.md) | ADR-015: PostgreSQL como única base relacional |
| [ADR-016](ADR-016-frontend-modularization.md) | ADR-016: Frontend modular por dominio |
| [ADR-017](ADR-017-business-partner-scope.md) | ADR-017: BusinessPartner como entidad subscriber-scoped |
| [ADR-018](ADR-018-message-infrastructure.md) | ADR-018: Infraestructura centralizada de mensajes visuales |
| [ADR-019](ADR-019-document-sequence-infrastructure.md) | ADR-019: Infraestructura centralizada de secuencias documentales |
| [ADR-020](ADR-020-entity-tracking-infrastructure.md) | ADR-020: Infraestructura de seguimiento de entidades (EF Core Change Tracking) |
| [ADR-021](ADR-021-pricing-engine-ssot.md) | ADR-021: Motor de Pricing v2 — Item.BaseSalePrice como SSOT + reglas de ajuste |
| [ADR-022](ADR-022-audit-infrastructure-entity-vs-process.md) | ADR-022: Infraestructura de Auditoría por Dominio — Entity Audit (FROZEN) + Process Audit (futuro) |
| [ADR-023](ADR-023-electronic-documents-v1-closure.md) | ADR-023: ElectronicDocuments v1.0 — Cierre de Módulo (FROZEN core, mantenimiento) |
| [ADR-024](ADR-024-electronic-document-diagnostic-infrastructure.md) | ADR-024: Infraestructura de Diagnóstico SRI reutilizable (extensión controlada de ADR-023) |
| [ADR-025](ADR-025-ride-design-freeze.md) | ADR-025: RIDE — decisión de presentación fiscal |
| [ADR-026](ADR-026-accounting-core.md) | ADR-026 — Accounting Core Architecture |
| [ADR-027](ADR-027-error-handling-architecture.md) | ADR-027 — Arquitectura unificada de errores |
| [ADR-028](ADR-028-purchase-reception-to-purchase-flow-freeze.md) | ADR-028 — Separación XML, recepción y compra |
| [ADR-031](ADR-031-credit-note-v1-activation.md) | ADR-031: Activación de Nota de Crédito V1.1.0 (extensión controlada de ADR-023) |
| [ADR-032](ADR-032-tax-line-ssot-ice-irbpnr.md) | ADR-032: ICE e IRBPNR como impuestos de línea — SSOT en `*DetailTax` (decisión arquitectónica) |
| [ADR-033](ADR-033-payment-term-ssot-and-document-schedules.md) | ADR-033: PaymentTerm como SSOT operativo + defaults por empresa/rol + cronograma final por documento |
| [ADR-034](ADR-034-temporal-contract-single-source.md) | ADR-034 — Contrato temporal único: fecha de negocio vs. instante UTC |
| [ADR-035](ADR-035-supplier-payment-unapplied-advance.md) | ADR-035 — Remanente no aplicado de SupplierPayment como anticipo (SupplierCredit) |
| [ADR-036](ADR-036-retention-electronic-document-cancellation.md) | ADR-036 — Ciclo fiscal y anulación de retenciones |
| [ADR-037](ADR-037-sri-global-catalog-governance.md) | ADR-037 — Gobernanza de catálogos globales SRI |
| [ADR-038](ADR-038-sri-electronic-compliance-architecture.md) | ADR-038 — Arquitectura de cumplimiento electrónico SRI |
| [ADR-039](ADR-039-communications-architecture.md) | ADR-039 — Communications como capacidad transversal |
| [ADR-040](ADR-040-item-company-scope-inventory-nature.md) | ADR-040 — Item por empresa y naturaleza operativa |
| [ADR-041](ADR-041-negative-sale-inventory-cost-obligations.md) | ADR-041 — Costo desconocido, provisional y obligaciones de venta |
| [ADR-042](ADR-042-sri-seed-boolean-flags-persistence.md) | ADR-042 — Flags booleanos de catálogos SRI: el seed persiste el valor declarado |
| [ADR-043](ADR-043-background-processing-ssot.md) | ADR-043 — Orquestación recurrente única |
| [ADR-044](ADR-044-purchase-cancellation-and-global-sri-policy.md) | ADR-044 — Anulación de compra y política fiscal global |
| [ADR-045](ADR-045-accounting-operational-continuity.md) | ADR-045 — Consolidación de la arquitectura de Accounting |
| [ADR-046](ADR-046-historical-handler-size-debt.md) | ADR-046 — Excepción temporal para deuda histórica de tamaño de handlers C-03 |
| [ADR-047](ADR-047-historical-tsx-size-debt.md) | ADR-047 — Excepción temporal para deuda histórica de tamaño TSX (PR-7) |
| [ADR-048](ADR-048-company-suspension-automatic-processes.md) | ADR-048 — Empresa suspendida o deshabilitada: procesos automáticos y sesiones |
| [ADR-049](ADR-049-sales-order-v1-architecture.md) | ADR-049 — Orden de Pedido de Venta V1 (Gestión de Ventas): diseño aprobado, no implementado |
| [ADR-050](ADR-050-employees-v1-architecture.md) | ADR-050 — Empleados V1 (módulo técnico independiente): arquitectura aprobada; implementada (Empleados V1 CLOSED, ver [STATUS](../../STATUS.md)) |
| [ADR-051](ADR-051-spa-v1.1-architecture.md) | ADR-051 — SPA V1.1: ACCEPTED / APROBADO; Spa planificado, no implementado, sin fases autorizadas |
| [ADR-052](ADR-052-customer-advances-v1-architecture.md) | ADR-052 — Anticipos de Clientes V1: ACCEPTED / APROBADO (2026-11-26); solo CA-F1 autorizada, sin funcionalidad operativa |
| [ADR-ERP](ADR-ERP-001-core-independence.md) | ADR-ERP-001 — ERP Core Independence |
| [ADR-ERP](ADR-ERP-002-platform-separation.md) | ADR-ERP-002 — Platform Separation (frontera de integración) |

## Diseños con consumidores

Los siguientes conservan decisiones referenciadas y no sustituyen las SSOT:

- [DOCUMENT-SEQUENCES-DESIGN-02.md](DOCUMENT-SEQUENCES-DESIGN-02.md).
- [RETENTIONS-MODULE-DESIGN-01.md](RETENTIONS-MODULE-DESIGN-01.md).
- [EXPENSE-SUPPLIER-SUBCATEGORIES-DESIGN-01.md](EXPENSE-SUPPLIER-SUBCATEGORIES-DESIGN-01.md) (diseño, sin implementar).
- [PEDIDOS-VENTA-V1-FUNCTIONAL-SPEC-01.md](PEDIDOS-VENTA-V1-FUNCTIONAL-SPEC-01.md) (diseño funcional de ADR-049, sin implementar; no es SSOT).
- [EMPLOYEES-V1-CONTRACT-SPEC-01.md](EMPLOYEES-V1-CONTRACT-SPEC-01.md) (especificación contractual de ADR-050, implementada; no es SSOT: ver [employees.md](../architecture/employees.md)).
- [RETENTIONS-SRI-AUTHORIZATION-WIRING-DESIGN-04B.md](RETENTIONS-SRI-AUTHORIZATION-WIRING-DESIGN-04B.md).
- [CUSTOMER-ADVANCES-V1-DESIGN-01.md](CUSTOMER-ADVANCES-V1-DESIGN-01.md) (diseño de ADR-052, con decisiones funcionales aprobadas; sin funcionalidad operativa; no es SSOT; [plan](../agent/CUSTOMER-ADVANCES-V1-IMPLEMENTATION-PLAN-01.md)).

## SPA V1.1 — Módulo de Gestión de Spa y Tratamientos Estéticos: planificado, no implementado

**Consolidación de planificación existente; no SSOT implementada. Ninguna fase de Spa está autorizada** (Empleados V1, su dependencia, está implementado y CLOSED, sin integración con Spa). El contrato concentra el comportamiento; el ADR explica arquitectura y el plan ordena dependencias. SPA-DEC-115/116 recuperadas y APROBADAS históricamente; O-23 CERRADA en inclusión funcional general. Detalles técnicos/legales y fichas O-24/V-01 siguen pendientes, sin autorización de implementar.

| Documento | Responsabilidad | Estado |
|---|---|---|
| [Contrato funcional](SPA-V1.1-CONTRACT-SPEC-01.md) | Planificación histórica, reglas, pantallas, datos, integraciones, estados y aceptación | Alcance funcional APROBADO; consolidación sin aprobación técnica integral ni autorización de implementación |
| [ADR-051](ADR-051-spa-v1.1-architecture.md) | Arquitectura de referencia y condiciones | ACCEPTED / APROBADO (2026-11-19); no implementada |
| [Plan de implementación](SPA-V1.1-IMPLEMENTATION-PLAN-01.md) | Fases, dependencias, pruebas y cierre | Documentado; ninguna fase autorizada |
| [Registro de decisiones](SPA-V1.1-APPROVED-DECISIONS-01.md) | SPA-DEC-115/116 históricas recuperadas, O-25/O-15 literales, Empleados aplicable y DIR-SSOT-01 | Evidencia de alcance limitado; no aprobación global |

Los seis antecedentes previos (RECOVERY-01, OPERATIONS-SPEC-DRAFT-01, CARE-PRIVACY-SPEC-DRAFT-01, SECURITY-COMPLIANCE-DRAFT-01, matriz de consolidación y normalización) se retiraron tras consolidar su contenido en estos cuatro documentos; sus versiones previas están en el historial Git.
