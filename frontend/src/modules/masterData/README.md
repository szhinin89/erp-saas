# MasterData frontend (strangler)

## Flujo de coexistencia

```
UI picker / búsqueda
    → businessPartnerFacade
        → GET /api/master/business-partners + legacyCustomerId / legacySupplierId en DTO
CRUD operacional legacy
    → customerService / supplierService (sin cambios)
CRUD MasterData (coexistente)
    → /masterdata/customers | /masterdata/suppliers
```

> Nota: el gating por feature flag (`masterdata.customers.enabled`/`masterdata.suppliers.enabled` vía entitlements) descrito en versiones anteriores de este documento ya no existe en `businessPartnerFacade.ts` — ver sección "Feature flags" más abajo.

## Operational link

Los pickers **no** emparejan por `identificationNumber` en frontend. Usan campos API:

- `legacyCustomerId` → `CustomerId` en facturas
- `legacySupplierId` → `SupplierId` en compras

Sin vínculo: fila visible, **no seleccionable**, warning en UI y log `[erp.masterdata.picker]` en DEV.

## Feature flags (OBSOLETO — sin integración actual)

> **Estado:** no implementado. `businessPartnerFacade.ts` no lee ningún feature flag hoy — siempre usa MasterData V2 (`GET /api/master/business-partners`), sin fallback condicional. Las claves `masterdata.customers.enabled`/`masterdata.suppliers.enabled` y el endpoint `GET /api/subscribers/entitlements/me` documentados en versiones anteriores de este archivo correspondían a un mecanismo de entitlements que fue eliminado (el endpoint no existe en el backend actual, y `frontend/src/modules/auth/api/entitlementsService.ts` fue removido del frontend). Si se reintroduce gating por flag, documentar aquí la fuente real vigente en ese momento.

## Observabilidad (DEV)

- Headers: `x-correlation-id`, `x-company-session-version`
- Logs: `[erp.api]`, `[erp.masterdata.picker]`, `[erp.session]` — sin JWT

## Endpoints

| Servicio                 | Ruta                            |
| ------------------------ | ------------------------------- |
| `businessPartnerService` | `/api/master/business-partners` |
| Legacy clientes          | `/api/sales/customers`          |
| Legacy proveedores       | `/api/purchases/suppliers`      |

## Rutas UI

| Ruta                    | Página                    |
| ----------------------- | ------------------------- |
| `/masterdata/customers` | `MasterDataCustomersPage` |
| `/masterdata/suppliers` | `MasterDataSuppliersPage` |
| `/sales/customers`      | Legacy                    |

## Rollback

1. Quitar `masterdata.*.enabled` del plan tenant.
2. Revertir imports facade en pickers si es necesario.
3. Ver `docs/masterdata/FRONTEND-MIGRATION-STATUS.md`.
