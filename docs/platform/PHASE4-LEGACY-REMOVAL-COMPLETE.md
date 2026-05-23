> **Documento histórico (Phase 2–5).** No usar como referencia de implementación. Rutas y naming actuales: [TEAM-NAMING-GUIDE.md](./TEAM-NAMING-GUIDE.md) · [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md).
# Phase 4 — Platform SuperAdmin Legacy Removal

**Fecha:** 2026-05-23  
**Estado:** ✅ COMPLETE

## Objetivo

Eliminar la superficie API legacy del control plane SuperAdmin, sustituida por `/api/platform/*`, sin afectar ERP Runtime ni aislamiento multi-tenant.

## Backend eliminado

| Artefacto | Ruta legacy |
|-----------|-------------|
| `SuperAdminController` | `/api/superadmin/*` |
| `SuperAdminConfigController` | `/api/superadmin/config/*` |
| `SaasPlansAdminController` | `/api/superadmin/commercial-plans/*` |
| `SuperAdminPlanesMenuController` | `/api/superadmin/planes/*` |
| `SuperAdminEmpresasMenuController` | `/api/superadmin/empresas/*` |
| `SuperAdminAppFeaturesController` | `/api/superadmin/AppFeatures/*` |
| `AuthController.SuperAdminLogin` | `POST /api/auth/superadmin-login` |
| `AccessController` bloque IAM | `/api/admin/iam/superadmin/subscribers*` |
| `SuperAdminLogin` use case (alias MediatR) | — |

## Backend añadido / migrado

| Artefacto | Ruta canónica |
|-----------|---------------|
| `PlatformSettingsController` | `GET/PUT /api/platform/settings/instance-quota` |

## Frontend eliminado

- `LEGACY_PLATFORM_API`, `LEGACY_SUPERADMIN_UI_REDIRECTS` (`platformApiPaths.ts`)
- `CompaniesPage` stack (`pages/`, hook, CSS, re-export)
- Beacon `recordLegacyUiRoute` en redirect `/companies`
- `/api/auth/superadmin-login` en `authRefreshPolicy`
- Alias `goToCompaniesSubscriberDetail` → callers usan `goToSubscriberDetail`

## Conservado (scope explícito)

- `/companies/*` → redirect a `/superadmin/*` (sessionStorage compat)
- `modules/companies/api/companyService.ts` (ficha suscriptor platform)
- `POST /api/auth/switch-subscriber`, IAM runtime (`bootstrap-login`, memberships, profiles)
- Tablas BD legacy (sin DROP)
- `SubscribersController` runtime (`entitlements/me`, `public-settings`, tenant admin PATCH)

## Middleware

- `SuperAdminPanelLockMiddleware`: bloquea `POST /api/platform/auth/login` cuando panel deshabilitado
- `EnterpriseDiagnosticMiddleware`: removido prefijo `/api/superadmin`

## Validación

- [x] `dotnet build`
- [x] `dotnet test backend/src/ERP.API.Tests` (187 tests)
- [x] `npm run build` (frontend)
- [x] Sin referencias activas a `/api/superadmin`, `superadmin-login`, `iam/superadmin` en código fuente

## Documentación

- [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md) — Phase 4 COMPLETE
- [ROUTE-MIGRATION.md](./ROUTE-MIGRATION.md) — strangler cerrado
