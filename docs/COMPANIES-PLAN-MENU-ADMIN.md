# Empresas: Plan ↔ menú (SuperAdmin)

Este documento describe la configuración de **planes comerciales** usando el **árbol real del menú** y su relación con el catálogo SaaS.

## Objetivo

Permitir que un SuperAdmin **arme qué módulos/submódulos (ítems del menú)** pertenecen a un **plan comercial** y pueda **incluir/quitar** esas definiciones del plan.

## Pantallas (frontend)

### `GET /companies` → pestaña **Plan ↔ menú**

- UI: `frontend/src/components/saas/CompaniesPlanMenuAssignment.tsx`
- Carga:
  - `GET /api/superadmin/navigation-menu` (árbol de menú)
  - `GET /api/superadmin/saas-plans` (planes + features)
  - `GET /api/superadmin/saas-features` (definiciones SaaS)
- **Editor de menú** (mismo componente que `/superadmin/navigation-menu`): `NavigationBarMenuEditor` + estilos `SuperAdminNavMenuPage.css`; reordenar grupos/ítems, sangría, añadir ítem, expandir/contraer.
  - **Guardar estructura del menú** → `PUT /api/superadmin/navigation-menu/groups/reorder` + `PUT .../items/reorder-levels`; crear ítem → `POST .../navigation-menu/items`.
- **Plan comercial** (columna a la derecha de cada fila del árbol vía `renderItemTrailing` en `NavigationMenuTree.tsx`):
  - **Incluir en plan** / **Quitar del plan** (borrador)
  - **Guardar plan** → `PUT /api/superadmin/saas-plans/{planId}/features`
- Sección “**Definiciones sin enlace al menú**”: features sin match en el árbol.

### `GET /companies` → pestaña **Plan y módulos** → sección “Catálogo módulos y formularios (SaaS)”

- UI: `frontend/src/components/saas/TenantSubscriptionMenuCatalog.tsx`
- Usa el **mismo árbol** del menú para visualizar ítems y permitir incluir/quitar features del plan seleccionado.
- Nota: además muestra badge verde “Empresa” cuando el `moduleKey` del ítem pertenece a los módulos efectivos del tenant (solo lectura).

## Emparejamiento ítem de menú ↔ feature SaaS

Archivo: `frontend/src/modules/saas/navItemToFeatureIds.ts`

Una `SaasFeatureDefinition` se enlaza a un `UiNavItem` cuando coincide alguno de estos criterios:

- **`resourceRef` exacto** con:
  - `permissionKey` del ítem, o
  - algún `permissionKeysAny`, o
  - `routePath` (exacto o sufijo si `resourceRef` empieza por `/`).
- **Prefijo de permiso**: si `permissionKey` o `permissionKeysAny` **empieza por** `${resourceRef}.` (ej. `catalog.products` enlaza con `catalog.products.view`).
- **Fallback por código** (cuando `resourceRef` es null):
  - se transforma `CODE_WITH_UNDERSCORES` → `code.with.underscores` y se trata como prefijo de permiso;
  - se intenta además matchear con el último segmento de la ruta.

**SuperAdmin y permisos:** un SuperAdmin **puede abrir** cualquier pantalla del producto sin depender del plan; eso es **autorización**. La columna «Incluir en plan» / «Quitar del plan» es **comercialización**: solo aparece si hay al menos una fila en **`saas_feature_definitions`** que encaje con el ítem (por `resourceRef`, permiso o reglas de código arriba). Si no hay match, verá el texto *Sin feature enlazada…* — no es un fallo de permisos, falta **definición + enlace** al menú.

Para que el árbol muestre acciones en “Inventario / Productos / Marcas / …” deben existir definiciones SaaS como `PRODUCTS`, `BRANDS`, etc. con `resourceRef` alineado (p. ej. `inventario.products`, `inventario.brands`, …) o con `code` que permita el match (p. ej. `INVENTARIO_TARIFFS` → prefijo `inventario.tariffs` para `inventario.tariffs.view`). Para rutas sin `permissionKey` (p. ej. `/profiles`), use `resourceRef` igual a la ruta (`/profiles`). Migración de referencia: **`SeedSaasFeaturesPlanMenuLinking`** (aranceles, categorización, perfiles).

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
- `PUT /api/superadmin/saas-plans/{planId}/features`

Controlador: `backend/src/ERP.API/Controllers/SaasPlansAdminController.cs`

### Definiciones SaaS (SuperAdmin)

- `GET /api/superadmin/saas-features`
- `POST /api/superadmin/saas-features`
- `PUT /api/superadmin/saas-features/{featureId}`

Controlador: `backend/src/ERP.API/Controllers/SaasFeaturesAdminController.cs`

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
2. Buscar tags/rutas `superadmin/navigation-menu`, `superadmin/saas-plans`, `superadmin/saas-features`, `access` (`superadmin/tenants`), `tenants`.
3. Verificar:
   - Requests esperados (`ReplaceFeaturesBody` con `features[]`: `featureId`, `isIncluded`, `limitPerPeriod`)
   - Respuestas `200` / `400` y, donde aplica, `401` / `403`
   - Seguridad (JWT + rol `SuperAdmin` en endpoints de planes/menú; `PATCH .../subscription` también `SuperAdmin`)

