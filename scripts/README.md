# Scripts — ERP SaaS

Mapa canónico y uso detallado: **[`docs/DEVELOPMENT.md`](../docs/DEVELOPMENT.md#scripts-powershell-canónicos)**.

## PowerShell (7 archivos)

| Script | Rol |
|--------|-----|
| [`../Crear-SuperAdmin.ps1`](../Crear-SuperAdmin.ps1) | Alta SuperAdmin first-run (raíz del repo) |
| `dev-restart.ps1` | Dev: Docker, migraciones EF, API + Vite |
| `run-e2e.ps1` | Playwright E2E local/CI |
| `verify-stack-allowlist.ps1` | CI: dependencias + scripts permitidos |
| `check-identity-guardrails.ps1` | CI: sin auth legacy `users` |
| `new-master-module.ps1` | Scaffolding módulo master |
| `import_inec_ecuador_geography.ps1` | Generar SQL geografía INEC |

Lista verificada en CI: **`stack-allowlist.json`** → clave `scriptsAllowed`.

## SQL (`sql/`)

Mapa: [`sql/README.md`](./sql/README.md). Política InstallData: [`docs/DATABASE.md`](../docs/DATABASE.md).

No añadir `.ps1` fuera del mapa sin actualizar `stack-allowlist.json`, `docs/DEVELOPMENT.md` y [`CONTEXT.md`](../CONTEXT.md).
