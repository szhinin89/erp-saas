# Scripts — ERP SaaS

Scripts PowerShell **canónicos**: lista explícita `scriptsAllowed` y subsistemas `scriptsAllowedRoots`
en [`stack-allowlist.json`](stack-allowlist.json). CI rechaza cualquier otro `.ps1`, salvo artefactos no versionados de `/.local/` (ver [alcance del auditor](../docs/DEVELOPMENT.md#scripts-powershell-canónicos)).

## Mapa

| Ruta | Rol |
|------|-----|
| [`dev-launcher.ps1`](dev-launcher.ps1) | Entorno dev: EF + API + Vite + estado [First Run](../docs/architecture/first-run.md), reset completo, tests (`erp_zh_dev`) |
| [`app-stack.ps1`](app-stack.ps1) | Stack de aplicación `docker-compose.app.yml` (Config/Build/Up/Down/Status/Logs/Health); rechaza `erp_zh_dev` |
| [`backup-restore.ps1`](backup-restore.ps1) | Backup + restore drill aislado (`-Action Backup\|RestoreCheck -Database <db>`) |
| [`backend-check.ps1`](backend-check.ps1) / [`frontend-check.ps1`](frontend-check.ps1) | Revisión, compilación y pruebas locales por capa |
| [`ci/run-e2e.ps1`](ci/run-e2e.ps1) | Playwright E2E completo sobre `erp_zh_test` |
| [`ci/verify-stack-allowlist.ps1`](ci/verify-stack-allowlist.ps1) | Auditoría stack CI |
| [`ci/check-pages-wrapper-rule.ps1`](ci/check-pages-wrapper-rule.ps1) | Regla de wrappers de páginas |
| [`tests/`](tests/) | Cuatro pruebas focales del launcher: [First Run](../docs/architecture/first-run.md) |
| [`ds/`](ds/) | Auditoría y guard del Design System |
| [`qa/diagnose-pos-warehouse.ps1`](qa/diagnose-pos-warehouse.ps1) | Diagnóstico POS/bodega |

## Tooling (no operación)

| Ruta | Rol |
|------|-----|
| [`../tools/architecture/`](../tools/architecture/) | Guardrails arquitectura |
| [`../tools/quality/`](../tools/quality/) | Handler size |
| [`../tools/generators/`](../tools/generators/) | Scaffolding módulos |
| [`../tools/dashboard/`](../tools/dashboard/) | Pipeline ProgressDashboard (raíz admitida) |
| [`../print-agent/scripts/`](../print-agent/scripts/) | Print Agent (raíz admitida) |

## Añadir script

1. Ubicación coherente (`ci/`, `db/`, `ds/`, …)
2. Entrada en `scriptsAllowed` en `stack-allowlist.json`
3. Actualizar [`CONTEXT.md`](../CONTEXT.md) y [`docs/DEVELOPMENT.md`](../docs/DEVELOPMENT.md)
