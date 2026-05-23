# Phase 5 — Legacy Surface Report

**Fecha:** 2026-05-23  
**Estado:** ✅ COMPLETE

## Resumen ejecutivo

Tras Phase 4 (eliminación API legacy) y Phase 5 (hardening frontend + CI), el control plane opera **exclusivamente** vía `/api/platform/*`. No quedan rutas activas `/api/superadmin/*`, `superadmin-login`, ni imports `platformService`.

## Superficie eliminada (Phase 4 + 5)

| Categoría | Antes | Después |
|-----------|-------|---------|
| API client | `platformService.ts` | `platformService.ts` |
| Constantes UI | `SUPERADMIN_UI`, `LEGACY_PLATFORM_API` | `PLATFORM_UI`, `PLATFORM_API` |
| Router platform | `superAdminShellRoutes`, `adminRoutes`, `CompaniesLegacyRedirect` | `platformRoutes.tsx` (única fuente) |
| Nav helpers | `companiesSubscriberDetailNav.ts` | `platformSubscriberDetailNav.ts` |
| BP telemetry | `legacy-masterdata` beacon en pickers | Eliminado (pickers usan `/api/master/business-partners`) |
| Auth legacy | `superadmin-login` en refresh policy | Solo `/api/platform/auth/login` |

## Huellas residuales aceptadas (no API legacy)

| Elemento | Motivo |
|----------|--------|
| Rutas UI `/superadmin/*` | Shell canónico platform (nombre URL estable) |
| Redirect `/companies/*` | Bookmark externo → `platformBookmarkRedirectRoutes()` |
| `/api/companies/*` (runtime) | ERP multiempresa tenant — **no** control plane |
| `/saas/companies/*` | Gestión empresas dentro de tenant impersonado |
| Layout y páginas `Platform*` | Phase 5b: renombrado físico completo (`modules/platform`, `components/platform`, `pages/Platform`) |
| Rol JWT `SuperAdmin` | Claim backend; no implica API legacy |
| Comentarios históricos en backend Platform controllers | Documentación de migración (sin endpoints activos) |

## CI guard (automático)

`npm run build` ejecuta:

1. `check-platform-legacy-surface.mjs` — falla ante tokens prohibidos en `frontend/src`
2. `extract-api-usage-graph.mjs` — falla si detecta endpoints legacy en strings API

Integrado también en `npm run architecture:check` como check `platform-legacy-surface`.

## Validación

| Check | Resultado |
|-------|-----------|
| `npm run build` | ✅ (guard + API graph + vite) |
| `dotnet build` Release | ✅ (Phase 4 baseline) |
| `ERP.API.Tests` | 187/187 ✅ |
| Legacy API en frontend/src | **0** |
| Legacy API en API_USAGE_GRAPH | **0 violations** |

Ver: [FRONTEND_CLEAN_ROUTER.md](./FRONTEND_CLEAN_ROUTER.md), [CI_GUARD_RULES.md](./CI_GUARD_RULES.md), [API_USAGE_GRAPH.json](./API_USAGE_GRAPH.json).
