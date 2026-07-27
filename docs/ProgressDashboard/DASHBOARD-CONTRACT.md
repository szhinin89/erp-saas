# Dashboard Contract — ZH Engineering Dashboard

**Estado: FROZEN v1.0** (congelado en Fase Dashboard 20.0). Ver [sección 8](#8-estado-frozen-v10) y [sección 9 — Mantenimiento](#9-mantenimiento) antes de tocar cualquier archivo de este sistema.

Contrato técnico del sistema de dashboard de ingeniería del ERP. Este documento debe permitir a un desarrollador o agente IA nuevo entender y operar el sistema completo **sin leer conversaciones anteriores**.

Si algo en el código contradice este documento, el código gana y este documento debe corregirse — nunca al revés.

---

## 1. Arquitectura del pipeline

```
Código real del ERP (backend/, frontend/)
        |
        v
build-dashboard-data.ps1  (orquestador de DATOS -- Fase Dashboard 9.0)
        |  valida/regenera 29 generadores automatizados
        |  valida (sin regenerar) 3 semillas + 7 archivos manuales
        |  corre validate-dashboard.ps1 (Quality Gate) al final
        v
JSON Data Model  (docs/ProgressDashboard/data/*.json)
        |
        v
render-dashboard.ps1  (único renderer -- lee datos + arma navegacion + escribe HTML)
        |
        v
docs/ProgressDashboard/index.html
```

En paralelo, **`PROGRESS.html`** (raíz del repo) es el mapa maestro de arquitectura — una pieza separada que este pipeline nunca genera ni modifica. Desde Fase Dashboard 13.0, `PROGRESS.html` **no** embebe sus datos en un array JS propio: carga `docs/ProgressDashboard/data/architecture-progress-source.js` (espejo `.js` de `architecture-progress-source.json`, la única fuente de verdad manual del progreso por etapas) vía `<script src>`. `index.html` lo referencia con un enlace relativo (`<a href='../../PROGRESS.html'>`), pero no depende de él para renderizar.

### Dos puntos de entrada válidos

**1) Regenerar todo desde cero** (re-escanea `backend/`/`frontend/`, corre los 29 generadores en orden, valida todo, renderiza):

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\dashboard\run-dashboard-final.ps1
```

**2) Solo renderizar** (camino rápido, el que se usa en el día a día):

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\dashboard\render-dashboard.ps1
```

`render-dashboard.ps1` primero valida sus 24 fuentes de datos declaradas (`$requiredDataFiles`, ver sección 3). Si todas existen, no están vacías y son JSON válido, **no regenera nada** — carga y renderiza directamente (camino usado en la mayoría de las fases de esta bitácora). Si falta o es inválida alguna, invoca automáticamente `build-dashboard-data.ps1` y solo continúa si logra resolverlas todas; si una fuente **manual** sigue faltando después, el pipeline se detiene con un error explícito (no inventa el contenido).

`run-dashboard-final.ps1` y `build-dashboard-data.ps1` corren la misma lista de 29 generadores, en el mismo orden (ver sección 4) — `run-dashboard-final.ps1` además llama a `render-dashboard.ps1` al final; `build-dashboard-data.ps1` en cambio termina con el Quality Gate (`validate-dashboard.ps1`) y nunca renderiza.

---

## 2. Responsabilidad de cada archivo

| Archivo / carpeta | Responsabilidad | Lo que NUNCA debe hacer |
|---|---|---|
| `PROGRESS.html` (raíz del repo) | Mapa maestro de arquitectura: diagrama visual, capas, progreso por etapas. Página estática que carga `architecture-progress-source.js` vía `<script src>`. | Ser generado, sobrescrito o editado por ningún script de `tools/dashboard/`. |
| `tools/dashboard/analyze-*.ps1` | Cada uno escanea una porción real del código (`backend/`, `frontend/`) o del modelo de datos ya generado, y produce un fragmento de evidencia real. | Inventar datos que no salgan de una fuente real. Escribir HTML. Escribir directamente a `index.html`. |
| `tools/dashboard/build-dashboard-data.ps1` | Orquestador único de la capa de datos (Fase 9.0): corre los 29 generadores en orden, valida (sin regenerar) semillas y archivos manuales, corre el Quality Gate al final. | Renderizar HTML. Regenerar un archivo semilla o manual — si falta, detiene el pipeline y exige creación manual. |
| JSON models (`docs/ProgressDashboard/data/*.json`) | Única fuente de verdad intermedia entre "código real" y "HTML". Cada archivo tiene una fuente de verdad declarada (ver sección 3). | Ser editados a mano con datos inventados (salvo los explícitamente manuales, que sí se editan a mano pero **citando evidencia real**). Ser el destino de escritura del renderer. |
| `tools/dashboard/render-dashboard.ps1` | Único renderer. Lee los JSON, calcula **solo derivaciones de presentación** (bandas de riesgo, agregados, HTML de navegación) y escribe `index.html` **una sola vez** al final (`$html \| Out-File $Output -Encoding utf8`). | Escanear código fuente directamente. Inventar relaciones no presentes en el JSON. Escribir a los archivos de `data/`. Modificar navegación/secciones/cálculos fuera de una fase aprobada (ver Mantenimiento). |
| `docs/ProgressDashboard/index.html` | Salida final, desechable y 100% regenerable — nunca se edita a mano. | Ser tratado como fuente de verdad; siempre se debe regenerar desde el pipeline. |
| `tools/dashboard/validate-dashboard.ps1` | Quality Gate final del pipeline de datos: corre al cierre de `build-dashboard-data.ps1`, escribe `dashboard-validation.json`. Si detecta error crítico, detiene el pipeline. | Modificar ningún otro archivo de datos. |
| `tools/dashboard/archive/` | Historial de versiones anteriores del renderer y orquestadores legacy, conservado por trazabilidad. | Ejecutarse como parte del flujo oficial. Recibir nuevas versiones numeradas — ver regla 5.5. |

---

## 3. Fuente de verdad de cada dataset (`docs/ProgressDashboard/data/`)

Clasificación exhaustiva de los 30 archivos que `render-dashboard.ps1` efectivamente carga (`LoadJson`/lectura directa), agrupados por su fuente de verdad real. Cualquier otro `.json` presente en esa carpeta y no listado aquí es **legado sin consumidor activo** (ver nota al final de esta sección).

### 3.1 — Datos semilla estáticos (3) — sin generador, por diseño

Curados a mano una sola vez; ningún script los reescribe. `build-dashboard-data.ps1` los valida, nunca los regenera.

| Archivo | Contenido |
|---|---|
| `erp.json` | Identidad del ERP (nombre, versión, estado) |
| `layers.json` | Capas del sistema (7: Web, Mobile, Chat, Intelligence, Core, Database, AI) |
| `domains.json` | Dominios de negocio reales (11: Sales, Purchases, Inventory, Security, etc.) |

### 3.2 — Archivos mantenidos manualmente (8) — sin generador, por diseño

Cada uno declara en su propio campo `method`/`source` que es "revisión manual, no analizador automatizado". Escribir un generador heurístico para estos violaría el propósito explícito de las fases que los crearon (evitar que una heurística reinvente investigación ya citada contra `CLAUDE.md`/`STATUS.md`/`ROADMAP.md`/ADRs). Si falta alguno, el pipeline se detiene y exige creación manual citando evidencia real — nunca se inventa.

| Archivo | Fuente de verdad | Validado por |
|---|---|---|
| `modules-status.json` | Estado funcional/madurez/freeze de cada módulo, curado contra `docs/STATUS.md`/ADRs | `build-dashboard-data.ps1` (`$manualFiles`) |
| `roadmap.json` | 7 etapas del Roadmap Maestro, curado contra `docs/ROADMAP.md` | `build-dashboard-data.ps1` (`$manualFiles`) |
| `blockers.json` | Bloqueadores reales, cada uno trazable a un campo `bloqueadores` de `roadmap.json`/`docs/ROADMAP.md` | `build-dashboard-data.ps1` (`$manualFiles`) |
| `architecture-governance.json` | Estado de gobierno (freeze/accepted/draft) por módulo | `build-dashboard-data.ps1` (`$manualFiles`) |
| `architecture-dependencies.json` | 89 aristas derivadas mecánicamente de `explorer-index.json`, con clasificación heurística de dominio documentada en su propio campo `method` | `build-dashboard-data.ps1` (`$manualFiles`) |
| `erp-closure.json` | Reestructuración a JSON de la Auditoría Técnica del ERP y su Plan Maestro (Fase Dashboard 14.0) | `build-dashboard-data.ps1` (`$manualFiles`) |
| `architecture-progress-source.json` | Único lugar donde se mantiene a mano el progreso por etapas/fases/tareas (reemplaza el antiguo array embebido en `PROGRESS.html`) | `build-dashboard-data.ps1`, **antes** de correr los generadores (`analyze-progress-map.ps1` depende de que exista) |
| `module-coverage-audit.json` | Comparación mecánica de sets de IDs de módulo entre datasets de gobernanza, contra `backend/src/ERP.Domain/Modules/` (Fase Dashboard 10.0) | ⚠️ **Gap conocido, documentado, no corregido en Fase 20.0** — no está en `$requiredDataFiles` de `render-dashboard.ps1` ni en `$manualFiles`/`$seedFiles` de `build-dashboard-data.ps1`. Si falta, `render-dashboard.ps1` falla con un error genérico de `LoadJson` en vez del mensaje guiado que reciben los otros 7. Corregirlo es un cambio de pipeline — queda fuera del alcance de esta fase (solo documentación) |

### 3.3 — Generados automáticamente (29) — vía `build-dashboard-data.ps1`

Mismo orden en que corren `run-dashboard-final.ps1` y `build-dashboard-data.ps1`:

| # | Script | Genera | Fuente de evidencia |
|---|---|---|---|
| 1 | `analyze-backend.ps1` | `backend-analysis.json` | escaneo real de `backend/` |
| 2 | `analyze-frontend.ps1` | `frontend-analysis.json` | escaneo real de `frontend/` |
| 3 | `analyze-tests.ps1` | `tests-analysis.json` | conteo real de archivos de test |
| 4 | `analyze-architecture.ps1` | `architecture-analysis.json` | escaneo de capas/violaciones |
| 5 | `analyze-dependencies.ps1` | `dependency-analysis.json` | detector de dependencias externas prohibidas (distinto de `analyze-module-graph.ps1`, ver nota) |
| 6 | `analyze-database.ps1` | `database-analysis.json` | escaneo de `DbContext`/`DbSet`/migraciones |
| 7 | `analyze-migrations.ps1` | `migration-analysis.json` | resumen de migraciones EF Core |
| 8 | `analyze-technical-debt.ps1` | `technical-debt.json` | TODO/FIXME/HACK/NotImplemented + archivos grandes |
| 9 | `analyze-security.ps1` | `security-analysis.json` | secretos/anónimos/connection strings detectados |
| 10 | `analyze-module-health.ps1` | `module-health.json` | booleanos Domain/Application/Frontend + score simple por módulo |
| 11 | `health-score.ps1` | `health-score.json` | agregación de análisis anteriores |
| 12 | `calculate-engineering-score.ps1` | `engineering-score.json` | score compuesto ponderado |
| 13 | `Manage-EngineeringHistory.ps1` | `engineering-trend.json` + snapshot en `history/` | fusión de snapshot + relectura de histórico |
| 14 | `quality-gate.ps1` | `quality-gate.json` | checks de build/test/coverage/static analysis |
| 15 | `build-dashboard-v12.ps1` | `dashboard-model-v12.json` | ensambla los fragmentos 1-14 en el modelo consolidado activo |
| 16 | `analyze-progress-map.ps1` | `architecture-progress.json` | lee `architecture-progress-source.json` (manual) y replica sus fórmulas (`calcPhase`/`calcStage`/`calcGlobal`) |
| 17 | `analyze-modules.ps1` | `modules.json` | mapea módulos reales a dominios reales (`domains.json`) |
| 18 | `analyze-features.ps1` | `features.json` | escanea `UseCases`/`*Query.cs`/`*Command.cs` por módulo |
| 19 | `analyze-processes.ps1` | `processes.json` | procesos de negocio verificados por grep contra código real |
| 20 | `analyze-tasks.ps1` | `tasks.json` | deriva tareas desde Quality Gate/deuda técnica/seguridad ya calculados |
| 21 | `validate-dashboard-model.ps1` | `model-health.json` | integridad referencial del modelo (referencias rotas, evidencia faltante) |
| 22 | `analyze-impact.ps1` | `impact.json` | Domain→Module→Feature→Process→Risk + Engineering Risk Coverage |
| 23 | `analyze-completion.ps1` | `completion-intelligence.json` | qué falta para terminar el ERP |
| 24 | `analyze-module-graph.ps1` | `dependencies.json` | grafo real de `using ERP.*.Modules.*`, fan-in/out, ciclos (DFS determinista desde Fase 19.0) |
| 25 | `analyze-critical-path.ps1` | `critical-path.json` | ranking de módulos incompletos por cuánto desbloquean transitivamente |
| 26 | `analyze-release-simulation.ps1` | `release-simulation.json` | escenarios "qué pasaría si", solo lectura |
| 27 | `analyze-recommendations.ps1` | `recommendations.json` | motor de reglas determinístico, cada recomendación cita su dato origen |
| 28 | `analyze-navigation-map.ps1` | `navigation-map.json` | relaciones Layer→Stage/Domain que usa el Architecture Explorer (diagrama interactivo, no la navegación de pestañas) |
| 29 | `analyze-dashboard-summary.ps1` | `dashboard-summary.json` | Engineering Confidence, bandas de riesgo, Production Decision, Quality Gate Detail |
| — | `analyze-explorer-index.ps1` | `explorer-index.json` | índice inverso consolidado (módulos, `reverseFileIndex`, `searchEntries`) — corre al final de la lista de generadores |

**Nota de nombres**: `analyze-dependencies.ps1` (#5, `dependency-analysis.json`) es distinto de `analyze-module-graph.ps1` (#24, `dependencies.json`) — mismo prefijo, propósito y salida completamente distintos.

### 3.4 — Quality Gate (1)

| Archivo | Generado por | Cuándo corre |
|---|---|---|
| `dashboard-validation.json` | `validate-dashboard.ps1` | al final de `build-dashboard-data.ps1`, después de validar los 29+3+8 archivos anteriores. Si detecta un error crítico, detiene el pipeline completo |

### 3.5 — Legado sin consumidor activo (no forma parte del contrato)

Confirmado por ausencia en la lista `LoadJson` de `render-dashboard.ps1` (auditorías de Fases 16.0/18.0): `dashboard-model.json`, `dashboard-model-v7.json`, `dashboard-model-v9.json`, `dashboard-model-v10.json` (superados por `dashboard-model-v12.json`), `api-analysis.json`/`analyze-api.ps1`, `docs-analysis.json`/`analyze-docs.ps1` (ambos scripts retirados de la orquestación automática, no eliminados), `risks.json`, `history.json`, `production.json` (placeholders vacíos), `project-model.json`, `project-tree.json`, `dashboard-diff.json`, `dashboard-state.json`, `git-analysis.json`, `metrics.json`, `history-retention.json`. Ninguno se borra en esta fase (fuera de alcance — solo documentación) ni en ninguna fase de hardening anterior; se documentan para que un mantenedor futuro no los confunda con datasets activos.

---

## 4. Dependencias entre scripts (orden real de ejecución)

```
analyze-backend → analyze-frontend → analyze-tests → analyze-architecture →
analyze-dependencies → analyze-database → analyze-migrations →
analyze-technical-debt → analyze-security → analyze-module-health → health-score →
calculate-engineering-score → Manage-EngineeringHistory → quality-gate → build-dashboard-v12 →
analyze-progress-map → analyze-modules → analyze-features → analyze-processes → analyze-tasks →
validate-dashboard-model → analyze-impact → analyze-completion → analyze-module-graph →
analyze-critical-path → analyze-release-simulation → analyze-recommendations →
analyze-navigation-map → analyze-dashboard-summary → analyze-explorer-index
        ↓
validate-dashboard.ps1  (Quality Gate — solo en build-dashboard-data.ps1, run-dashboard-final.ps1 NO lo corre)
        ↓
render-dashboard.ps1  (NO en build-dashboard-data.ps1; SÍ al final de run-dashboard-final.ps1; se puede correr solo)
```

Cada paso depende únicamente de las salidas de los pasos anteriores en esta lista (más los datos semilla/manuales, disponibles desde el inicio). No hay dependencias circulares. Fuente única de esta lista: el propio array `$generators` de `build-dashboard-data.ps1` — este documento no la reinventa, la refleja.

---

## 5. Arquitectura de navegación (congelada en Fases 15.0-17.0)

`index.html` es un portal de **6 categorías principales**, cada una con su propia barra de navegación secundaria (sub-pestañas) — **un solo panel visible a la vez** por categoría. 36 secciones totales, ninguna duplicada, ninguna movida fuera de su categoría desde Fase 17.0.

| Categoría (`data-group`) | Sub-navegación (`data-subgroup`) | Responde a |
|---|---|---|
| **Estado General** (`home`) — landing por defecto | KPIs · Executive Dashboard · Global Status · Production Decision | ¿Cuál es el estado del ERP? |
| **Módulos y Negocio** (`business`) | Business Capability · Madurez · Cierre ERP | ¿Qué módulos existen? |
| **Arquitectura** (`architecture`) — incluye el diagrama interactivo (Architecture Explorer) | Resumen · Dependencias · Explorer · ADR · Progreso | ¿Cómo está la arquitectura? |
| **Calidad e Ingeniería** (`engineering`) | Resumen · Quality Gate · Coverage · Technical Debt | ¿Cómo está la calidad? |
| **Seguridad y Riesgos** (`security`) | Riesgos · Release · Seguridad | — |
| **Roadmap** (`roadmap`) | Roadmap · Hitos · Ruta · Cierre ERP (atajo cruzado a Módulos, no duplica el panel) | ¿Qué debo desarrollar ahora? |

Mecánica (JS embebido en `render-dashboard.ps1`, sección `$jsHtml`): `showGroup(name)` activa una categoría y su primer sub-grupo (recordado en `ACTIVE_SUBGROUP`/`DEFAULT_SUBGROUP`); `showSubGroup(group, sub)` filtra, dentro de esa categoría, qué `<section data-subgroup=...>` queda visible. Ningún panel vive fuera de su `data-group`/`data-subgroup` — verificado sin huérfanos en Fase 18.0.

---

## 6. Flujo completo del Dashboard (código → HTML → usuario)

```
1. Código real del ERP cambia (backend/ o frontend/)
        ↓
2. build-dashboard-data.ps1 (o run-dashboard-final.ps1)
   - 29 generadores re-escanean el código real
   - 3 semillas + 8 manuales se validan (nunca se regeneran)
   - validate-dashboard.ps1 corre el Quality Gate
        ↓
3. docs/ProgressDashboard/data/*.json queda actualizado
        ↓
4. render-dashboard.ps1
   - Carga los 30 JSON (sección 3)
   - Calcula únicamente derivaciones de presentación (bandas de riesgo,
     agregados, HTML de cada sección)
   - Arma la navegación (6 categorías × subnav, sección 5)
   - Escribe docs/ProgressDashboard/index.html UNA sola vez, al final
        ↓
5. Un desarrollador o agente abre index.html
   - Aterriza en "Estado General" (landing por defecto)
   - Navega por categoría → sub-categoría, un panel a la vez
   - El buscador embebido (SEARCH_INDEX) salta directo a cualquier
     módulo/dominio/feature/proceso/archivo sin pasar por la navegación manual
```

Si solo cambió código de negocio sin afectar módulos/dominios/features/procesos, basta con `run-dashboard-final.ps1`. Si se agregó un módulo/dominio/relación nueva que requiera actualizar un archivo **manual** (sección 3.2), ese archivo se edita a mano primero, citando evidencia real, y luego se corre el pipeline.

---

## 7. Reglas

### 7.1 — No inventar datos
Todo valor en cualquier JSON debe originarse en: (a) un archivo real del repositorio (ruta verificable), (b) un conteo/grep ejecutado en el momento sobre código real, (c) otro JSON del mismo pipeline ya validado bajo esta misma regla, o (d) para los 8 archivos manuales (sección 3.2), investigación citada contra `CLAUDE.md`/`STATUS.md`/`ROADMAP.md`/ADRs. Ningún analizador ni el renderer puede escribir un valor "razonable" o "de ejemplo" para rellenar un hueco.

### 7.2 — Toda relación requiere evidencia
Cualquier relación Módulo↔Dominio, Feature↔Módulo, Proceso↔Módulo debe declarar su evidencia. Si no existe, se marca explícitamente `"unmapped"`/`"pending"` con razón documentada.

### 7.3 — El renderer solamente consume datos
`render-dashboard.ps1` no escanea `backend/`/`frontend/`, no hace `grep` de código de negocio, no calcula relaciones Módulo↔Dominio↔Feature↔Proceso↔Riesgo. Sí calcula: fórmulas de presentación puramente aritméticas, bandas LOW/MEDIUM/HIGH/CRITICAL, y desde Fase 15.0, la estructura de navegación (agrupación, no relación de negocio).

### 7.4 — `PROGRESS.html` es mapa maestro
Ningún script de `tools/dashboard/` genera, sobrescribe ni edita `PROGRESS.html`. Puede enlazarlo, nunca reemplazarlo ni duplicar su contenido.

### 7.5 — `render-dashboard.ps1` es el único renderer
No crear `render-dashboard-vNN.ps1` nuevos. Evolucionar el archivo activo directamente; snapshots grandes van a `tools/dashboard/archive/`.

### 7.6 — Orden determinista obligatorio en cualquier recorrido de grafo/colección (desde Fase 19.0)
Cualquier código que enumere un `Hashtable`/`Dictionary` no ordenado (`.Keys`, `.Values`, `GetEnumerator()`) para producir contenido visible en `index.html` debe, en cambio, enumerar una colección con orden garantizado (`[ordered]@{}`, array ya ordenado explícitamente, o `Sort-Object` antes de construir la estructura). .NET aleatoriza el hash de strings por proceso — un `@{}` plano nunca garantiza el mismo orden entre corridas. Ver Fase Dashboard 19.0 (`Find-DependencyCycles`) como precedente y patrón a replicar si aparece un caso nuevo.

---

## 8. Estado FROZEN v1.0

Declarado en **Fase Dashboard 20.0**, tras el cierre acumulado de las Fases 15.0-19.0:

| Fase | Qué cerró |
|---|---|
| 15.0 | Diseño de las 6 categorías de navegación (análisis, sin código) |
| 16.0 | Implementación: Home + 6 categorías, `data-group` por sección |
| 17.0 | Navegación secundaria: subnav por categoría, `data-subgroup` |
| 18.0 | Hardening: 0 IDs duplicados, 0 huérfanos, 0 código muerto, 0 errores HTML/JS detectables |
| 19.0 | Determinismo: detección de ciclos de dependencias 100% reproducible (verificado con hash SHA-256 idéntico en 3 corridas consecutivas) |
| 20.0 | Esta documentación — cierre formal |

**Lo que queda FROZEN**: 36 secciones, 6 categorías de navegación con sub-navegación, todos los cálculos y fórmulas de presentación existentes, el pipeline de 29 generadores + 8 manuales + 3 semillas + 1 Quality Gate, el orden de ejecución de la sección 4.

**Lo que NO está congelado**: el *contenido* de los datasets (números, porcentajes, listas) — esos cambian cada vez que se corre el pipeline sobre código real actualizado, por diseño. Congelar el Dashboard significa congelar su *estructura*, no sus mediciones.

---

## 9. Mantenimiento

A partir de FROZEN v1.0, cualquier cambio a este sistema se evalúa contra esta lista antes de tocar código:

### Permitido

- **Corrección de bugs reales** — con reproducción, causa raíz identificada, y evidencia de que el comportamiento actual es incorrecto (no solo "podría ser mejor"). Mismo estándar que otras infraestructuras CLOSED del repo (ver `CLAUDE.md`).
- **Nuevos módulos del ERP** — cuando el ERP agrega un módulo real, los datasets que lo modelan (`modules-status.json`, `architecture-governance.json`, etc.) se actualizan para incluirlo, siguiendo el patrón ya documentado en sección 3.2/3.3. Esto **no** es una excepción a "no nuevas secciones" — es mantener datasets existentes al día con la realidad del ERP.
- **Nuevos datasets cuando aparecen nuevos módulos** — si un módulo nuevo requiere un dato que ningún dataset actual captura, se agrega **como fila nueva de un dataset existente** o, si es estructuralmente distinto, como un dataset nuevo consumido por una sección **ya existente** (nunca para crear una sección nueva).

### No permitido

- **Nuevas métricas.**
- **Nuevas secciones** (las 36 actuales son el universo cerrado).
- **Cambios de navegación** (las 6 categorías y su subnav, cerrados en Fases 16.0/17.0).
- **Rediseños** de HTML/CSS/JS más allá de lo estrictamente necesario para un bug fix real.

Cualquier necesidad real que no encaje en "Permitido" requiere una decisión explícita del usuario antes de implementarse — igual que el resto de infraestructuras CLOSED del repositorio (ver `CLAUDE.md`, "Infraestructuras CLOSED — Regla General de Gobernanza").

---

## Referencias

- Guía operativa completa (comandos, tabla de analizadores, historial de auditorías de limpieza): [`tools/dashboard/README.md`](../../tools/dashboard/README.md)
- Modelo de datos principal de métricas: `docs/ProgressDashboard/data/dashboard-model-v12.json`
- Renderer activo: `tools/dashboard/render-dashboard.ps1`
- Orquestador de datos: `tools/dashboard/build-dashboard-data.ps1`
- Historial de versiones: `tools/dashboard/archive/`
