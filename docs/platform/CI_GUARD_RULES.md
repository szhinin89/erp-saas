# Platform Control Plane — CI Hard Gates

**Orquestador:** `tools/ci/run-platform-guard.mjs`  
**Config:** `tools/ci/platform-guard-config.json`  
**Reporte:** `docs/ci/PLATFORM_GUARD_REPORT.md` (generado en CI)

## Ejecución local

```bash
cd frontend
npm run platform:guard
```

Scripts individuales:

| Script | Comando |
|--------|---------|
| Full guard | `npm run platform:guard` |
| Static scan | `npm run platform:scan` |
| Import guard | `npm run platform:imports` |
| API allowlist | `npm run platform:api-endpoints` |

Backend (xUnit):

```bash
dotnet test backend/src/ERP.Architecture.Tests -c Release --filter "FullyQualifiedName~PlatformControlPlaneGuardTests"
```

## Checks (fail-fast)

| # | Check | Ámbito |
|---|-------|--------|
| 1 | `static-forbidden-patterns` | `frontend/src` + `backend/src` (sin `docs/`) |
| 2 | `platform-imports` | imports legacy (`modules/superadmin`, `superadminService`, …) |
| 3 | `frontend-routes` | `platformRoutes.tsx`, `App.tsx` |
| 4 | `api-endpoints` | allowlist de prefijos `/api/*` usados en frontend |
| 5 | `PlatformControlPlaneGuardTests` | rutas backend — prohibido `/api/superadmin` |

## Patrones prohibidos (case-insensitive, comentarios excluidos)

- `/api/superadmin/`
- `superadmin-login`
- `SuperAdminController`
- `SuperAdminService` / `platformService`
- `usePlatformGate`
- `LEGACY_PLATFORM`
- `LEGACY_SUPERADMIN`
- `/api/admin/iam/superadmin`

## Allowlist API (frontend)

Prefijos permitidos en `platform-guard-config.json` → `allowedApiPrefixes`:

- Control plane: `/api/platform`, `/api/subscribers`
- Auth: `/api/auth`, `/api/admin/iam`
- ERP runtime: `/api/master`, `/api/sales`, `/api/purchases`, `/api/inventory`, …

**FAIL** si un endpoint literal en frontend no cae en la allowlist o coincide con prefijos prohibidos.

## CI integration

| Workflow | Step |
|----------|------|
| `architecture.yml` | `npm run platform:guard` (hard-gate) |
| `frontend-ci.yml` | `npm run platform:guard` + incluido en `npm run build` |
| `backend-ci.yml` | `dotnet test` + `npm run platform:guard` |

## Principio

Preventivo · obligatorio · fail-fast (sin warnings que pasen el pipeline).
