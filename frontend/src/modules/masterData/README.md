# MasterData frontend (strangler)

## Flujo de coexistencia

```
UI picker / búsqueda
    → businessPartnerFacade
        → feature flag masterdata.customers.enabled | masterdata.suppliers.enabled (entitlements)
        → (ON) GET /api/master/business-partners + legacyCustomerId / legacySupplierId en DTO
        → (OFF o 403) fallback /api/sales/customers | /api/purchases/suppliers
CRUD operacional legacy
    → customerService / supplierService (sin cambios)
CRUD MasterData (coexistente)
    → /masterdata/customers | /masterdata/suppliers
```

## Operational link

Los pickers **no** emparejan por `identificationNumber` en frontend. Usan campos API:

- `legacyCustomerId` → `CustomerId` en facturas
- `legacySupplierId` → `SupplierId` en compras

Sin vínculo: fila visible, **no seleccionable**, warning en UI y log `[erp.masterdata.picker]` en DEV.

## Feature flags (entitlements)

| Clave | Efecto |
|-------|--------|
| `masterdata.customers.enabled` | Pickers ventas usan MasterData |
| `masterdata.suppliers.enabled` | Pickers compras/gastos usan MasterData |

Fuente: `GET /api/subscribers/entitlements/me` → `enabledFeatures`. Sin flag → solo legacy.

## Observabilidad (DEV)

- Headers: `x-correlation-id`, `x-company-session-version`
- Logs: `[erp.api]`, `[erp.masterdata.picker]`, `[erp.session]` — sin JWT

## Endpoints

| Servicio | Ruta |
|----------|------|
| `businessPartnerService` | `/api/master/business-partners` |
| Legacy clientes | `/api/sales/customers` |
| Legacy proveedores | `/api/purchases/suppliers` |

## Rutas UI

| Ruta | Página |
|------|--------|
| `/masterdata/customers` | `MasterDataCustomersPage` |
| `/masterdata/suppliers` | `MasterDataSuppliersPage` |
| `/sales/customers` | Legacy |

## Rollback

1. Quitar `masterdata.*.enabled` del plan tenant.
2. Revertir imports facade en pickers si es necesario.
3. Ver `docs/masterdata/FRONTEND-MIGRATION-STATUS.md`.
