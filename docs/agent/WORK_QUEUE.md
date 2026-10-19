# WORK_QUEUE.md — Cola de tareas controladas

## QA-INVENTORY-INFRA-SUITES-01 (2026-10-19)

CLOSED. Resuelto en `509ac6d75`; PostgreSQL 39/39 PASS. Regla: [inventory.md § 2](../architecture/inventory.md#2-participación-y-alcance). Sin bloqueadores.

## AUTH - SENSITIVE LOGGING FIX (2026-10-19)

BUG REAL corregido: evento de auditoria de rotacion refresh_success registraba successorHash (SHA-256 del refresh token sucesor, clave de busqueda en BD) por parametro explicito del template LoggerMessage de RefreshTokenService (no serializacion automatica ni configuracion). Eliminado successorHash de mensajes y campos estructurados; trazabilidad conservada por familyId, tokenId y successorId. Sin cambios de logica de autenticacion, rotacion ni persistencia. PASS: RefreshTokenServiceTests 21/21 (19 existentes + 2 nuevos con CapturingLogger: rotacion, reuso benigno/sospechoso y token inexistente sin tokens ni hashes en logs); los 2 nuevos fallan contra el codigo previo. Logs historicos previos al fix pueden contener hashes. Sin suites completas, commit ni push.


## FIRST RUN - CONSOLIDACION SSOT (2026-10-19)

Cerrado. Flujo vigente: launcher -> API/frontend -> `/setup` si no esta inicializado -> alta inicial manual -> ERP operativo. Contrato, seguridad, operacion y gate de pruebas: [docs/architecture/first-run.md](../architecture/first-run.md). Documentos historicos `docs/agent/FIRST_RUN_*` retirados. QA obsoleto retirado: `qa-engine/`, `qa-bot/` y workflow `qa-regression.yml` dependian de endpoints Platform/no versionados inexistentes. `IsFirstRun` se conserva (redundante; retirarlo exige migracion sobre el baseline aplicado).


## BACKGROUND DEPLOYMENT — FASE D

**Status: IMPLEMENTADO y validado focalmente (2026-10-15).** API/Worker separados
en Compose canónico, misma imagen/volumen, storage explícito y Redis único.
PASS Compose 4/4, tests 21/21 + PostgreSQL 4/4, build/smoke aislado. Coolify real
pendiente de inspección/transición según runbook. Sin suites completas, commit/push.
[Entrega](BACKGROUND_DEPLOYMENT_PHASE_D_DELIVERY.md).

## PURCHASES FASE B — BUG REAL: ORDEN DE MIGRACIÓN

**Status: CORREGIDO y validado focalmente (2026-10-15).** Índices/trigger de activos
antes del backfill; regresiones de ambas claves únicas. PostgreSQL PASS 6/6 + 2/2
trigger, reproducción previa FAIL 4/4 por 23505. Reaplicación al piloto pendiente.
Sin suites completas, commit/push. [purchases.md](../architecture/purchases.md).

## PURCHASES LEGACY / DOMAIN — FASE B

**Status: IMPLEMENTADO y validado focalmente (2026-10-15).** Sin commit/push.
SSOT PurchaseState + PurchaseId y Discarded=4 persistido; sin flags legacy.
Pruebas focales PASS, incluida migración/índices en PostgreSQL real. Aplicación
al piloto pendiente de despliegue. [purchases.md](../architecture/purchases.md).

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
