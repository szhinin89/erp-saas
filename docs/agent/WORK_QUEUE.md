# WORK_QUEUE.md — Cola de tareas controladas

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
- **A9** — Commercial → ElectronicDocument recovery
