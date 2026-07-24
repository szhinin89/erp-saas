# Dashboard Contract — ZH Engineering Dashboard

Contrato técnico del sistema de dashboard de ingeniería del ERP. Este documento debe permitir a un desarrollador o agente IA nuevo entender y operar el sistema completo **sin leer conversaciones anteriores**.

Si algo en el código contradice este documento, el código gana y este documento debe corregirse — nunca al revés.

---

## 1. Arquitectura del pipeline

```
Código real del ERP (backend/, frontend/)
        |
        v
run-dashboard-final.ps1  (orquestador oficial)
        |
        v
Analyzers  (tools/dashboard/analyze-*.ps1 + build-dashboard-v12.ps1)
        |
        v
JSON Data Model  (docs/ProgressDashboard/data/*.json)
        |
        v
render-dashboard.ps1  (único renderer)
        |
        v
docs/ProgressDashboard/index.html
```

En paralelo, **`PROGRESS.html`** (raíz del repo) es el mapa maestro de arquitectura — una pieza completamente separada, con sus propios datos embebidos en JS, que este pipeline nunca genera ni modifica. `index.html` lo referencia con un enlace relativo (`<a href='../../PROGRESS.html'>`) dentro de la tarjeta "Architecture & Domains", pero no depende de él para renderizar.

### Cómo ejecutar el pipeline completo

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\dashboard\run-dashboard-final.ps1
```

Esto corre, en orden: los analizadores base (`analyze-backend.ps1` … `analyze-security.ps1`), `health-score.ps1`, `calculate-engineering-score.ps1`, `snapshot-dashboard-v2.ps1`, `analyze-engineering-trend.ps1`, `quality-gate.ps1`, `build-dashboard-v12.ps1` (ensambla `dashboard-model-v12.json`) y finalmente `render-dashboard.ps1`.

Los analizadores de capacidad de negocio (`analyze-modules.ps1`, `analyze-features.ps1`, `analyze-processes.ps1`, `analyze-tasks.ps1`, `analyze-impact.ps1`) **no** están incluidos todavía en `run-dashboard-final.ps1` — se corren aparte, en este orden, antes de `render-dashboard.ps1` (que sí depende de sus salidas):

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\dashboard\analyze-modules.ps1
powershell -ExecutionPolicy Bypass -File .\tools\dashboard\analyze-features.ps1
powershell -ExecutionPolicy Bypass -File .\tools\dashboard\analyze-processes.ps1
powershell -ExecutionPolicy Bypass -File .\tools\dashboard\analyze-tasks.ps1
powershell -ExecutionPolicy Bypass -File .\tools\dashboard\analyze-impact.ps1
powershell -ExecutionPolicy Bypass -File .\tools\dashboard\render-dashboard.ps1
```

---

## 2. Responsabilidad de cada archivo

| Archivo / carpeta | Responsabilidad | Lo que NUNCA debe hacer |
|---|---|---|
| `PROGRESS.html` (raíz del repo) | Mapa maestro de arquitectura: diagrama visual, capas, progreso por etapas. Página estática independiente con datos embebidos en un array JS (`const D = [...]`). | Ser generado, sobrescrito o editado por ningún script de `tools/dashboard/`. |
| `tools/dashboard/analyze-*.ps1` | Cada uno escanea una porción real del código (`backend/`, `frontend/`) o del modelo de datos ya generado, y produce un fragmento de evidencia real (conteo, lista de archivos, relación módulo↔dominio, etc.). | Inventar datos que no salgan de una fuente real. Escribir HTML. Escribir directamente a `index.html`. |
| `tools/dashboard/build-dashboard-v12.ps1` | Ensambla los fragmentos de los analizadores base en `dashboard-model-v12.json`. | Calcular métricas de negocio (features/procesos/riesgo) — eso es responsabilidad de los analizadores de capacidad de negocio. |
| JSON models (`docs/ProgressDashboard/data/*.json`) | Única fuente de verdad intermedia entre "código real" y "HTML". Cada archivo es la salida de un analizador específico (ver tabla de contratos, sección 3). | Ser editados a mano con datos inventados. Ser el destino de escritura del renderer. |
| `tools/dashboard/render-dashboard.ps1` | Único renderer. Lee los JSON, calcula **solo derivaciones de presentación** (Engineering Confidence ponderado, clasificación LOW/MEDIUM/HIGH/CRITICAL, agregados por dominio) y escribe `index.html` **una sola vez** al final (`$html | Out-File $Output -Encoding utf8`). | Escanear código fuente directamente. Inventar relaciones no presentes en el JSON. Escribir a los archivos de `data/`. |
| `docs/ProgressDashboard/index.html` | Salida final, desechable y 100% regenerable — nunca se edita a mano. | Ser tratado como fuente de verdad; siempre se debe regenerar desde el pipeline. |
| `tools/dashboard/archive/` | Historial de versiones anteriores del renderer (v2 → v21) y de orquestadores legacy (`run-dashboard-v2.ps1` … `run-dashboard-v9.ps1`), conservado por trazabilidad. | Ejecutarse como parte del flujo oficial. Recibir nuevas versiones numeradas — ver regla 4.5. |

---

## 3. Contrato de los JSON de capacidad de negocio

Estos 6 archivos (más `impact.json`, generado en la misma familia aunque no listado originalmente) forman el modelo `Domain → Module → Feature → Process → Risk`. `layers.json`, `domains.json` y `erp.json` son datos base curados a mano (arquitectura de capas y dominios reales del ERP); el resto los genera un analizador.

### `layers.json` — curado a mano
Array de capas del sistema.
```json
{ "id": "web", "name": "Web ERP", "icon": "globe", "order": 1 }
```
| Campo | Tipo | Obligatorio |
|---|---|---|
| `id` | string, único | sí |
| `name` | string | sí |
| `icon` | string (nombre de ícono) | sí |
| `order` | number | sí |

### `domains.json` — curado a mano
Array de dominios de negocio reales del ERP.
```json
{ "id": "sales", "name": "Sales", "layer": "web" }
```
| Campo | Tipo | Obligatorio |
|---|---|---|
| `id` | string, único | sí |
| `name` | string | sí |
| `layer` | string, debe existir en `layers.json[].id` | sí |

### `modules.json` — generado por `analyze-modules.ps1`
Módulo real (de `dashboard-model-v12.json` → `Health.value`) mapeado a un dominio real, vía tabla de mapeo explícita en el analizador.
```json
{
  "id": "Sales",
  "domainId": "sales",
  "score": 81.5,
  "architecture": 90, "tests": 80, "documentation": 70, "backend": 90, "frontend": 70
}
```
| Campo | Tipo | Obligatorio | Notas |
|---|---|---|---|
| `id` | string, único | sí | Debe coincidir exactamente con `Health.value[].Module` en `dashboard-model-v12.json` |
| `domainId` | string | sí | `"unmapped"` si el módulo no tiene dominio de negocio modelado todavía — nunca se fuerza a un dominio incorrecto |
| `score`, `architecture`, `tests`, `documentation`, `backend`, `frontend` | number | sí | Copiados 1:1 del módulo real en `Health.value` |

### `features.json` — generado por `analyze-features.ps1`
Módulo → lista de features reales, cada una con evidencia de archivo/carpeta real.
```json
{
  "module": "Sales",
  "features": [
    { "name": "Authorize Sales", "status": "implemented", "evidence": ["backend/src/ERP.Application/Modules/Sales/UseCases/AuthorizeSalesUseCases.cs"] }
  ]
}
```
Si no se encontró evidencia real (módulo solo-de-dominio, o sin capa Application), la entrada usa esta forma en lugar de `features` poblado:
```json
{ "module": "Menu", "features": [], "pending": true, "reason": "Domain-only catalog module (no Application UseCases layer yet)", "evidence": ["backend/src/ERP.Domain/Modules/Menu"] }
```
| Campo | Tipo | Obligatorio |
|---|---|---|
| `module` | string, debe existir en `modules.json[].id` | sí |
| `features[].name` | string | si `features` no está vacío |
| `features[].status` | string, hoy siempre `"implemented"` (solo se registra lo que existe en código) | sí |
| `features[].evidence` | array de rutas relativas reales | sí, al menos 1 elemento |
| `pending`, `reason` | boolean / string | solo cuando `features` está vacío |

### `processes.json` — generado por `analyze-processes.ps1`
Proceso de negocio declarado como secuencia de pasos; cada paso se verifica contra el código real (grep) antes de marcarse `verified`.
```json
{
  "process": "Venta",
  "steps": [
    { "name": "Customer", "module": "Sales", "status": "verified", "evidence": ["backend/src/ERP.Application/Modules/Sales/SalesMapper.cs"] }
  ]
}
```
| Campo | Tipo | Obligatorio |
|---|---|---|
| `process` | string | sí |
| `steps[].name` | string | sí |
| `steps[].module` | string, debe existir en `modules.json[].id` | sí |
| `steps[].status` | `"verified"` o `"unmapped"` | sí |
| `steps[].evidence` | array de rutas reales | solo si `status == "verified"` |
| `steps[].reason` | string | solo si `status == "unmapped"` |

### `tasks.json` — generado por `analyze-tasks.ps1`
Tareas reales derivadas de señales ya calculadas (quality gate, deuda técnica, seguridad) — nunca inventadas por ítem individual cuando el modelo solo tiene un conteo agregado.
```json
{ "task": "Reduce critical findings (currently 31)", "category": "Technical Debt", "priority": "HIGH", "source": "TechnicalDebt.LargeFiles", "evidence": ["backend/src/.../ArchivoGrande.cs"] }
```
| Campo | Tipo | Obligatorio |
|---|---|---|
| `task` | string | sí |
| `category` | string (`"Quality Gate"`, `"Technical Debt"`, `"Security"`) | sí |
| `priority` | `"LOW"` \| `"MEDIUM"` \| `"HIGH"` | sí |
| `source` | string, referencia al campo real de `dashboard-model-v12.json` que originó la tarea | sí |
| `evidence` | array de rutas reales, puede ser `[]` cuando el `source` es un conteo agregado sin desglose por archivo | sí |

### `impact.json` — generado por `analyze-impact.ps1`
Domain → Module → Feature (conteo) → Process (con riesgo) → Risk, más la métrica global de cobertura.
```json
{
  "generated": "2026-07-16 00:57:17",
  "coverage": { "mappedFeaturePoints": 19, "totalFeaturePoints": 146, "percentage": 13.01 },
  "domains": [
    {
      "domain": "Sales",
      "modules": [
        {
          "name": "Sales", "score": 81.5, "features": 9,
          "processes": [ { "name": "Venta", "status": "verified", "risk": "LOW" } ],
          "risks": ["1 large file(s) (>=500 lines)"],
          "risk": "MEDIUM"
        }
      ]
    }
  ]
}
```
| Campo | Tipo | Obligatorio |
|---|---|---|
| `coverage.mappedFeaturePoints` | number | sí — suma de `features` de módulos con ≥1 proceso verificado |
| `coverage.totalFeaturePoints` | number | sí — suma de `features` de todos los módulos |
| `coverage.percentage` | number | sí — `mappedFeaturePoints / totalFeaturePoints * 100`, redondeado a 2 decimales |
| `domains[].modules[].risks` | array de strings | sí, puede ser `[]`; cada string debe originarse en una señal real (ver regla 4.1) |
| `domains[].modules[].risk` | `"LOW"` \| `"MEDIUM"` \| `"HIGH"` \| `"CRITICAL"` | sí |

---

## 4. Reglas

### 4.1 — No inventar datos
Todo valor en cualquier JSON debe originarse en: (a) un archivo real del repositorio (ruta verificable), (b) un conteo/grep ejecutado en el momento sobre código real, o (c) otro JSON del mismo pipeline ya validado bajo esta misma regla. Ningún analizador ni el renderer puede escribir un valor "razonable" o "de ejemplo" para rellenar un hueco.

### 4.2 — Toda relación requiere evidencia
Cualquier relación Módulo↔Dominio, Feature↔Módulo, Proceso↔Módulo debe declarar su evidencia (ruta de archivo real, o resultado de grep). Si la evidencia no existe, la relación se marca explícitamente como `"unmapped"` / `"pending"` con una razón documentada — nunca se omite en silencio ni se fuerza una relación incorrecta para "llenar" el dashboard.

### 4.3 — El renderer solamente consume datos
`render-dashboard.ps1` no escanea `backend/` ni `frontend/`, no hace `grep`, no calcula relaciones Módulo↔Dominio↔Feature↔Proceso↔Riesgo. Esas relaciones ya vienen resueltas en los JSON. El renderer solo hace: cargar JSON, calcular fórmulas de presentación puramente aritméticas (ponderaciones, umbrales LOW/MEDIUM/HIGH/CRITICAL, sumas/promedios de campos ya existentes), y construir el HTML.

### 4.4 — `PROGRESS.html` es mapa maestro
Ningún script de `tools/dashboard/` genera, sobrescribe ni edita `PROGRESS.html`. Es una pieza independiente mantenida aparte. `index.html` puede enlazarlo, nunca reemplazarlo ni duplicar su contenido.

### 4.5 — `render-dashboard.ps1` es el único renderer
No crear `render-dashboard-vNN.ps1` nuevos. Evolucionar `render-dashboard.ps1` directamente. Si se necesita un punto de referencia grande antes de un cambio mayor, copiar un snapshot a `tools/dashboard/archive/` — pero el archivo activo en `tools/dashboard/` sigue siendo uno solo.

---

## 5. Flujo de mantenimiento

```
Código del ERP cambia (backend/ o frontend/)
        ↓
Ejecutar los analizadores afectados
  (run-dashboard-final.ps1 para métricas base;
   analyze-modules/features/processes/tasks/impact.ps1
   si cambió algo que afecte dominios, features o procesos)
        ↓
Los JSON en docs/ProgressDashboard/data/ se actualizan
        ↓
Ejecutar render-dashboard.ps1
        ↓
docs/ProgressDashboard/index.html queda regenerado
```

Si solo cambió código de negocio sin afectar la estructura de módulos/dominios/features/procesos, basta con `run-dashboard-final.ps1` (que ya incluye `render-dashboard.ps1` al final). Si se agregó un módulo, un dominio, o una relación nueva, hay que correr también los analizadores de capacidad de negocio en el orden de la sección 1 antes de renderizar.

### Restricciones permanentes de este flujo

- **No modificar código funcional del ERP** para "hacer que el dashboard se vea mejor". Si un módulo tiene score bajo o riesgo HIGH, el dashboard debe reportarlo — no se ajusta el código del ERP ni los umbrales del dashboard para ocultar un problema real.
- **No modificar `PROGRESS.html`.**
- **No crear nuevas versiones `render-dashboard-vNN.ps1`.**

---

## Referencias

- Guía operativa completa (comandos, tabla de analizadores, orden de ejecución): [`tools/dashboard/README.md`](../../tools/dashboard/README.md)
- Modelo de datos principal de métricas: `docs/ProgressDashboard/data/dashboard-model-v12.json`
- Renderer activo: `tools/dashboard/render-dashboard.ps1`
- Historial de versiones: `tools/dashboard/archive/`
