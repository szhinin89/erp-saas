# AGENTS.md — Reglas de operación por tareas controladas

## Leer antes de modificar código

- `STATUS.md`
- `docs/decisions/` (especialmente ADRs vigentes; mínimos relevantes: ADR-040, ADR-041)
- `docs/agent/PROJECT_STATE.md`
- `docs/agent/WORK_QUEUE.md`
- `docs/agent/DECISIONS.md`

## Regla de CLOSED

- **CLOSED = no reauditar ni rediseñar salvo BUG REAL.**
- No modificar módulos CLOSED salvo BUG REAL directamente relacionado.

## Clasificación de hallazgos

Clasificar hallazgos **solo** como una de estas categorías:

- `KEEP`
- `EXTEND`
- `NEW`
- `BUG REAL`
- `STRUCTURAL DEBT`
- `PILOT BLOCKER`
- `FUTURE`
- `DO NOT TOUCH`

## Flujo de trabajo por tarea

audit focalizado → reproducir → fix mínimo → tests → diff review

## Contradicciones y dominio no definido

Si aparece contradicción de negocio o dominio no definida:
**STOP y reportar únicamente la decisión necesaria.** No avanzar con supuestos.

## Base de datos

PostgreSQL real obligatorio para validar:
concurrencia, transacciones, scope, locks e invariantes de persistencia.
No sustituir por SQLite, in-memory ni mocks para estos casos.

## Alcance

- No hacer refactors fuera de alcance.
- No commit ni push salvo autorización expresa.

## Estilo de respuesta

Mantener respuestas compactas.
