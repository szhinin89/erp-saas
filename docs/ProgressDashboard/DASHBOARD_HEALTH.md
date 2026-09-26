# Dashboard Health — Historial de deuda técnica del pipeline

Fuente de verdad de la salud técnica del `ProgressDashboard` (`tools/dashboard/*` → `docs/ProgressDashboard/data/*.json` → `docs/ProgressDashboard/index.html`). Ver contrato del pipeline en [`DASHBOARD-CONTRACT.md`](DASHBOARD-CONTRACT.md).

Este documento **nunca se borra**. Los hallazgos resueltos se marcan como `Resuelto`, no se eliminan. No se crean IDs nuevos para un problema que ya existe — se actualiza el existente.

---

## Dashboard Health Summary

- Hallazgos abiertos: 1
- Hallazgos resueltos: 3
- Hallazgos críticos: 0
- Hallazgos importantes: 1
- Hallazgos menores: 0
- Última auditoría: 2026-09-26
- Estado general: Advertencia

---

## Historial de hallazgos

### DH-001 — Datos del pipeline desactualizados

- **Fecha de detección:** 2026-07-24
- **Estado:** Resuelto
- **Prioridad:** 🟧 Importante
- **Descripción:** Los JSON en `docs/ProgressDashboard/data/` fueron generados por última vez el 2026-07-20 19:20 (timestamps `generated` de `architecture-progress.json`, `impact.json`, `model-health.json`, `dependencies.json`, `release-simulation.json`, `recommendations.json`, `navigation-map.json`, `completion-intelligence.json`, `dashboard-summary.json`, `critical-path.json`, `explorer-index.json`), mientras 158 archivos `.cs`/`.ts`/`.tsx` en `backend/src` y `frontend/src` tienen fecha de modificación posterior a esa corrida.
- **Impacto:** Las métricas, scores y estados mostrados en `index.html` pueden no reflejar el estado real actual del código. No rompe el pipeline; es información desactualizada.
- **Recomendación:** Ejecutar el pipeline completo (`run-dashboard-final.ps1` + `analyze-modules/features/processes/tasks/impact.ps1` + `render-dashboard.ps1`) en la próxima entrega que modifique código real de algún módulo.
- **Responsable:** Automático (pipeline)
- **Fecha de resolución:** 2026-09-26
- **Observaciones:** Persistirá como `Abierto` hasta que se corra el pipeline completo tras un cambio real de código. No se regenera solo para "refrescar la fecha" — regla de [[feedback_dashboard_regen_criteria]]. **2026-09-26 (ZH-DASHBOARD-DH003-ADR-PATH-FIX-01):** resuelto al corregir DH-003. `run-dashboard-final.ps1` completo terminó con exit 0 tras el cambio real de código de SPAY-02C (`c395ec6c`): regeneró los 30 JSON de `data/`, `index.html` y el snapshot de `history/`. Evidencia de 02C en los datos: migración `SupplierPaymentUnappliedAdvance`, `GetSupplierPaymentPolicy*`, módulo Payables.

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

### DH-003 — `render-dashboard.ps1` falla: lee `docs/adr`, eliminado al consolidar la documentación

- **Fecha de detección:** 2026-09-26 (entrega ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C-PROD-CLOSE)
- **Estado:** Resuelto
- **Prioridad:** 🟥 Crítico
- **Descripción:** `tools/dashboard/render-dashboard.ps1` (línea ~2936) hace `Get-ChildItem (Join-Path $ProjectRoot "docs\adr")`. Esa carpeta se eliminó en `8e925b70` (docs: consolidate project rules…); los ADR viven hoy en `docs/decisions/`. `run-dashboard-final.ps1` aborta con "render-dashboard.ps1 failed" después de actualizar los JSON de `data/`, sin generar `index.html`.
- **Impacto:** El Dashboard no puede regenerarse. Una corrida deja los `data/*.json` actualizados y el `index.html` viejo: estado inconsistente. En esta entrega se revirtieron las salidas parciales, así que el repo queda coherente, sin regenerar.
- **Recomendación:** Apuntar el renderer a `docs/decisions` (o hacer opcional la lectura de ADR) en una tarea propia del pipeline, y luego ejecutar `run-dashboard-final.ps1` completo. Eso también cerraría DH-001.
- **Responsable:** Pipeline (tarea dedicada)
- **Fecha de resolución:** 2026-09-26 (ZH-DASHBOARD-DH003-ADR-PATH-FIX-01)
- **Observaciones:** No se corrigió en 02C (hallazgo Crítico preexistente, fuera de alcance). Bloquea la regeneración pedida tras 02C.
- **Causa raíz:** el commit `8e925b70` (docs: consolidate project rules and archive historical plans) movió `docs/adr/*` a `docs/decisions/` y renombró `docs/STATUS.md` a `/STATUS.md`, pero `tools/dashboard/*` siguió leyendo las rutas viejas.
- **Corrección mínima:** una sola fuente por concepto, sin fallback, sin doble ruta y sin crear carpetas.
  - ADRs → `docs/decisions`, en `render-dashboard.ps1` (verificación de ADR de gobernanza), `validate-dashboard.ps1` (check 6, referencias rotas) y `analyze-docs.ps1`. En este último el filtro pasa a `ADR-*.md`, porque `docs/decisions` también contiene documentos que no son ADR; ese script no lo invoca `run-dashboard-final.ps1` (salida huérfana, DH-002).
  - `STATUS.md` → raíz del repo, en `render-dashboard.ps1` (lectura de consistencia). Es el mismo commit y la misma clase de defecto: la única otra ruta faltante del pipeline. Se incluyó aquí por decisión explícita del usuario, en lugar de abrir un DH-004.
  - Etiquetas y comentarios visibles que nombraban las rutas viejas, actualizados en consecuencia.
- **Verificación:** `run-dashboard-final.ps1` → exit 0, sin errores en el log; `index.html` y `data/*.json` regenerados; la sección ADR muestra "Todas las referencias a ADR fueron verificadas contra docs/decisions/"; `validate-dashboard` reporta `Broken references: 0`; no hay salidas parciales.
- **Nota informativa (sin ID nuevo):** quedan citas en prosa a `docs/adr/...` dentro de datos fuente curados a mano (`roadmap.json`, `architecture-progress-source.json`, `modules-status.json`). Son texto histórico, no lecturas de ruta: no afectan ningún cálculo. Actualizarlas es una edición de contenido fuente, fuera de esta corrección.

---

### DH-004 — Regeneración sobrescribe presentación curada e historial

- **Fecha de detección / resolución:** 2026-09-26
- **Estado:** Resuelto
- **Causa raíz:** commits `2a48d75d`, `cf2c7ff3`, `0162d2a7` y `e909f1ef` editaron la salida HTML sin trasladar su presentación al renderer. `51f52f21` volvió a generar la versión anterior del renderer.
- **Corrección:** fuente explícita recuperada de `c395ec6c` en `templates/`, composición validada antes de escribir, datos técnicos actuales con precedencia y referencias ausentes conservadas como históricas. Se recuperan 39 secciones, navegación y diagrama con porcentajes originales y futuros a 0%.
- **Alcance:** Dashboard exclusivamente; no se modifica ERP funcional, `PROGRESS.html` ni progreso manual.

## Convenciones de este documento

- IDs correlativos (`DH-001`, `DH-002`, …), asignados una sola vez y nunca reutilizados ni renumerados.
- Estados válidos: `Abierto`, `En progreso`, `Resuelto`, `Descartado`.
- Un hallazgo que reaparece tras marcarse `Resuelto` no genera un ID nuevo: se reabre el mismo ID, se actualiza `Estado` a `Abierto` y se agrega una observación con la fecha de reaparición.
- La sección "Dashboard Health Summary" se recalcula en cada entrega que toque este archivo.
