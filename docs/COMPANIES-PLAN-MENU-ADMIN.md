# Empresas: plan comercial y menú (SuperAdmin)

Este documento describe el flujo vigente de configuración mínima: **plan comercial + menú**, sin edición granular de features desde UI/API SuperAdmin.

## Objetivo

Permitir que un SuperAdmin:

- asigne el **plan comercial** de una empresa,
- configure menú de plan/tenant con el árbol real de navegación,
- mantenga el flujo operativo sin gestión manual de features por plan.

## Pantallas (frontend)

### `GET /companies` → pestaña **Plan ↔ menú**

- UI: `frontend/src/components/saas/CompaniesPlanMenuAssignment.tsx`
- Carga:
  - `GET /api/superadmin/navigation-menu` (árbol de menú)
  - `GET /api/superadmin/saas-plans` (planes)
- **Editor de menú** (mismo componente que `/superadmin/navigation-menu`): `NavigationBarMenuEditor` + estilos `SuperAdminNavMenuPage.css`; reordenar grupos/ítems, sangría, añadir ítem, expandir/contraer.
  - **Guardar estructura del menú** → `PUT /api/superadmin/navigation-menu/groups/reorder` + `PUT .../items/reorder-levels`; crear ítem → `POST .../navigation-menu/items`.
- El árbol se usa para **estructura de navegación**, no para activar/desactivar features de plan.

### `GET /companies` → pestaña **Plan y módulos** → sección “Catálogo módulos y formularios (SaaS)”

- UI: `frontend/src/components/saas/TenantSubscriptionMenuCatalog.tsx`
- Usa el **mismo árbol** del menú para visualización de catálogo y consistencia de navegación.
- Nota: además muestra badge verde “Empresa” cuando el `moduleKey` del ítem pertenece a los módulos efectivos del tenant (solo lectura).

## Estado actual de emparejamiento menú ↔ feature

Archivo: `frontend/src/modules/saas/navItemToFeatureIds.ts`

El proyecto conserva lógica de emparejamiento `UiNavItem` ↔ `SaasFeatureDefinition` para compatibilidad de dominio y lectura, pero **ya no forma parte del flujo editable principal** de SuperAdmin.

- **`resourceRef` exacto** con:
  - `permissionKey` del ítem, o
  - algún `permissionKeysAny`, o
  - `routePath` (exacto o sufijo si `resourceRef` empieza por `/`).
- **Prefijo de permiso**: si `permissionKey` o `permissionKeysAny` **empieza por** `${resourceRef}.` (ej. `catalog.products` enlaza con `catalog.products.view`).
- **Fallback por código** (cuando `resourceRef` es null):
  - se transforma `CODE_WITH_UNDERSCORES` → `code.with.underscores` y se trata como prefijo de permiso;
  - se intenta además matchear con el último segmento de la ruta.

**SuperAdmin y permisos:** un SuperAdmin **puede abrir** pantallas globales por rol. El control comercial operativo se concentra en `planCode` del tenant y configuración de menú.

La migración histórica `SeedSaasFeaturesPlanMenuLinking` se mantiene como referencia técnica; no es requisito para operar el flujo mínimo actual.

## Endpoints (backend) y Swagger

### Menú (SuperAdmin)

- `GET /api/superadmin/navigation-menu`
  - Retorna `{ menu }` con grupos y árbol recursivo de ítems.
- `PUT /api/superadmin/navigation-menu/groups/reorder`
- `PUT /api/superadmin/navigation-menu/items/reorder-levels`
- `POST /api/superadmin/navigation-menu/items`

Controlador: `backend/src/ERP.API/Controllers/SuperAdminController.cs`

### Planes SaaS (SuperAdmin)

- `GET /api/superadmin/saas-plans`
- `POST /api/superadmin/saas-plans`
- `PUT /api/superadmin/saas-plans/{planId}`
- `DELETE /api/superadmin/saas-plans/{planId}`
- `PUT /api/superadmin/saas-plans/reorder`
- `PUT /api/superadmin/saas-plans/{planId}/recommended`

Controlador: `backend/src/ERP.API/Controllers/SaasPlansAdminController.cs`

### Empresas (lista + suscripción del tenant)

| Método | Ruta | Uso en UI |
|--------|------|-----------|
| `GET` | `/api/access/superadmin/tenants` | Lista empresas en `/companies` (vía `companyService` / access). |
| `POST` | `/api/access/superadmin/tenants` | Alta empresa + admin inicial. |
| `GET` | `/api/tenants/{id}` | Detalle de empresa seleccionada. |
| `PATCH` | `/api/tenants/{id}/company` | Datos de empresa (pestaña Datos). |
| `PATCH` | `/api/tenants/{id}/subscription` | `planCode` + `enabledModules` (pestaña Plan y módulos). |

Controladores: `AccessController.cs` (prefijo `api/access`), `TenantsController.cs` (`api/tenants`).

## Verificación rápida en Swagger

1. Levantar backend y abrir Swagger UI (ver `docs/DESARROLLO.md`).
2. Buscar tags/rutas `superadmin/navigation-menu`, `superadmin/saas-plans`, `access` (`superadmin/tenants`), `tenants`.
3. Verificar:
   - Requests esperados de planes, menú y suscripción de tenant
   - Respuestas `200` / `400` y, donde aplica, `401` / `403`
   - Seguridad (JWT + rol `SuperAdmin` en endpoints de planes/menú; `PATCH .../subscription` también `SuperAdmin`)

