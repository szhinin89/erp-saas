# SaaS — reglas comerciales y navegación

Producto multi-tenant comercial. Detalle billing: `docs/SAAS-COMMERCIAL.md`. Naming platform: [`docs/platform/TEAM-NAMING-GUIDE.md`](../docs/platform/TEAM-NAMING-GUIDE.md).

---

## IDs de tenant fuera de la URL

**Regla de producto:** no colocar UUID de tenant (ni tokens) en `location.search` ni enlaces compartibles.

### Implementación de referencia

`frontend/src/navigation/companiesTenantDetailNav.ts`:

| Clave sessionStorage | Uso |
|---------------------|-----|
| `erp.saas.companies.detailTenantId` | Ficha Datos empresa (operador platform) |
| `erp.saas.companies.subscriptionTenantId` | Pestaña Plan y módulos |

Funciones: `goToCompaniesTenantDetail`, `goToCompaniesTenantSubscription` → `navigate('/companies')` sin query.

### Al extender el producto

- Nueva clave con prefijo **`erp.saas.`** o store existente.
- **No** `?tenantId=`, `?data=<uuid>`, `?subscription=<uuid>`.
- Migrar URLs legacy: persistir en sessionStorage, quitar query con `replace`.

Cursor hint operativo: `.cursor/rules/saas-navigation-no-sensitive-url.mdc`.

---

## Asignación a planes

**Alcance:** nuevo **módulo** (nav, permiso raíz, pantalla) o **formulario/pantalla** (ruta, CRUD).

### Regla obligatoria

1. **Módulo nuevo:** antes de cerrar, **preguntar** en qué planes SaaS incluirlo (`SaasFeatureDefinition` tipo **Module** + `saas_plan_features`). No asumir "todos los planes".
2. **Formulario nuevo** bajo módulo existente: **preguntar** planes del formulario (puede diferir del módulo padre).
3. Flujo: `SaasFeatureDefinition` (`kind` + `resourceRef`) → asignar planes → enlazar menú/permisos.

Operador platform → Planes SaaS exige **≥1 plan** al alta Module/Form.

---

## Impersonación operador platform

Ver banner en [FRONTEND-RULES.md](./FRONTEND-RULES.md#zh-form-system).

---

## Tarifas SRI

Ver [BACKEND-RULES.md#tarifas-sri](./BACKEND-RULES.md).
