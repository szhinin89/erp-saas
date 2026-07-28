# Architecture Backlog — Iniciativas de Gobernanza / Arquitectura

Registro de iniciativas arquitectónicas y de gobernanza del ERP: decisiones ya aceptadas (ADR) que requieren una migración de código posterior, deuda arquitectónica transversal, o esfuerzos de estandarización que afectan a múltiples módulos. **No es un roadmap funcional** — para features de producto ver [`docs/ROADMAP.md`](../ROADMAP.md); para deuda técnica del pipeline del dashboard ver [`docs/ProgressDashboard/DASHBOARD_HEALTH.md`](../ProgressDashboard/DASHBOARD_HEALTH.md).

Este documento **nunca se borra**. Las iniciativas cerradas se marcan como `Completada`, no se eliminan. No se crean IDs nuevos para una iniciativa que ya existe — se actualiza la existente.

Cada entrada enlaza a su ADR/auditoría/reglas correspondientes — **no duplica su contenido**.

---

## Backlog Summary

- Iniciativas registradas: 1
- Abiertas / pendientes de migración: 1
- Completadas: 0
- Prioridad Alta: 1
- Última actualización: 2026-07-27

> Nota: la numeración empieza en `GOV-002` porque es el primer ID que se solicitó registrar formalmente en este documento; no existe una iniciativa `GOV-001` en ningún documento del repositorio al momento de esta creación. Si `GOV-001` corresponde a una iniciativa real registrada en otro sistema (fuera de este repo), agregarla aquí retroactivamente en vez de dejar el hueco sin explicar.

---

## Iniciativas

### GOV-002 — Arquitectura Unificada de Manejo de Errores

- **Estado:** Pendiente de migración
- **Prioridad:** 🔴 Alta
- **Tipo:** Gobernanza / Arquitectura
- **Fecha de registro:** 2026-07-27

**Objetivo resumido:** Unificar el manejo de errores del ERP mediante un contrato único Backend ↔ Frontend, estandarización de `FailureCode`, códigos HTTP semánticamente correctos, normalización de errores en el frontend y reglas obligatorias para nuevos desarrollos.

**Referencias (fuente de verdad — no duplicar contenido aquí):**

| Documento | Contenido |
|---|---|
| [`docs/adr/ADR-027-error-handling-architecture.md`](../adr/ADR-027-error-handling-architecture.md) | Decisión arquitectónica completa: contrato de envelope, categorías, mapeo HTTP, responsabilidades por capa, UX, plan de migración detallado |
| [`docs/architecture/ERROR-HANDLING-AUDIT.md`](./ERROR-HANDLING-AUDIT.md) | Auditoría del estado actual (matriz Cumple/Parcial/No cumple, backend y frontend) |
| [`AI-RULES/ERROR-HANDLING.md`](../../AI-RULES/ERROR-HANDLING.md) | Reglas obligatorias ejecutables (E-B1..E-B8, E-F1..E-F7) para todo módulo nuevo desde ya |

**Plan de ejecución** (detalle completo en ADR-027 §15 — "Plan de migración"):

| Fase | Nombre | Estado |
|---|---|---|
| 1 | Backend Core | No iniciada |
| 2 | Frontend Core | No iniciada |
| 3 | Migración de módulos | No iniciada |
| 4 | Eliminación de compatibilidad antigua | No iniciada |

**Restricciones vigentes mientras la iniciativa esté abierta:**
- No modifica funcionalidad del ERP ni el roadmap funcional (`docs/ROADMAP.md`) — es transversal a todas las etapas.
- Todo módulo nuevo debe cumplir `AI-RULES/ERROR-HANDLING.md` desde el momento de su creación, aunque la migración de módulos existentes (Fase 3) no haya comenzado.
- Ningún cambio a esta iniciativa se documenta duplicando el ADR — solo se actualiza `Estado`/`Fase actual`/`Observaciones` en esta entrada.

**Observaciones:** —

---

## Convenciones de este documento

- IDs correlativos (`GOV-001`, `GOV-002`, …), asignados una sola vez y nunca reutilizados ni renumerados.
- Estados válidos: `Propuesta`, `Pendiente de migración`, `En progreso`, `Completada`, `Descartada`.
- Una iniciativa completada no se elimina — se marca `Completada` con fecha, y su ADR (si queda FROZEN) pasa a referenciarse también desde `CLAUDE.md`/`docs/STATUS.md` según corresponda.
- Este documento registra la iniciativa y enlaza su documentación — el detalle técnico, el rationale y el plan de migración viven exclusivamente en el ADR referenciado.
