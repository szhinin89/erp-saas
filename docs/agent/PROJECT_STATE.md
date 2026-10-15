# PROJECT_STATE.md — Estado vigente del proyecto

## PURCHASES FASE B — BUG REAL: ORDEN DE MIGRACIÓN (2026-10-15)

Corregido el 23505 de históricos con claves reutilizadas: ambos índices de activos
y función del trigger se actualizan antes del backfill. Reproducción FAIL 4/4;
fix PostgreSQL PASS 6/6 y trigger PASS 2/2. Sin reaplicar al piloto, suites completas,
commit ni push. [Detalle](PURCHASES_PHASE_B_DELIVERY.md).

## PURCHASES LEGACY / DOMAIN — FASE B (2026-10-15)

Implementado y validado focalmente, sin commit/push. Recepción usa PurchaseState +
PurchaseId; Discarded=4 es estado persistido propio, sin inferencias por totales ni
booleanos legacy. Activos Draft/Confirmed, históricos Cancelled/Discarded.
Application 51/51, Infrastructure 41/41 + migración PostgreSQL 1/1, frontend 90/90;
build/TypeScript/lint focal/arquitectura/diff PASS. Migración al piloto pendiente.
Detalle e inventario: [entrega](PURCHASES_PHASE_B_DELIVERY.md).

## ZH BACKGROUND PROCESSING — STRUCTURAL DEBT (2026-10-15)

Implementado y validado, sin commit/push. Siete jobs, runner/catálogo/options únicos,
locks PostgreSQL, lotes y checkpoints durables; API y worker separables. ADR-043
reemplaza explícitamente la orquestación FROZEN anterior. Gate focal PASS; detalle
en STATUS.md y BACKGROUND_PROCESSING_DELIVERY.md. Migración creada y validada en
PostgreSQL de pruebas; aplicación al piloto pendiente de despliegue.

Actualizado: 2026-10-06. Fuente de verdad de entrega: `STATUS.md`. Este archivo es un espejo operativo condensado; en caso de discrepancia, prevalece `STATUS.md` + ADRs citados.

## Entregas cerradas

### A1 CLOSED
- Commit: `43cce57a9`
- Tema: Item Company Scope / Inventory Nature
- ADR: [ADR-040](../decisions/ADR-040-item-company-scope-inventory-nature.md)
- No reabrir salvo BUG REAL.

### A2 CLOSED
- Commit: `64e1b21ca`
- Tema: Negative Sales / Deferred COGS
- ADR: [ADR-041](../decisions/ADR-041-negative-sale-inventory-cost-obligations.md)
- No reabrir salvo BUG REAL.

## Estado general

### A9 implementado y validado (2026-10-08)
- Commercial → ElectronicDocument recovery: job recurrente create-only, scopes frescos y unicidad por origen existente; sin pipeline para documentos existentes ni repetición comercial.
- Gates PASS: PostgreSQL 19/19, guard de filtros 1/1, job 1/1, issuer 31/31, Architecture 143/143, architecture:check, API build y diff check.
- Apto para declarar CLOSED. Sin migración, commit ni push. Detalle vigente: `STATUS.md`, sección A9.

ERP en preparación final / piloto real.
No desarrollo general.
Prioridad: bugs reales, blockers de piloto y deuda estructural con impacto real.
