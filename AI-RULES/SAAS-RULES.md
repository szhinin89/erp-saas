# SaaS — IDs sensibles fuera de la URL

Producto multi-tenant. Detalle billing/planes: [`docs/archive/SAAS-COMMERCIAL.md`](../docs/archive/SAAS-COMMERCIAL.md) (histórico — ver [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md)).

---

## IDs de tenant/company fuera de la URL

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

---

## Tarifas SRI

Ver [BACKEND-RULES.md#tarifas-sri](./BACKEND-RULES.md).
