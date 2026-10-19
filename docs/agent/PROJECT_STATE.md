# PROJECT_STATE.md — Estado vigente del proyecto

## AUTH - SENSITIVE LOGGING FIX (2026-10-19)

BUG REAL corregido: evento de auditoria de rotacion refresh_success registraba successorHash (SHA-256 del refresh token sucesor, clave de busqueda en BD) por parametro explicito del template LoggerMessage de RefreshTokenService (no serializacion automatica ni configuracion). Eliminado successorHash de mensajes y campos estructurados; trazabilidad conservada por familyId, tokenId y successorId. Sin cambios de logica de autenticacion, rotacion ni persistencia. PASS: RefreshTokenServiceTests 21/21 (19 existentes + 2 nuevos con CapturingLogger: rotacion, reuso benigno/sospechoso y token inexistente sin tokens ni hashes en logs); los 2 nuevos fallan contra el codigo previo. Logs historicos previos al fix pueden contener hashes. Sin suites completas, commit ni push.


## FIRST RUN - CONSOLIDACION SSOT (2026-10-19)

Cerrado. Flujo vigente: launcher -> API/frontend -> `/setup` si no esta inicializado -> alta inicial manual -> ERP operativo. Contrato, seguridad, operacion y gate de pruebas: [docs/architecture/first-run.md](../architecture/first-run.md). Documentos historicos `docs/agent/FIRST_RUN_*` retirados. QA obsoleto retirado: `qa-engine/`, `qa-bot/` y workflow `qa-regression.yml` dependian de endpoints Platform/no versionados inexistentes. `IsFirstRun` se conserva (redundante; retirarlo exige migracion sobre el baseline aplicado).


## PURCHASE CANCELLATION — BUG REAL (2026-10-16)

Implementado; gate conservador de salidas posteriores bajo lock y política SRI
global versionada. Validación focal PASS. Sin suites generales, commit/push ni
migración al piloto. Calendario oficial y ventana interna Company: STRUCTURAL DEBT.
[Entrega](PURCHASE_CANCELLATION_DELIVERY.md), [ADR-044](../decisions/ADR-044-purchase-cancellation-and-global-sri-policy.md).

## BACKGROUND DEPLOYMENT — FASE D (2026-10-15)

Implementado: Compose único API OFF / Worker ON, misma imagen y volumen; storage
explícito, Redis único, orden API ready → Worker. PASS Compose 4/4, tests focales
21/21 + PostgreSQL 4/4, build y smoke aislado. Sin tocar piloto/Coolify, suites
completas, commit ni push. Transición real pendiente. [Entrega](BACKGROUND_DEPLOYMENT_PHASE_D_DELIVERY.md).

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
