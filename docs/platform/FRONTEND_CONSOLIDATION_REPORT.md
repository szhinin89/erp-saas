> **Documento histórico (Phase 2–5).** No usar como referencia de implementación. Rutas y naming actuales: [TEAM-NAMING-GUIDE.md](./TEAM-NAMING-GUIDE.md) · [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md).
# Frontend Consolidation Report

**Fecha:** 2026-05-23

## Objetivo

100% del Platform Control Plane UI debe consumir `/api/platform/*`. Eliminar wrappers duplicados (`companyService`) y separar claramente runtime tenant Admin.

## Cambios realizados

### Nuevos módulos API

| Archivo | Responsabilidad |
|---------|-----------------|
| `frontend/src/modules/platform/api/subscriberService.ts` | CRUD perfil + config global suscriptor (SuperAdmin) |
| `frontend/src/modules/subscribers/api/tenantSubscriberService.ts` | Perfil operativo tenant Admin (runtime) |

### Eliminados

| Archivo | Motivo |
|---------|--------|
| `modules/companies/api/companyService.ts` | Wrapper duplicado sobre mismos paths platform |

### Migraciones de consumidores

| Consumidor | Antes | Después |
|------------|-------|---------|
| `useSubscriberDetailPage.ts` | `companyService` | `subscriberService` + `platformService` |
| `CompanyConfigPage.tsx` | `companyService` (paths platform incorrectos para Admin) | `tenantSubscriberService` (runtime `/api/subscribers`) |
| `CompanyModuleChips.tsx` | tipo `CompanyItem` | `PlatformSubscriber` |

### Barrel deprecado

`modules/companies/index.ts` re-exporta desde servicios canónicos con `@deprecated` para compatibilidad transitoria de imports.

## Matriz de rutas frontend

| Feature | Service | Endpoint |
|---------|---------|----------|
| Listado suscriptores (platform UI) | `platformService.getSubscribers` | `GET /api/platform/subscribers` |
| Crear suscriptor | `platformService.createSubscriberWithAdmin` | `POST /api/platform/subscribers` |
| Detalle suscriptor (platform) | `subscriberService.getSubscriber` | `GET /api/platform/subscribers/{id}` |
| Entitlements admin | `platformService.getSubscriberEntitlements` | `GET /api/platform/subscribers/{id}/entitlements` |
| Menú suscriptor | `platformService.*Menu*` | `/api/platform/subscribers/{id}/menu` |
| Config global trial | `subscriberService.resolveSubscriberConfig` | `/api/platform/config/{id}/resolve` |
| Config empresa (Admin ERP) | `tenantSubscriberService.*` | `/api/subscribers/{id}/*` |
| Gating módulos sesión | `entitlementsService` | `GET /api/subscribers/entitlements/me` |
| Switch tenant | `platformService.switchSubscriber` | `POST /api/auth/switch-subscriber` |
| Planes públicos | `platformService.getPublicPlans` | `GET /api/public/plans` |

## CI enforcement

Nuevo check `validate-subscriber-api-surface.mjs`:

- Falla si `/api/subscribers` aparece fuera de whitelist runtime
- Whitelist: `entitlementsService`, `tenantSubscriberService`, `PasswordResetPage`, e2e helpers

Forbidden patterns:

- `companyService`
- `companyService.getSubscribers`
- `companies/api/companyService` imports

## Drift restante (no bloqueante)

| Item | Notas |
|------|-------|
| UI routes `/superadmin/*` | Intencional — JWT role `SuperAdmin` preservado |
| i18n keys `superadmin.*` | Cosmético — fuera de alcance consolidación API |
| `modules/companies/` folder | Barrel deprecado; puede eliminarse en cleanup futuro |

## Validación

```bash
cd frontend && npm run build   # incluye platform guard
node tools/ci/run-platform-guard.mjs
```

Resultado esperado: **PASS**, 0 violaciones subscriber-api-surface fuera de whitelist.
