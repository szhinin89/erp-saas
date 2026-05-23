# Platform Control Plane — Guía de migración

## Objetivo

Consolidar el control plane en rutas **canónicas** sin romper contratos legacy de inmediato. Los clientes existentes pueden seguir llamando rutas antiguas; el backend responde con headers RFC 8594:

- `Deprecation: true`
- `X-Api-Deprecated: true`
- `X-Deprecated-Endpoint: <legacy-path>`
- `Link: </api/platform/...>; rel="successor-version"` (cuando aplica)

Además: **log warning** + registro en `ILegacyEndpointUsageTracker` (dashboard en `/superadmin/observability`).

## Frontend — Phase 2 (aplicado)

1. **API client** (`platformService.ts`, `companyService.ts`): platform shell → **solo** `/api/platform/*`.
2. **Growth analytics / navigation / features / plans catalog:** migrados a platform metrics, navigation-menu, features, plans.
**Phase 4 (2026-05-23):** strangler cerrado — controllers legacy eliminados. Ver [PHASE4-LEGACY-REMOVAL-COMPLETE.md](./PHASE4-LEGACY-REMOVAL-COMPLETE.md).
4. **Navegación:** shell Super Admin unificado; `/companies` redirige vía `CompaniesLegacyRedirect`.
5. **Ficha suscriptor:** `/superadmin/subscribers/:subscriberId` con 9 tabs — reemplaza `CompaniesPage`.
6. **Users / Billing / Observability:** páginas reales (no placeholders).

**Runtime ERP sin cambios:** `entitlements/me`, `public-settings`, `switch-subscriber`, `/api/companies/*`.

## Backend — Phase 2 (aplicado)

- `DeprecatedApiAttribute` + usage tracker + header `X-Deprecated-Endpoint`.
- Controllers platform: subscribers (extendido), navigation, features, observability, billing, users, metrics (growth).
- `[DeprecatedApi]` method-level en `SuperAdminController` (navigation, growth, plans, revoke sessions).

## Checklist para eliminar legacy (Phase 3+)

| Paso | Acción | Criterio de borrado |
|------|--------|---------------------|
| 1 | Verificar 0 calls legacy en observability dashboard | 30d ventana |
| 2 | Eliminar `CompaniesPage` | 0 hits `/companies` 60d |
| 3 | PATCH `enabledModules` en platform | Paridad con subscription legacy |
| 4 | Eliminar controllers legacy por dominio | Tests + CI verde |
| 5 | Persistir usage tracker (Redis) | Multi-instancia prod |

## Naming

- **Backend / API:** inglés técnico (`subscribers`, `plans`, `audit`).
- **Frontend UI:** i18n (`es` / `en` / `qu`) desacoplado de paths API.

## Validación

```bash
dotnet build backend/src/ERP.API/ERP.API.csproj -c Release
dotnet test backend/src/ERP.API.Tests -c Release
dotnet test backend/src/ERP.Architecture.Tests -c Release
cd frontend && npm run build
```

Comprobar:

- [x] `GET /api/platform/subscribers` lista suscriptores
- [x] `GET /api/platform/observability/legacy-endpoints` dashboard
- [x] `/companies` → `/superadmin/subscribers`
- [x] `/superadmin/menu-plans` → `/superadmin/plans?tab=menu`
- [x] Ficha suscriptor 9 tabs compila y carga entitlements
- [ ] Smoke manual: impersonación, switch-company, BP pickers (regresión manual)

Reporte de deuda: [PHASE2-CLEANUP-AUDIT.md](./PHASE2-CLEANUP-AUDIT.md).
