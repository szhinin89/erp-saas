# Seguridad — auth, tokens y multi-tenant

Canónico para identidad y aislamiento. Detalle descriptivo: `docs/IDENTITY.md`, `docs/security/auth/refresh-rotation.md`. Reglas PR: [PR-RULES-CATALOG.md](./pr-rules-catalog.md).

---

## Multi-tenant (innegociable)

- `TenantId` desde **JWT/contexto**, nunca como autoridad desde body/query en operaciones tenant-scoped.
- Filtros globales EF por tenant en lecturas.
- Índices únicos con `TenantId` cuando la unicidad es por empresa.
- Cross-tenant solo en flujos plataforma / operador platform autorizados.
- Frontend: no enviar `tenantId` en formularios tenant-scoped.
- URLs: no UUID tenant en query — ver [SaaS — IDs sensibles fuera de la URL](#saas--ids-sensibles-fuera-de-la-url) más abajo.

### Prohibiciones backend (resumen)

- `IgnoreQueryFilters()` sin `PlatformQueryReason`
- `company_id` del body como autoridad de tenant
- Rutas con `tenantId` para operaciones del tenant autenticado

---

## Tokens

| Token | Almacenamiento | Rotación |
|-------|----------------|----------|
| Access JWT | Memoria SPA (+ espejo Zustand no persistido) | Corta (config JWT) |
| Refresh opaco | Cookie **httpOnly** `Path=/api` | Cada uso (family chain) |

### Prohibido

- Refresh token en `localStorage` / `sessionStorage`
- Revocar todos los tokens del usuario en replay de una sola familia

---

## Refresh rotation

- **FamilyId** por sesión/dispositivo
- Reuso benigno (< grace) → 401 sin revocar familia
- Reuso sospechoso → `RevokeFamilyAsync` (no logout global)
- Multi-tab: Web Locks `erp-refresh` + BroadcastChannel `erp.auth`
- Frontend: solo `authRefreshManager`

---

## Login

- Login unificado tenant vía `/api/auth/login` (incluye operador platform cuando aplica).
- Logout: `fullLogout()` — stores, sessionStorage, BC `logout`, cookie vía API.

Detalle endpoints y claims: `docs/IDENTITY.md`.

---

## Roles — fuente única (`SecurityRoles` / `isAdminRole`)

| Capa | Fuente única | Uso |
|------|---------------|-----|
| Backend | `ERP.Domain.Kernel.Security.SecurityRoles` (`Admin`, `User`) | Claim `ClaimTypes.Role` del JWT. Bypass de permisos (`*`) cuando `role == SecurityRoles.Admin` |
| Frontend | `frontend/src/access/permissionUi.ts` → `ADMIN_ROLES` / `isAdminRole(role)` | Único helper para gates de página por rol Admin |

**Prohibido**: comparaciones ad-hoc (`role === 'Admin'`, `role.toLowerCase() === 'admin'`, literal `"Admin"`/`"User"` fuera de `SecurityRoles`) en backend o frontend. Cualquier check de rol Admin pasa por `SecurityRoles.Admin` (backend) o `isAdminRole()` (frontend).

Excepción documentada: `Platform.Contracts/Integration/Dtos/CompanyProvisionRequest.cs` (`CreatorRole = "Admin"`) — DTO de integración cross-boundary sin referencia a `ERP.Domain`, fuera del alcance de `SecurityRoles` (ver `ERP_CORE_FREEZE.md`).

No confundir con perfiles de acceso asignables (ej. `"DataEntry"`), que son tenant-scoped y no forman parte de `SecurityRoles`.

---

## Security & Access Contract V1 — LOCKED

Modelo de autorización UI vigente, congelado para esta pasada (Security & Access Governance V1). Cambios a la lista ❌ requieren discusión explícita + actualización de esta sección.

**Modelo actual:**
- **Menú/navegación**: 100% server-driven vía `GET /api/v1/me/menu` (`NavigationBuilder`, filtra por rol + `IEffectivePermissionKeysProvider`; Admin ve todo sin filtro).
- **Gates de página por rol**: único mecanismo permitido es `isAdminRole(user?.role)` (ej. `SecuritySettingsPage`, `ConfigContext` para lectura de config de tenant).
- **`usePermissionsUi().canShow` / `.has`**: compatibility shim, **siempre `true`** — no implementan autorización real. `isAdminRole` del hook refleja el rol real de sesión.
- **`SessionContext.authorization.{roles, permissions}`**: poblado por backend pero **no** consumido aún para gating de UI (reservado para futuro permission-key UI checks).
- **Deny-by-default**: sin sesión / rol no-Admin en páginas con gate → `NoAccessPage` (`common.noAccess`). No hay acceso implícito.

✅ **Permitido** (no requiere revisión arquitectónica):
- Agregar nuevas páginas con gate `isAdminRole(user?.role)` siguiendo el patrón de `SecuritySettingsPage`.
- Agregar nuevos permission keys al catálogo backend (`ERP.Domain/Kernel/Permissions/*.cs`) y al menú server-driven (`UiNavItems.PermissionKey`/`PermissionKeysAnyJson`).
- Agregar tests de acceso (admin / no-admin / sin sesión) siguiendo `SecuritySettingsPage.test.tsx`.

❌ **Restringido** (requiere revisión arquitectónica explícita):
- Implementar lógica real en `canShow`/`has` del hook `usePermissionsUi` sin actualizar esta sección + tests de regresión.
- Crear una segunda fuente de verdad para "es Admin" (nuevo helper, nueva constante, nueva comparación de string).
- Bypasses temporales de autorización (flags, `if (true)`, roles hardcodeados fuera de `SecurityRoles`/`ADMIN_ROLES`).
- Mover el filtrado de menú del backend al frontend.

---

## Guardrails CI

`tools/architecture/check-identity-guardrails.ps1` — ver [ENFORCEMENT.md](./enforcement.md).

---

## SaaS — IDs sensibles fuera de la URL

Producto multi-tenant. Detalle billing/planes: [`docs/archive/SAAS-COMMERCIAL.md`](../archive/SAAS-COMMERCIAL.md) (histórico — ver [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md)).

**Regla de producto:** no colocar UUID de tenant, company ni tokens en `location.search` ni en enlaces compartibles.

### Implementación de referencia

`frontend/src/lib/session/sessionStorageKeys.ts`:

- Prefijo de claves de contexto de navegación: **`erp.saas.`** (`SAAS_SESSION_STORAGE_PREFIX`).
- `fullLogout` limpia todas las claves `erp.saas.*` al cerrar sesión.

### Al extender el producto

- Nueva clave con prefijo **`erp.saas.`** o store existente.
- **No** `?tenantId=`, `?companyId=`, `?data=<uuid>`.
- Migrar URLs legacy: persistir en sessionStorage, quitar query con `replace`.

Cursor hint operativo: `.cursor/rules/saas-navigation-no-sensitive-url.mdc`.

Tarifas SRI: ver [backend.md#tarifas-sri](./backend.md).
