# Seguridad — auth, tokens y multi-tenant

Canónico para identidad y aislamiento. Detalle descriptivo: `docs/IDENTITY.md`, `security/auth/refresh-rotation.md`. Reglas PR: [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md).

---

## Multi-tenant (innegociable)

- `TenantId` / `SubscriberId` desde **JWT/contexto**, nunca como autoridad desde body/query en operaciones tenant-scoped.
- Filtros globales EF por tenant en lecturas.
- Índices únicos con `TenantId` cuando la unicidad es por empresa.
- Cross-tenant solo en flujos plataforma/SuperAdmin autorizados.
- Frontend: no enviar `tenantId` en formularios tenant-scoped.
- URLs: no UUID tenant en query — ver [SAAS-RULES.md](./SAAS-RULES.md).

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

- Login unificado tenant vía `/api/auth/login` (incluye SuperAdmin platform cuando aplica).
- Logout: `fullLogout()` — stores, sessionStorage, BC `logout`, cookie vía API.

Detalle endpoints y claims: `docs/IDENTITY.md`.

---

## Guardrails CI

`tools/architecture/check-identity-guardrails.ps1` — ver [ENFORCEMENT.md](./ENFORCEMENT.md).
