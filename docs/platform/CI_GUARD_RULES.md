# CI Guard Rules — Platform Legacy Surface

**Phase 5** · Bloqueante en `npm run build` y `npm run architecture:check`

## Scripts

| Script | Comando | Rol |
|--------|---------|-----|
| Legacy surface guard | `node tools/architecture/check-platform-legacy-surface.mjs` | Escanea `frontend/src` |
| API usage graph | `node tools/architecture/extract-api-usage-graph.mjs` | Genera `docs/platform/API_USAGE_GRAPH.json` |
| Wrapper npm | `npm run platform:guard` | Solo guard |
| Wrapper npm | `npm run platform:api-graph` | Solo graph |
| Build | `npm run build` | tsc → guard → graph → vite |

## Tokens prohibidos (`frontend/src`)

El build **falla** si cualquier línea de código (no comentarios `//` o `*`) contiene:

| Token / patrón | Motivo |
|----------------|--------|
| `superadmin-login` | Auth legacy eliminado |
| `/api/superadmin/` | Control plane legacy |
| `/api/admin/iam/superadmin` | IAM platform duplicado |
| `LEGACY_PLATFORM` | Constantes strangler |
| `LEGACY_PLATFORM_API` | Constantes strangler |
| `superAdminService` | Client renombrado a `platformService` |

## API graph validation

`extract-api-usage-graph.mjs` extrae literales `/api/...` y falla si alguno coincide con:

- `/api/superadmin/`
- `superadmin-login`
- `/api/admin/iam/superadmin`

Salida: [`API_USAGE_GRAPH.json`](./API_USAGE_GRAPH.json) con buckets `platformEndpoints`, `runtimeEndpoints`, `publicEndpoints`.

## Integración architecture:check

En [`tools/architecture/run-all.mjs`](../../tools/architecture/run-all.mjs):

```javascript
{ name: 'platform-legacy-surface', run: runCheckPlatformLegacySurface }
```

## Extender reglas

1. Añadir patrón en `FORBIDDEN` de `check-platform-legacy-surface.mjs`
2. Si aplica a endpoints, añadir en `LEGACY_PATTERNS` de `extract-api-usage-graph.mjs`
3. Documentar aquí y en `LEGACY_SURFACE_REPORT.md`

## Exclusiones intencionales

- **`docs/**`** — histórico de migración
- **Comentarios** en código — ignorados por el guard
- **`/superadmin/*` UI paths** — shell canónico (no son API)
- **`/api/companies/*`** — runtime ERP tenant (fuera de control plane)
