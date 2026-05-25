> **Documento histórico (Phase 2–5).** No usar como referencia de implementación. Rutas y naming actuales: [TEAM-NAMING-GUIDE.md](./TEAM-NAMING-GUIDE.md) · [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md).
# API Duplication Removal Map

**Objetivo:** eliminar ambigüedad entre `/api/subscribers/*` (legacy control plane) y `/api/platform/subscribers/*` (canónico).

## Clasificación de endpoints

### DUPLICADO DIRECTO — REMOVED

| Método | Legacy | Canónico | Acción |
|--------|--------|----------|--------|
| POST | `/api/subscribers` | `POST /api/platform/subscribers` | **Eliminado** |
| PATCH | `/api/subscribers/{id}/global-parameters` | `PATCH /api/platform/subscribers/{id}/global-parameters` | **Eliminado** |
| PATCH | `/api/subscribers/{id}/subscription` | `PATCH /api/platform/subscribers/{id}/plan` | **Eliminado** |
| GET | `/api/platform/subscribers` | *(único listado)* | KEEP |
| GET | `/api/platform/subscribers/{id}/menu` | *(no existía en legacy raíz)* | KEEP |
| PUT/DELETE | `/api/platform/subscribers/{id}/menu` | *(no existía en legacy raíz)* | KEEP |
| GET | `/api/platform/subscribers/{id}/entitlements` | *(no existía en legacy raíz)* | KEEP |
| PATCH | `/api/platform/subscribers/{id}/activate\|suspend\|trial\|grace-period\|plan` | lifecycle | KEEP |

### RUNTIME ERP — KEEP (no es control plane)

| Método | Ruta | Rol | Notas |
|--------|------|-----|-------|
| GET | `/api/subscribers/entitlements/me` | Session | `SubscriberEntitlementsController` |
| GET | `/api/subscribers/{id}/public-settings` | Anonymous | Password reset |
| GET | `/api/subscribers/{id}` | Admin (own tenant) | Tenant profile read |
| PATCH | `/api/subscribers/{id}/company` | Admin (own tenant) | Tenant profile write |
| PATCH | `/api/subscribers/{id}/operational-settings` | Admin (own tenant) | Moneda, idioma, TZ |
| PATCH | `/api/subscribers/{id}/password-reset-mode` | Admin | Seguridad tenant |
| POST | `/api/auth/switch-subscriber` | Auth | Cross-cutting |
| GET | `/api/public/plans` | Public | Pre-login |

### OVERLAP SuperAdmin vs Runtime (resolución)

`SubscribersController.GetById` y `UpdateCompany` son **dual-role**:

- **Admin** → runtime legítimo (`runtimeSubscriberService` en frontend)
- **SuperAdmin** → duplicado funcional de platform; frontend **no** debe usar `/api/subscribers` para control plane — usar `subscriberService` → `/api/platform/subscribers`

No se eliminaron estos métodos del controller runtime para no romper Tenant Admin.

## Implementación backend

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `SubscribersController.cs` | Solo runtime ERP; eliminados POST, global-parameters, subscription |
| `LegacySubscriberControlPlaneMiddleware.cs` | **Eliminado** (sin rutas legacy) |

### Headers en respuestas legacy

```
Deprecation: true
X-Api-Deprecated: true
X-Deprecated-Endpoint: /api/subscribers/...
Link: </api/platform/subscribers/...>; rel="successor-version"
```

## Mapa de migración frontend

| Antes | Después |
|-------|---------|
| `companyService.getSubscriber` (platform paths) | `subscriberService.getSubscriber` |
| `companyService.updateSubscriberCompany` (platform UI) | `subscriberService.updateSubscriberCompany` |
| `companyService.*` config global | `subscriberService.*` |
| `companyService.*` (CompanyConfigPage Admin) | `runtimeSubscriberService.*` → `/api/subscribers` |
| `platformService.getSubscribers` | sin cambio (ya canónico) |

## Eliminaciones

- `frontend/src/modules/companies/api/companyService.ts` — **removed**
- Duplicidad activa frontend `/api/subscribers` para control plane — **removed**

## Verificación

```bash
node tools/ci/run-platform-guard.mjs
dotnet test backend/src/ERP.Architecture.Tests --filter PlatformControlPlane
```
