# Fase B+ — Iteración 11: entitlements snapshot E2E

**Branch:** `refactor/saas-enterprise-11-entitlements-snapshot-e2e`

## Entregables

### API

- `GET /api/saas/entitlements/me` (`Session` policy)
- `ITenantEntitlementsService.GetEntitlementsSnapshotAsync`
- `GetMyEntitlementsHandler` (MediatR)

Respuesta (`TenantEntitlementsSnapshot`):

- `planCode`, `planName`
- `enabledModules[]` (canónicas, fail-closed)
- `enabledFeatures[]` (códigos no-`Module`)
- `limits` (`featureCode` → límite o null)
- `hasModuleRestrictions`

### Frontend (F1)

- `entitlementsService.getMe()`
- `syncSessionEntitlements()` — snapshot SaaS + permisos RBAC (`getMyPermissions`)
- `permissionsStore.setEntitlementsSnapshot` — módulos **solo** desde snapshot
- `AppLayout.moduleEntitled` — sin fallback JWT; alias ES→EN vía `normalizeModuleKey`
- Login / switch-tenant / refresh AppLayout usan `syncSessionEntitlements`

### Tests

- `TenantEntitlementsServiceTests.GetEntitlementsSnapshotAsync_*`
- `syncSessionEntitlements.test.ts` (vitest)

## Verificación

```powershell
cd c:\ProyectCursor\erp-saas\backend\src\ERP.Infrastructure.Tests
dotnet test --filter "FullyQualifiedName~GetEntitlementsSnapshot"

cd c:\ProyectCursor\erp-saas\frontend
npm run test:unit
```
