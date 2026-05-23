# Guía de naming — Platform / operador platform

**Para todo el equipo.** Evita confusión entre nombres legacy y el producto actual.

**Última sync:** 2026-05-23

---

## Regla de oro

| Usar en código, docs operativos, PRs, scripts | No usar nunca en nombres nuevos |
|------------------------------------------------|----------------------------------|
| **platform**, **operador platform**, **PlatformOperator** | `SuperAdmin`, `superadmin`, `superAdmin*`, `isSuperAdmin`, `SuperAdminController` |

Los valores wire legacy (`SuperAdmin` en JWT antiguo, keys JSON antiguas, URL `/superadmin/*` como redirect) existen **solo** en los archivos listados abajo — no copiar esos literales en código nuevo.

---

## Fuente de verdad (leer en este orden)

| # | Documento | Para qué |
|---|-----------|----------|
| 1 | [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md) | Rutas API `/api/platform/*` y UI `/platform/*` |
| 2 | [LEGACY_ALIAS_MAP.md](./LEGACY_ALIAS_MAP.md) | Qué existió antes y qué reemplazó (histórico + wire) |
| 3 | [docs/IDENTITY.md](../IDENTITY.md) | JWT, login, first-run |
| 4 | `backend/.../PlatformAuthConstants.cs` | Contrato auth backend |
| 5 | `frontend/src/constants/platformAuth.ts` | Contrato auth frontend |

Informes Phase 2–4 en `docs/platform/PHASE*.md` son **histórico**; no usar sus rutas como referencia de implementación.

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

## Legacy permitido (solo lectura / compat)

No crear identificadores con estos nombres; solo parsear datos viejos o redirects.

| Wire legacy | Dónde está centralizado | Uso |
|-------------|-------------------------|-----|
| JWT `"SuperAdmin"` | `PlatformAuthConstants.LegacyPlatformOperatorWireRole`, `platformAuth.ts` → `LEGACY_WIRE.jwtRole` | Tokens/BD antiguos |
| JSON `superAdminPanelEnabled` | `platformAuth.ts` → `LEGACY_WIRE` | GET deployment antiguo |
| JSON `requireSuperAdminPanel` | `platformAuth.ts` → `LEGACY_WIRE` | Menú API antiguo |
| URL `/superadmin/*` | `platformRoutes.tsx` | Redirect → `/platform/*` |
| Config `Deployment:SuperAdminPanelEnabled` | `DeploymentFeatureFlags.cs` | Env legacy |
| Columna BD `require_superadmin_panel` | EF mapping | Sin rename de esquema |

---

## Prohibido (CI falla si reaparece)

- Rutas `/api/superadmin/*`, `superadmin-login`, `/api/admin/iam/superadmin/*`
- Archivos/clases `*SuperAdmin*`, `SuperAdminService`, `useSuperAdmin`, `isSuperAdmin`
- Imports `modules/superadmin`, `pages/SuperAdmin`
- Script `Crear-SuperAdmin.ps1` (eliminado)
- Literal `'SuperAdmin'` en frontend fuera de `constants/platformAuth.ts`

Guards: `tools/ci/platform-guard-config.json`, `tools/architecture/check-platform-legacy-surface.mjs`, `PlatformControlPlaneGuardTests.cs`.

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
| **Impersonación** | Platform elige tenant → banner + `/saas/*` | `POST /api/auth/switch-subscriber` |

---

## Checklist PR (platform)

- [ ] Sin `SuperAdmin` / `superadmin` en nombres de archivos, funciones, props, rutas nuevas
- [ ] UI platform bajo `/platform/*` (redirect legacy OK solo en `platformRoutes.tsx`)
- [ ] API control plane bajo `/api/platform/*`
- [ ] Rol/menú: `PlatformOperator` o helpers de `platformAuth.ts`
- [ ] Docs operativos enlazan a este archivo o `CANONICAL-ROUTES.md`
