# WORK_QUEUE.md — Cola de tareas controladas

## PURCHASES FASE B — BUG REAL: ORDEN DE MIGRACIÓN

**Status: CORREGIDO y validado focalmente (2026-10-15).** Índices/trigger de activos
antes del backfill; regresiones de ambas claves únicas. PostgreSQL PASS 6/6 + 2/2
trigger, reproducción previa FAIL 4/4 por 23505. Reaplicación al piloto pendiente.
Sin suites completas, commit/push. [Entrega](PURCHASES_PHASE_B_DELIVERY.md).

## PURCHASES LEGACY / DOMAIN — FASE B

**Status: IMPLEMENTADO y validado focalmente (2026-10-15).** Sin commit/push.
SSOT PurchaseState + PurchaseId y Discarded=4 persistido; sin flags legacy.
Pruebas focales PASS, incluida migración/índices en PostgreSQL real. Aplicación
al piloto pendiente de despliegue. [Entrega](PURCHASES_PHASE_B_DELIVERY.md).

## A9 — Commercial → ElectronicDocument recovery

**Status: IMPLEMENTADO y validado; apto para declarar CLOSED (2026-10-08).**

Recuperación durable exclusivamente de SalesInvoice Authorized + Electronic + secuencial definitivo sin documento por origen. Registro create-only en infraestructura existente; scopes frescos; concurrencia PostgreSQL segura. No reautoriza ni reanuda/envía documentos existentes. Sin migración, commit ni push. Gates y resultados: `STATUS.md`, sección A9. A1–A8 permanecen CLOSED por instrucción vigente.

## A3 — Atomic Sales Draft Update

**Status: OPEN**

### Problema
Update Draft puede modificar/eliminar hijos antes de completar validación/save,
dejando el Draft parcialmente alterado si la operación falla.

### Objetivo
Una actualización rechazada debe dejar el Sales Draft exactamente igual.

### Alcance
- Sales Draft Update
- Lines
- Taxes
- Payments / schedules si forman parte del mismo agregado
- Transaction boundary
- Repository behavior
- PostgreSQL tests

### No tocar
- A1
- A2
- Pricing
- SRI
- Accounting
- Inventory salvo dependencia directa

### Gate
- reproducir bug
- test que falle antes
- fix mínimo
- test PostgreSQL real
- regresión focalizada
- diff review
- no commit

---

## Después (orden previsto, aún sin abrir)

- **A4** — SalesReturn taxes ICE/IRBPNR
- **A5** — Packaging barcode search
- **A6** — Warehouse guards/race
- **A7** — Sales concurrency
- **A8** — Authorization retry/idempotence
