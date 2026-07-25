# Dashboard Health — Historial de deuda técnica del pipeline

Fuente de verdad de la salud técnica del `ProgressDashboard` (`tools/dashboard/*` → `docs/ProgressDashboard/data/*.json` → `docs/ProgressDashboard/index.html`). Ver contrato del pipeline en [`DASHBOARD-CONTRACT.md`](DASHBOARD-CONTRACT.md).

Este documento **nunca se borra**. Los hallazgos resueltos se marcan como `Resuelto`, no se eliminan. No se crean IDs nuevos para un problema que ya existe — se actualiza el existente.

---

## Dashboard Health Summary

- Hallazgos abiertos: 2
- Hallazgos resueltos: 0
- Hallazgos críticos: 0
- Hallazgos importantes: 2
- Hallazgos menores: 0
- Última auditoría: 2026-07-24
- Estado general: Advertencia

---

## Historial de hallazgos

### DH-001 — Datos del pipeline desactualizados

- **Fecha de detección:** 2026-07-24
- **Estado:** Abierto
- **Prioridad:** 🟧 Importante
- **Descripción:** Los JSON en `docs/ProgressDashboard/data/` fueron generados por última vez el 2026-07-20 19:20 (timestamps `generated` de `architecture-progress.json`, `impact.json`, `model-health.json`, `dependencies.json`, `release-simulation.json`, `recommendations.json`, `navigation-map.json`, `completion-intelligence.json`, `dashboard-summary.json`, `critical-path.json`, `explorer-index.json`), mientras 158 archivos `.cs`/`.ts`/`.tsx` en `backend/src` y `frontend/src` tienen fecha de modificación posterior a esa corrida.
- **Impacto:** Las métricas, scores y estados mostrados en `index.html` pueden no reflejar el estado real actual del código. No rompe el pipeline; es información desactualizada.
- **Recomendación:** Ejecutar el pipeline completo (`run-dashboard-final.ps1` + `analyze-modules/features/processes/tasks/impact.ps1` + `render-dashboard.ps1`) en la próxima entrega que modifique código real de algún módulo.
- **Responsable:** Automático (pipeline)
- **Fecha de resolución:** —
- **Observaciones:** Persistirá como `Abierto` hasta que se corra el pipeline completo tras un cambio real de código. No se regenera solo para "refrescar la fecha" — regla de [[feedback_dashboard_regen_criteria]].

### DH-002 — 14 archivos JSON huérfanos sin documentar en `data/`

- **Fecha de detección:** 2026-07-24
- **Estado:** Abierto
- **Prioridad:** 🟧 Importante
- **Descripción:** `dashboard-model-v7.json`, `dashboard-model-v9.json`, `dashboard-model.json`, `dashboard-diff.json`, `dashboard-state.json`, `git-analysis.json`, `history.json`, `history-retention.json`, `metrics.json`, `production.json`, `project-model.json`, `project-tree.json`, `risks.json`, `roadmap.json` no son leídos por ningún script de `tools/dashboard/*.ps1` (verificado por grep cruzado del nombre de archivo contra todos los `.ps1`). A diferencia de otros huérfanos ya aceptados y documentados (`dashboard-model-v10.json`, `api-analysis.json`, `docs-analysis.json` — ver `tools/dashboard/README.md` líneas 78-80), estos 14 no figuran en ningún lado como deuda conocida.
- **Impacto:** ~750 KB de datos obsoletos en el repositorio; riesgo de que un futuro agente o desarrollador los confunda con la fuente de verdad vigente del pipeline.
- **Recomendación:** Documentar en `tools/dashboard/README.md` como deuda aceptada (mismo patrón que `dashboard-model-v10.json`), o archivar/eliminar tras confirmación explícita del usuario.
- **Responsable:** Decisión conjunta
- **Fecha de resolución:** —
- **Observaciones:** Ninguna corrección automática — requiere decisión explícita del usuario sobre documentar vs. archivar/eliminar.

---

## Convenciones de este documento

- IDs correlativos (`DH-001`, `DH-002`, …), asignados una sola vez y nunca reutilizados ni renumerados.
- Estados válidos: `Abierto`, `En progreso`, `Resuelto`, `Descartado`.
- Un hallazgo que reaparece tras marcarse `Resuelto` no genera un ID nuevo: se reabre el mismo ID, se actualiza `Estado` a `Abierto` y se agrega una observación con la fecha de reaparición.
- La sección "Dashboard Health Summary" se recalcula en cada entrega que toque este archivo.
