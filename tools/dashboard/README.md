# ZH Engineering Dashboard

**Estado: FROZEN v1.0** (Fase Dashboard 20.0). Este README es la guía operativa (comandos, tabla de analizadores). El contrato técnico completo — fuente de verdad de cada dataset, arquitectura de navegación, reglas y la sección **Mantenimiento** (qué cambios están permitidos a partir de FROZEN) — vive en [`docs/ProgressDashboard/DASHBOARD-CONTRACT.md`](../../docs/ProgressDashboard/DASHBOARD-CONTRACT.md). Ante cualquier contradicción entre este archivo y ese contrato, gana el contrato.

Sistema de "Engineering Intelligence" que genera `docs/ProgressDashboard/index.html`: un reporte técnico que conecta arquitectura real, capacidades de negocio y métricas de ingeniería del ERP.

No confundir con `PROGRESS.html` (raíz del repo) — el mapa maestro visual/arquitectónico, no generado por estos scripts y que estos scripts nunca modifican. Desde Fase Dashboard 13.0, `PROGRESS.html` carga sus datos desde `docs/ProgressDashboard/data/architecture-progress-source.js` (ya no un array embebido a mano).

## Archivo principal

```
tools/dashboard/render-dashboard.ps1
```

Es la **única versión activa**. No crear nuevos scripts de render numerados (`render-dashboard-` + número de versión + `.ps1`) — evolucionar este archivo directamente. El historial completo de versiones anteriores (v2 → v21, incluyendo backups y variantes "final") vive en `tools/dashboard/archive/` con fines de registro; ninguno de esos archivos se ejecuta como parte del flujo actual.

## Flujo de datos

```
Código real del ERP (backend/, frontend/)
        |
        v
build-dashboard-data.ps1 (orquestador de datos, Fase 9.0)
   -- corre los analizadores (tools/dashboard/analyze-*.ps1),
      valida (sin regenerar) semillas + archivos manuales,
      corre el Quality Gate (validate-dashboard.ps1) al final
        |
        v
Modelo JSON (docs/ProgressDashboard/data/*.json)
        |
        v
render-dashboard.ps1
        |
        v
docs/ProgressDashboard/index.html
```

`render-dashboard.ps1` invoca `build-dashboard-data.ps1` automáticamente solo si detecta datos faltantes/inválidos; si todo ya está al día, renderiza directo sin re-correr nada. Detalle completo de qué archivo es semilla/manual/generado/quality-gate: `DASHBOARD-CONTRACT.md` sección 3.

El renderer **nunca infiere ni inventa** relaciones o riesgos — solo agrega y presenta lo que los analizadores ya calcularon con evidencia real (rutas de archivo, conteos de código, coincidencias de grep). Si un dato no existe todavía, el HTML lo dice explícitamente ("No features mapped yet", "N/A - pending") en vez de rellenar con contenido ficticio.

## Analizadores y su salida

| Script | Genera | Fuente de evidencia |
|---|---|---|
| `analyze-backend.ps1`, `analyze-frontend.ps1`, `analyze-tests.ps1`, `analyze-architecture.ps1`, `analyze-dependencies.ps1`, `analyze-database.ps1`, `analyze-migrations.ps1`, `analyze-technical-debt.ps1`, `analyze-security.ps1` | fragmentos consumidos por `build-dashboard-v12.ps1` → `dashboard-model-v12.json` | escaneo directo de `backend/` y `frontend/` |
| `analyze-module-health.ps1` | `module-health.json` | por módulo: cruza `backend-analysis.json` (capas Domain/Application presentes) + `frontend-analysis.json` (¿existe carpeta frontend homónima?) + `tests-analysis.json` para un score simple (40/25/25). **Requerido por `health-score.ps1`** — no es huérfano, debe ejecutarse siempre antes |
| `calculate-engineering-score.ps1`, `quality-gate.ps1`, `health-score.ps1` | secciones `EngineeringScore`/`QualityGate`/`Health` de `dashboard-model-v12.json` | agregación de los analizadores anteriores |
| `Manage-EngineeringHistory.ps1` | `docs/ProgressDashboard/history/dashboard-{timestamp}-v2.json` + `engineering-trend.json` | fusión de `snapshot-dashboard-v2.ps1` (escribe un snapshot por corrida) + `analyze-engineering-trend.ps1` (relee TODOS los snapshots del historial); ambos scripts originales fueron archivados tras validar una corrida completa con el script fusionado (ver "Auditoría de consolidación" más abajo) |
| `analyze-progress-map.ps1` | `architecture-progress.json` | parsea el array `const D = [...]` embebido en `PROGRESS.html` (raíz del repo) y replica en PowerShell sus propias fórmulas (`calcPhase`/`calcStage`/`calcGlobal`/`findPhase`) — extrae, no inventa, el progreso ya curado a mano en el mapa maestro |
| `validate-dashboard-model.ps1` | `model-health.json` | valida integridad referencial del modelo de conocimiento (modules/domains/features/processes/tasks): referencias rotas, evidencia faltante, ítems sin mapear. **No confundir con `module-health.json`** (ver nota más abajo) |
| `analyze-modules.ps1` | `modules.json` | mapea los módulos reales de `Health.value` (dashboard-model-v12.json) a los dominios reales de `domains.json`, con tabla de mapeo explícita; módulos sin dominio de negocio modelado quedan `domainId: "unmapped"` |
| `analyze-features.ps1` | `features.json` | escanea `backend/src/ERP.Application/Modules/*` buscando carpetas `UseCases` (recursivo) o archivos `*Query.cs`/`*Command.cs`; cada feature referencia el archivo/carpeta real que la originó |
| `analyze-processes.ps1` | `processes.json` | define procesos de negocio (Venta, Compra) como secuencia de pasos; cada paso se marca `verified` solo si hay evidencia real (grep) en el código del módulo, si no queda `unmapped` con motivo |
| `analyze-tasks.ps1` | `tasks.json` | deriva tareas reales desde `QualityGate.Warnings`, `TechnicalDebt.LargeFiles` (≥500 líneas, excluyendo migraciones EF), conteos TODO/FIXME/HACK/NotImplemented, y hallazgos de seguridad reales (excluyendo `node_modules`) |
| `analyze-impact.ps1` | `impact.json` | por módulo: cruza `modules.json`/`features.json`/`processes.json` con conteo real de TODO/FIXME/HACK/NotImplementedException (grep en vivo sobre el código), archivos grandes reales, y archivos de `Security.SecretFiles`/`AnonymousFiles` atribuidos por ruta; calcula el nivel de riesgo y la métrica global **Engineering Risk Coverage** |

`layers.json`, `domains.json` y `erp.json` son datos base curados a mano (arquitectura de capas y dominios del ERP) — no los genera ningún analizador automático.

### Nota: `model-health.json` vs `module-health.json`

Nombres casi idénticos, contenido distinto — no confundir:

- **`model-health.json`** (generado por `validate-dashboard-model.ps1`, activo) — integridad referencial del *modelo de conocimiento completo* (¿hay referencias rotas entre modules/domains/features/processes/tasks? ¿falta evidencia?). Consumido por `render-dashboard.ps1` y `analyze-completion.ps1`.
- **`module-health.json`** (generado por `analyze-module-health.ps1`, **activo — restaurado el 2026-07-16**, ver más abajo) — booleanos Domain/Application/Frontend + Score simple por módulo individual. Consumido por `health-score.ps1`.

### Auditoría de limpieza (2026-07-16, primera pasada)

Se movieron a `tools/dashboard/archive/` 19 scripts sin referencias reales en el flujo activo en ese momento (verificado con `grep`, no por suposición): versiones viejas del ensamblador (`build-dashboard.ps1`, `build-dashboard-v2/v3/v7/v9/v10.ps1`, superadas por `build-dashboard-v12.ps1`), `snapshot-dashboard.ps1` (superado por `snapshot-dashboard-v2.ps1`), `compare-dashboard.ps1`, `export-dashboard-report.ps1`, `run-dashboard.ps1` (orquestador viejo, reemplazado por `run-dashboard-final.ps1`), utilidades sueltas sin referencias (`build.ps1`, `init.ps1`, `build-project-tree.ps1`, `generate-mermaid.ps1`, `generate-project-model.ps1`, `manage-dashboard-history.ps1`, `save-dashboard-history.ps1`), y `analyze-git.ps1`/`analyze-module-health.ps1`.

### Auditoría de consolidación (2026-07-16, segunda pasada)

Un análisis posterior, más profundo (verificando `LoadJson` real de cada script, no solo qué lo ejecuta), encontró que **la primera pasada archivó `analyze-module-health.ps1` por error**: `health-score.ps1` sí depende de su salida (`module-health.json`) y su propio `LoadJson` no tolera archivo faltante (`$ErrorActionPreference="Stop"` + sin `Test-Path`) — el pipeline solo seguía funcionando porque quedaba una copia vieja de `module-health.json` en disco desde antes de esa primera pasada. Correcciones aplicadas:

1. **`analyze-module-health.ps1` restaurado** desde `archive/` a activo, y agregado a `run-dashboard-final.ps1` justo antes de `health-score.ps1`.
2. **`snapshot-dashboard-v2.ps1` + `analyze-engineering-trend.ps1` fusionados en `Manage-EngineeringHistory.ps1`**: ambos leían/escribían el mismo directorio (`docs/ProgressDashboard/history/`), uno preparaba los datos del otro, y siempre se ejecutaban consecutivos — no representaban capacidades independientes, sino dos mitades de "registrar y resumir historial". Los dos originales se archivaron **solo después de** validar una corrida completa end-to-end con el script fusionado (snapshot #5 creado correctamente, `engineering-trend.json` con 7 snapshots).
3. **`run-dashboard-final.ps1` ampliado** de 18 a 32 pasos: ahora orquesta también los analizadores de capacidad de negocio, Architecture Intelligence, `analyze-explorer-index.ps1` y `validate-dashboard-model.ps1` — antes había que correrlos manualmente siguiendo el orden documentado en este README. Ahora existe **un solo comando** (`run-dashboard-final.ps1`) que regenera todo el dashboard de punta a punta.

Inventario final: **34 scripts activos**, cada uno con responsabilidad verificada por código (no por documentación), cero dependencias colgantes conocidas.

### Cierre técnico (2026-07-16, tercera pasada)

Auditoría del grafo de dependencias (productor → consumidor de cada JSON) encontró 3 casos con evidencia objetiva de cero consumidores reales. Cambios aplicados:

1. **`analyze-docs.ps1` retirado de `run-dashboard-final.ps1`** — `docs-analysis.json` no tiene ningún consumidor real (`build-dashboard-v12.ps1` nunca lo carga, pese a que este README lo afirmaba antes). El script **no se eliminó ni se modificó**, solo dejó de ejecutarse en la corrida automática; sigue disponible para correrlo manualmente si se conecta a un consumidor futuro.
2. **`analyze-api.ps1` retirado de `run-dashboard-final.ps1`** — mismo caso: `api-analysis.json` sin consumidores, sin plan futuro documentado que lo requiera. Script conservado sin cambios, solo retirado de la orquestación automática.
3. **`Manage-EngineeringHistory.ps1` ya no lee `dashboard-model-v10.json`** ni escribe el campo `Dashboard` en el snapshot — ese archivo no tiene productor activo (huérfano desde una limpieza anterior) y el campo nunca era leído por el cálculo de tendencia ni por ningún otro script. El snapshot ahora guarda únicamente `EngineeringScore`/`Health`/`Security`/`TechnicalDebt`, igual que siempre se usó en la práctica. El cálculo de tendencia (Paso 2 del mismo script) no se tocó.

Ningún script fue eliminado ni renombrado; ninguna carpeta se movió; ningún cálculo, métrica o contrato JSON distinto del campo `Dashboard` cambió.

### Architecture Intelligence (dependencias, critical path, simulación, recomendaciones)

Familia de analizadores nueva, independiente de la anterior, que responde "¿dónde está el mayor riesgo?", "¿qué desbloquea más trabajo?", "¿qué depende de qué?", "¿qué pasa si termino X?" y "¿qué debería hacer el equipo primero?". No modifican ningún JSON/analizador existente; solo agregan lectura.

| Script | Genera | Fuente de evidencia |
|---|---|---|
| `analyze-module-graph.ps1` | `dependencies.json` | escaneo real de `using ERP.(Application\|Domain\|Infrastructure).Modules.*` en los `.cs` de cada módulo (`backend/src/ERP.Application/Modules/*`, `ERP.Domain/Modules/*`, `ERP.Infrastructure/*`); una arista solo cuenta si el nombre referenciado coincide con un id real de `modules.json`. Calcula fan-in/fan-out/coupling/instability/cohesión aproximada/bus factor (autores distintos vía `git log`), ciclos (DFS) y profundidad de dependencias |
| `analyze-critical-path.ps1` | `critical-path.json` | cruza `modules.json` (score = proxy de completitud) con `dependencies.json` (grafo real) e `impact.json`/`tasks.json` (features/procesos/tareas reales) para rankear módulos incompletos por cuántos módulos desbloquean transitivamente |
| `analyze-release-simulation.ps1` | `release-simulation.json` | simulación de solo lectura: aplica las fórmulas exactas de `calculate-engineering-score.ps1` (pesos 0.30/0.20/0.20/0.20/0.10) y de `render-dashboard.ps1` (Production Readiness, bandas de riesgo) sobre valores hipotéticos; nunca escribe sobre datos reales |
| `analyze-recommendations.ps1` | `recommendations.json` | motor de reglas determinístico: cada recomendación cita el campo JSON exacto que la origina (`justifiedBy`); ninguna regla genera texto sin una condición real que la dispare |
| `analyze-navigation-map.ps1` | `navigation-map.json` | resuelve TODA relación que el Architecture Explorer (vista inicial del dashboard) necesita para su drill-down: Layer→Stage (búsqueda de ventana contigua sobre `stages[]`, verificada por igualdad exacta de pct; si es ambigua, cae a una referencia curada leída del código fuente de `analyze-progress-map.ps1`, nunca adivinada), coreModule→Stage/Phase (match único de nombre+pct), coreModule→módulo real (tabla de traducción explícita, validada contra `modules.json`), Layer "web"→Domains (`domains.json[].layer` tal cual), y Layer↔`database-analysis.json`/`migration-analysis.json`/`frontend-analysis.json`/`backend-analysis.json`. El renderer solo lee este JSON — no decide ninguna relación |
| `analyze-dashboard-summary.ps1` | `dashboard-summary.json` | consolida TODA la lógica de cálculo/decisión que antes vivía embebida en `render-dashboard.ps1` (Engineering Confidence ponderado, bandas de riesgo LOW/MEDIUM/HIGH/CRITICAL, Production Decision, Quality Gate Detail, Security Posture, Technical Debt Trend, Release Recommendation, Production Gate, Roadmap, Trend, banderas ejecutivas). Reutiliza (no recalcula) la banda de Production Readiness ya publicada en `completion-intelligence.json`; solo calcula el promedio numérico crudo porque ningún otro archivo lo expone. El renderer quedó como consumidor puro de este JSON para toda esta sección |

**Nota de nombres**: `analyze-module-graph.ps1` es distinto de `tools/dashboard/analyze-dependencies.ps1` (ya existente, detector de dependencias externas prohibidas → `dependency-analysis.json`, consumido por `calculate-engineering-score.ps1`). Ese script no se tocó; el grafo de dependencias entre módulos vive en un archivo nuevo (`dependencies.json`) generado por un script nuevo.

### Orden de ejecución — un solo comando

Desde el 2026-07-16, `run-dashboard-final.ps1` orquesta las 32 pasos completos (analizadores base → capacidad de negocio → Architecture Intelligence → validación → render) en el orden correcto de dependencias. Ya no es necesario correr nada manualmente:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\dashboard\run-dashboard-final.ps1
```

Orden interno real (ver el propio script para la lista exacta y actualizada):

```
analyze-backend -> analyze-frontend -> analyze-tests -> analyze-architecture ->
analyze-dependencies -> analyze-database -> analyze-migrations ->
analyze-technical-debt -> analyze-security -> analyze-module-health -> health-score ->
calculate-engineering-score -> Manage-EngineeringHistory -> quality-gate -> build-dashboard-v12 ->
analyze-progress-map -> analyze-modules -> analyze-features -> analyze-processes -> analyze-tasks ->
validate-dashboard-model -> analyze-impact -> analyze-completion -> analyze-module-graph ->
analyze-critical-path -> analyze-release-simulation -> analyze-recommendations ->
analyze-navigation-map -> analyze-dashboard-summary -> analyze-explorer-index -> render-dashboard
```

Si solo cambiaste código de negocio y quieres una corrida más rápida sin volver a escanear `backend/`/`frontend/` desde cero, puedes ejecutar manualmente solo desde `build-dashboard-v12.ps1` en adelante — pero `run-dashboard-final.ps1` completo es la única forma **garantizada** de estar al día.

## Modelo JSON (`docs/ProgressDashboard/data/`)

| Archivo | Contenido |
|---|---|
| `dashboard-model-v12.json` | Agregado principal: `EngineeringScore`, `QualityGate`, `Trend`, `Health` (módulos + score), `Security`, `TechnicalDebt`, `Architecture`, `Dependencies` |
| `erp.json` | Identidad del ERP (nombre, versión, estado) |
| `layers.json` | Capas del sistema (Web, Mobile, Chat, Intelligence, Core, Database, AI) |
| `domains.json` | Dominios de negocio reales (Sales, Purchases, Inventory, Security, etc.) |
| `modules.json` | Módulo real → dominio real (generado) |
| `features.json` | Módulo → features reales con evidencia de archivo (generado) |
| `processes.json` | Procesos de negocio → pasos verificados/unmapped con evidencia (generado) |
| `tasks.json` | Tareas reales derivadas de quality gate / deuda técnica / seguridad (generado) |
| `impact.json` | Domain → Module → Feature → Process → Risk + `coverage` (Engineering Risk Coverage) (generado) |
| `completion-intelligence.json` | Conclusiones de "qué falta para terminar el ERP" (generado por `analyze-completion.ps1`) |
| `dependencies.json` | Grafo real de dependencias entre módulos + métricas de ingeniería (fan-in/out, coupling, instability, cohesión aprox., bus factor, ciclos, profundidad) (generado por `analyze-module-graph.ps1`) |
| `critical-path.json` | Impacto por módulo (dependientes directos/transitivos, features/procesos/tareas afectados, % del ERP, riesgo) + orden de critical path (generado por `analyze-critical-path.ps1`) |
| `navigation-map.json` | Todas las relaciones Layer→Stage/coreModule/Domain/Database/Frontend/Backend que el Architecture Explorer necesita, ya resueltas y verificadas (generado por `analyze-navigation-map.ps1`) |
| `dashboard-summary.json` | Engineering Confidence, bandas de riesgo, Production Decision, Quality Gate Detail, Security Posture, Technical Debt Trend, Release Recommendation, Production Gate, Roadmap, Trend, banderas ejecutivas — toda la lógica que antes vivía en `render-dashboard.ps1` (generado por `analyze-dashboard-summary.ps1`) |
| `release-simulation.json` | Escenarios "qué pasaría si" (Security/Quality/Fiscal/Accounting/Sales) recalculados con las fórmulas reales, sin tocar datos reales (generado por `analyze-release-simulation.ps1`) |
| `recommendations.json` | Recomendaciones de arquitectura con cita exacta de los datos que las justifican (generado por `analyze-recommendations.ps1`) |

### Datasets manuales y de gobernanza (Fases Dashboard 3.0-14.0, no listados arriba)

Agregados en fases posteriores a la tabla original de este README; contrato completo (campos, formato) en `DASHBOARD-CONTRACT.md` sección 3.2:

| Archivo | Contenido | Origen |
|---|---|---|
| `modules-status.json` | Estado funcional/madurez/freeze por módulo | Manual, curado contra `docs/STATUS.md`/ADRs |
| `roadmap.json` | 7 etapas del Roadmap Maestro | Manual, curado contra `docs/ROADMAP.md` |
| `blockers.json` | Bloqueadores reales del proyecto | Manual |
| `architecture-governance.json` | Estado de gobierno (freeze/accepted/draft) por módulo | Manual |
| `architecture-dependencies.json` | Grafo de dependencias arquitectónicas + ciclos (DFS determinista desde Fase 19.0) | Manual (aristas) + `analyze-module-graph.ps1`-style derivación en `render-dashboard.ps1` |
| `erp-closure.json` | Auditoría de cierre del ERP por módulo (Fase Dashboard 14.0) | Manual |
| `architecture-progress-source.json` (+ espejo `.js`) | Progreso por etapas/fases/tareas — fuente que carga `PROGRESS.html` | Manual |
| `module-coverage-audit.json` | Auditoría de cobertura de módulos reales vs. datasets nombrados (Fase Dashboard 10.0) | Manual — **sin generador ni validación en `build-dashboard-data.ps1`, gap conocido documentado en `DASHBOARD-CONTRACT.md` sección 3.2** |
| `dashboard-validation.json` | Resultado del Quality Gate final | `validate-dashboard.ps1` |

## Generación del HTML

`render-dashboard.ps1` carga los JSON de arriba, calcula únicamente derivaciones de presentación (Engineering Confidence Score ponderado, clasificación LOW/MEDIUM/HIGH/CRITICAL, agregados por dominio) y escribe **una sola vez**, al final:

```powershell
$html | Out-File $Output -Encoding utf8
```

Desde Fase Dashboard 16.0/17.0, el HTML generado organiza sus 36 secciones en **6 categorías de navegación** (Estado General, Módulos y Negocio, Arquitectura, Calidad e Ingeniería, Seguridad y Riesgos, Roadmap), cada una con su propia sub-navegación — un panel visible a la vez. Detalle completo de qué sección vive en qué categoría/sub-categoría: `DASHBOARD-CONTRACT.md` sección 5. Esta navegación está **FROZEN** — no se agregan, mueven ni renombran secciones/categorías sin una decisión explícita (ver `DASHBOARD-CONTRACT.md` sección 9, Mantenimiento).

## Validación

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\dashboard\render-dashboard.ps1
```

Debe terminar con `Dashboard generated successfully.` y escribir `docs\ProgressDashboard\index.html`.

## Mantenimiento (FROZEN v1.0)

Política completa en `DASHBOARD-CONTRACT.md` sección 9. Resumen: se permiten correcciones de bugs reales y datasets/módulos nuevos que reflejen crecimiento real del ERP; no se permiten métricas nuevas, secciones nuevas, cambios de navegación ni rediseños.
