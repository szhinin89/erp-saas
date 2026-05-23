# Scripts — ERP SaaS

Scripts PowerShell **canónicos** (lista en [`stack-allowlist.json`](stack-allowlist.json)). CI rechaza cualquier otro `.ps1`.

## Mapa

| Ruta | Rol |
|------|-----|
| [`setup/Crear-PlatformOperator.ps1`](setup/Crear-PlatformOperator.ps1) | Alta operador platform first-run |
| [`dev/dev-restart.ps1`](dev/dev-restart.ps1) | Docker + EF + API + Vite |
| [`ci/run-e2e.ps1`](ci/run-e2e.ps1) | Playwright E2E completo |
| [`ci/verify-stack-allowlist.ps1`](ci/verify-stack-allowlist.ps1) | Auditoría stack CI |
| [`db/import_inec_ecuador_geography.ps1`](db/import_inec_ecuador_geography.ps1) | SQL geografía INEC |
| [`db/sql/`](db/sql/) | SQL excepcional documentado |

## Tooling (no operación)

| Ruta | Rol |
|------|-----|
| [`../tools/architecture/`](../tools/architecture/) | Guardrails arquitectura |
| [`../tools/quality/`](../tools/quality/) | Handler size |
| [`../tools/generators/`](../tools/generators/) | Scaffolding módulos |

## Carpetas reservadas

`deploy/`, `maintenance/`, `auth/` — runbooks futuros.

## Añadir script

1. Ubicación coherente (`dev/`, `ci/`, `db/`, …)
2. Entrada en `scriptsAllowed` en `stack-allowlist.json`
3. Actualizar [`CONTEXT.md`](../CONTEXT.md) y [`docs/DEVELOPMENT.md`](../docs/DEVELOPMENT.md)
