# Architecture enforcement (Node.js ESM)

Validaciones ejecutables de reglas documentadas en [`AI-RULES/`](../../AI-RULES/README.md). Complementan guardrails PowerShell y NetArchTest. **Cero impacto runtime** — solo CI y desarrollo local.

Rationale histórico: [`docs/adr/`](../../docs/adr/README.md) · CI authority: [`AI-RULES/AGENT-COMPATIBILITY.md`](../../AI-RULES/AGENT-COMPATIBILITY.md#ci-authority)

## Ejecución

Desde la raíz:

```bash
node tools/architecture/run-all.mjs
node tools/architecture/run-all.mjs --json
node tools/architecture/run-all.mjs --annotate   # GitHub ::error/::warning
node tools/architecture/run-all.mjs --only backend-layering
```

Desde `frontend/`:

```bash
npm run architecture:check      # 9 checks + score + report + annotations en CI
npm run architecture:backend    # solo checks .NET
npm run architecture:report     # regenera architecture-report.json
```

## Salida unificada

Cada ejecución de `run-all.mjs` produce:

1. **Consola** — `[PASS]` / `[FAIL]` / `[WARN]` por check + score
2. **JSON** — `tools/architecture/architecture-report.json`
3. **GitHub annotations** — cuando `GITHUB_ACTIONS=true` o `--annotate`

Ejemplo consola:

```
[PASS] pages-wrapper
[WARN] backend-controller-thin
  ~ backend/src/ERP.API/Controllers/AccessController.cs
    B-controller-warn: controller has 381 lines (warning threshold 150)

Architecture checks OK (9/9 passed).
Warnings: 13 (score impact only).
Architecture score: 74/100 (warning, drift: medium)
```

Ejemplo anotación GitHub:

```
::warning file=backend/src/ERP.API/Controllers/AccessController.cs::B-controller-warn: controller has 381 lines (warning threshold 150)
```

## Checks

### Frontend

| Script | Regla | Qué valida |
|--------|-------|------------|
| `check-pages-wrapper.mjs` | PR-6 | `frontend/src/pages/**/*.tsx` — ≤15 líneas, sin hooks/api |
| `check-import-boundaries.mjs` | F-import | Imports prohibidos en pages/modules |
| `check-module-boundaries.mjs` | F-module | Cross-imports entre módulos (configurable) |
| `check-css-prefixes.mjs` | F-css | Prefijos por área; clases ambiguas |
| `check-design-system.mjs` | F-04 | Design System único (grid/toggle/icon/modal/tabs/table/activity) |
| `check-no-cross-layer.mjs` | F-cross-layer | Pages sin fetch/axios/api directo |

### Backend (heurístico, sin Roslyn)

| Script | Regla | Qué valida |
|--------|-------|------------|
| `check-backend-layering.mjs` | B-layering | `.csproj` — refs de proyecto/paquete por capa |
| `check-backend-clean-architecture.mjs` | B-domain/app | `using` prohibidos en Domain/Application |
| `check-backend-controller-thin.mjs` | B-controller | Líneas máx.; EF/SQL inline = error; líneas altas = warning |
| `check-backend-subscriber-rules.mjs` | B-subscriber | `IgnoreQueryFilters()` allowlist; entidades tenant |

## Architecture score

`calculate-score.mjs` + `config/scoring-rules.json` → campos en `architecture-report.json`:

```json
{
  "architectureScore": 74,
  "status": "warning",
  "driftRisk": "medium",
  "summary": { "checksPassed": 9, "violations": 0, "warnings": 13 },
  "modules": { "api-controllers": { "violations": 0, "warnings": 13 } },
  "adrs": ["docs/adr/ADR-001-modular-monolith.md", "..."]
}
```

Estados: `healthy` (≥90), `warning` (≥70), `critical` (<70). Warnings no fallan CI; violations sí.

## Configuración

| Archivo | Propósito |
|---------|-----------|
| `config/architecture-rules.json` | Reglas FE/BE, exemptions, `backend.*`, `adr.index` |
| `config/css-prefixes.json` | Mapa path → prefijos CSS + globales |
| `config/design-system.json` | Clases deprecadas y patrones F-04 (Design System) |
| `config/scoring-rules.json` | Penalizaciones y umbrales de score |
| `architecture-grandfather.json` | Legacy permitido (`backendControllerMaxLines`, `designSystemGrandfathered`, …) |

**Extender reglas:** editar JSON → `npm run architecture:check`. Documentar en [`AI-RULES/ENFORCEMENT.md`](../../AI-RULES/ENFORCEMENT.md).

**Excepciones:** preferir refactor. Si inevitable: `exemptions` o grandfather + ADR.

## Estructura

```
tools/architecture/
├── run-all.mjs              # runner + export runAllChecks()
├── report-json.mjs
├── calculate-score.mjs
├── github-annotations.mjs
├── check-*.mjs              # 5 FE + 4 BE
├── formatters/
│   ├── github-formatter.mjs
│   └── console-formatter.mjs
├── config/
│   ├── architecture-rules.json
│   ├── css-prefixes.json
│   └── scoring-rules.json
└── shared/
    ├── fs-utils.mjs
    ├── report-utils.mjs
    ├── rule-utils.mjs
    └── backend-utils.mjs
```

## CI

`.github/workflows/architecture.yml` ejecuta `npm run architecture:check` (Node 22) tras guardrails PowerShell. Annotations automáticas en PRs vía `GITHUB_ACTIONS`.

## Precedencia

1. Scripts ejecutables + CI  
2. [`AI-RULES/*`](../../AI-RULES/README.md)  
3. Adaptadores (`.mdc`, `CLAUDE.md`)  
4. ADRs (rationale, no override de CI sin cambio de config)
