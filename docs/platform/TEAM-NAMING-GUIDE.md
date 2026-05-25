# Guía de naming — Platform / operador platform

**Para todo el equipo.** Evita confusión entre nombres legacy y el producto actual.

**Última sync:** 2026-05-25

---

## Regla de oro

| Usar en código, docs operativos, PRs, scripts | No usar nunca |
|------------------------------------------------|---------------|
| **platform**, **operador platform**, **PlatformOperator** | `SuperAdmin`, `superadmin`, `superAdmin*`, `isSuperAdmin`, `SuperAdminController` |

**Zero legacy en código (2026-05-25):** no hay aliases wire, redirects `/superadmin/*` ni `/companies/*`, ni lectura de JWT `SuperAdmin`. Schema greenfield: baseline único `20260525224928_InitialEnterpriseBaseline` (sin valores wire legacy).

---

## Fuente de verdad (leer en este orden)

| # | Documento | Para qué |
|---|-----------|----------|
| 1 | [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md) | Rutas API `/api/platform/*` y UI `/platform/*` |
| 2 | [CLEAN_TARGET_MODEL.md](./CLEAN_TARGET_MODEL.md) | Mapa entidad → tabla → API → frontend |
| 3 | [docs/IDENTITY.md](../IDENTITY.md) | JWT, login, first-run |
| 4 | `backend/.../PlatformAuthConstants.cs` | Contrato auth backend |
| 5 | `frontend/src/constants/platformAuth.ts` | Contrato auth frontend |

---

## Canónico (copiar tal cual)

### API

| Acción | Ruta |
|--------|------|
| Login operador platform | `POST /api/platform/auth/login` |
| CRUD suscriptores (control plane) | `/api/platform/subscribers/*` |
| Planes, menú, métricas, audit | `/api/platform/plans`, `/navigation-menu`, `/metrics`, `/audit`, … |
| First-run (una vez por instancia) | `POST /api/setup/platform-operator` |

### UI

| Ruta | Pantalla |
|------|----------|
| `/platform/overview` | Dashboard |
| `/platform/subscribers` | Listado suscriptores |
| `/platform/subscribers/:id` | Ficha suscriptor |
| `/platform/plans` | Planes + menú (`?tab=menu`) |
| `/platform/users` | Operadores platform |
| `/platform/billing` | Billing SaaS |
| `/platform/observability` | Métricas |
| `/platform/audit` | Audit log |

Constantes: `PLATFORM_UI` en `frontend/src/modules/platform/api/platformApiPaths.ts`.

### Código / tipos

| Concepto | Nombre canónico |
|----------|-----------------|
| Rol JWT / menú | `PlatformOperator` |
| Constante backend | `PlatformAuthConstants.JwtPlatformOperatorRole` |
| Constante frontend | `JWT_PLATFORM_OPERATOR_ROLE` |
| Helper rol | `isJwtPlatformOperatorRole()` |
| Hook shell | `usePlatformGate` |
| Servicio API | `platformService` (`modules/platform/api/`) |
| Módulo FE | `modules/platform/`, `pages/Platform/` |
| Script first-run | `scripts/setup/Crear-PlatformOperator.ps1` |
| Usuario dev seed | `platform@erp.com` / `Admin123!` |

---

## Prohibido (CI falla si reaparece)

- Rutas `/api/superadmin/*`, `superadmin-login`, `/api/admin/iam/superadmin/*`
- Archivos/clases `*SuperAdmin*`, `SuperAdminService`, `useSuperAdmin`, `isSuperAdmin`
- Imports `modules/superadmin`, `pages/SuperAdmin`
- Script `Crear-SuperAdmin.ps1` (eliminado)
- Literal `'SuperAdmin'` en cualquier capa del producto
- Identificadores `Tenant*`, `TENANT_*`, `tenantId`, `variant="tenant"`, `shell-content-frame--tenant` en `frontend/src` (canónico: **Subscriber**)

Guards: `tools/ci/platform-guard-config.json`, `tools/architecture/check-platform-legacy-surface.mjs`, `tools/architecture/check-frontend-subscriber-naming.mjs`, `PlatformControlPlaneGuardTests.cs`.

---

## First-run local

1. Arrancar API → banner en consola con `POST /api/setup/platform-operator`
2. O ejecutar: `.\scripts\setup\Crear-PlatformOperator.ps1`
3. Dev reset token: `POST /api/dev/reset-first-run` (solo Development)

---

## Separación mental (evitar mezclar)

| Capa | Contexto JWT | Rutas |
|------|--------------|-------|
| **Platform control plane** | `user_type=Platform`, rol `PlatformOperator`, sin ERP operativo global | `/platform/*`, `/api/platform/*` |
| **ERP runtime** | `subscriber_id` + `company_id`, permisos por plan | `/sales`, `/masterdata`, … |
| **Impersonación** | Platform elige suscriptor → banner + `/saas/*` | `POST /api/auth/switch-subscriber` |

---

## Checklist PR (platform)

- [ ] Sin `SuperAdmin` / `superadmin` en nombres de archivos, funciones, props, rutas
- [ ] UI platform bajo `/platform/*` (sin redirects legacy)
- [ ] API control plane bajo `/api/platform/*`
- [ ] Rol/menú: `PlatformOperator` o helpers de `platformAuth.ts`
- [ ] Docs operativos enlazan a este archivo o `CANONICAL-ROUTES.md`
