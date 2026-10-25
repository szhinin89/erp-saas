# AGENTS.md — Reglas de operación por tareas controladas

## Leer antes de modificar código

- `STATUS.md`
- `docs/decisions/` (especialmente ADRs vigentes; mínimos relevantes: ADR-040, ADR-041)
- `docs/agent/PROJECT_STATE.md`
- `docs/agent/WORK_QUEUE.md`
- `docs/agent/DECISIONS.md`

## Reglas globales

`CLAUDE.md` es obligatorio también para Codex y prevalece sobre este archivo; incluye la regla **ERP ZH V1 GREENFIELD** (una única versión oficial, sin código/migraciones/seeds/scripts/ADR/docs paralelos u obsoletos, preservando datos y sin operaciones destructivas).

## Regla de CLOSED

- **CLOSED = no reauditar ni rediseñar salvo BUG REAL.**
- No modificar módulos CLOSED salvo BUG REAL directamente relacionado.

## Flujo de trabajo por tarea

Aplicar el [flujo obligatorio del agente por fase](docs/architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase), incluida la clasificación `KEEP / BUG REAL / DEBT / FALTA` y la separación de responsabilidades entre agente y propietario.

## Contradicciones y dominio no definido

Si aparece contradicción de negocio o dominio no definida:
**STOP y reportar únicamente la decisión necesaria.** No avanzar con supuestos.

## Base de datos

PostgreSQL real obligatorio para validar:
concurrencia, transacciones, scope, locks e invariantes de persistencia.
No sustituir por SQLite, in-memory ni mocks para estos casos.

## Alcance

- No hacer refactors fuera de alcance.
- Staging y commit los realiza el propietario según el flujo por fase. No hacer push salvo autorización expresa.

## Estilo de respuesta

Mantener respuestas compactas.
