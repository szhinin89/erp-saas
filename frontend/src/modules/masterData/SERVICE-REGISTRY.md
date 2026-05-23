# MasterData — registro de servicios frontend

## Canónico (mantener)

| Archivo | Uso |
|---------|-----|
| `api/businessPartnerService.ts` | HTTP `/api/master/business-partners` |
| `api/businessPartnerFacade.ts` | Strangler, feature flags, pickers |
| `api/operationalLinkResolver.ts` | Metadatos seleccionable / warning |
| `config/masterDataFeatureFlags.ts` | `masterdata.customers.enabled`, `masterdata.suppliers.enabled` |

## Legacy (eliminar tras cutover)

| Archivo | Uso |
|---------|-----|
| `modules/customers/api/customerService.ts` | Ventas operacional |
| `modules/customers/api/customerCatalogService.ts` | Catálogo mismo API |
| `modules/compras/suppliers/api/supplierService.ts` | Compras operacional |

## Regla de lectura nueva

- **Pickers y búsquedas transversales:** `businessPartnerFacade`
- **CRUD pantallas legacy:** servicios legacy hasta migración de pantallas
- **CRUD MasterData:** `businessPartnerFacade` / `businessPartnerService` en `/masterdata/*`
