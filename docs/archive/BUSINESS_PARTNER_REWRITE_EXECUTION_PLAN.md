# BUSINESS_PARTNER_REWRITE EXECUTION PLAN
### ERP SaaS — ZH Technologies
**Versión:** 2.0 (post-implementación)  
**Estado:** ✅ Backend completo · ✅ Frontend completo · ✅ Build verde · ✅ BD aplicada  
**Build:** `dotnet build` → 0 errores · `npx tsc --noEmit` → Exit 0

---

## 1. CONTEXTO Y OBJETIVO

### Problema que se resolvía
El modelo anterior (V1) tenía:
- `CustomerProfile` y `SupplierProfile` como entidades separadas por cada tipo de rol
- Flags boolean `isCustomer`/`isSupplier` en el DTO
- `legacyCustomerId`/`legacySupplierId` como puente hacia entidades legacy
- Pickers de cliente/proveedor con `pickerMeta.selectable = false` → **todos deshabilitados**
- Email/Phone/LegalRepresentativeName en la entidad raíz de identidad

### Solución implementada (V2)
- **Identity → Role → Operational Configuration** (patrón ERP empresarial)
- `BusinessPartnerRole` como entidad extensible: agregar rol = 1 valor en enum, no nueva tabla
- `businessPartnerId` como ID único canónico en todos los documentos
- Email/Phone/LegalRep → `BusinessPartnerContact`
- `CompanyBpSettings` → `CompanyBpTradingSettings` con Block/Unblock auditado

---

## 2. INVENTARIO COMPLETO DE ARCHIVOS

### 2.1 DOMINIO (ERP.Domain/MasterData)

| Archivo | Tipo | Estado |
|---|---|---|
| `Entities/BusinessPartner.cs` | Aggregate Root | ✅ Reescrito |
| `Entities/BusinessPartnerRole.cs` | Aggregate Root | ✅ Nuevo |
| `Entities/BusinessPartnerLocation.cs` | Aggregate Root | ✅ Refactorizado |
| `Entities/BusinessPartnerContact.cs` | Aggregate Root | ✅ Refactorizado |
| `Entities/CompanyBpTradingSettings.cs` | Aggregate Root | ✅ Nuevo (renombrado) |
| `Entities/CustomerProfile.cs` | Entidad | ❌ **ELIMINADO** |
| `Entities/SupplierProfile.cs` | Entidad | ❌ **ELIMINADO** |
| `Entities/CompanyBusinessPartnerSettings.cs` | Entidad | ❌ **ELIMINADO** (renombrado) |
| `Enums/RoleType.cs` | Enum | ✅ Nuevo |
| `Enums/PersonType.cs` | Enum | ✅ Nuevo |
| `Enums/LocationType.cs` | Enum | ✅ Nuevo (extraído) |
| `Enums/LocationPurpose.cs` | Flags Enum | ✅ Nuevo |
| `Enums/ContactRole.cs` | Enum | ✅ Nuevo (extraído) |
| `ValueObjects/TaxIdentification.cs` | VO | ✅ Conservado |
| `ValueObjects/PersonName.cs` | VO | ✅ Nuevo |
| `ValueObjects/PhysicalAddress.cs` | VO | ✅ Nuevo |
| `ValueObjects/ContactInfo.cs` | VO | ✅ Nuevo |
| `ValueObjects/SupplierRoleConfig.cs` | VO | ✅ Nuevo |
| `ValueObjects/CarrierRoleConfig.cs` | VO | ✅ Nuevo |
| `Events/BusinessPartnerEvents.cs` | Domain Events | ✅ Nuevo (5 eventos) |
| `Events/BusinessPartnerRoleEvents.cs` | Domain Events | ✅ Nuevo (4 eventos) |
| `Events/BusinessPartnerLocationEvents.cs` | Domain Events | ✅ Nuevo (4 eventos) |
| `Events/BusinessPartnerContactEvents.cs` | Domain Events | ✅ Nuevo (4 eventos) |
| `Interfaces/IBusinessPartnerRepository.cs` | Interfaz | ✅ Refactorizado |
| `Interfaces/IBusinessPartnerRoleRepository.cs` | Interfaz | ✅ Nuevo |
| `Interfaces/IBusinessPartnerLocationRepository.cs` | Interfaz | ✅ Refactorizado |
| `Interfaces/IBusinessPartnerContactRepository.cs` | Interfaz | ✅ Refactorizado |
| `Interfaces/ICompanyBpTradingSettingsRepository.cs` | Interfaz | ✅ Nuevo |
| `Interfaces/ICustomerProfileRepository.cs` | Interfaz | ❌ **ELIMINADO** |
| `Interfaces/ISupplierProfileRepository.cs` | Interfaz | ❌ **ELIMINADO** |
| `Interfaces/ICompanyBpSettingsRepository.cs` | Interfaz | ❌ **ELIMINADO** |

**Total dominio: 25 archivos · 8 eliminados · 10 nuevos · 7 refactorizados**

### 2.2 APLICACIÓN (ERP.Application/MasterData)

| Use Case | Archivos | Estado |
|---|---|---|
| `CreateBusinessPartner/` | Command + Handler + Validator | ✅ Reescrito |
| `UpdateBusinessPartner/` | Commands (2) + Handlers (2) + Validators (2) | ✅ Reescrito |
| `ActivateBusinessPartner/` | Command + Handler | ✅ Actualizado |
| `DeactivateBusinessPartner/` | Command + Handler | ✅ Nuevo (renombrado) |
| `GetBusinessPartner/` | Query + Handler | ✅ Reescrito |
| `SearchBusinessPartners/` | Query + Handler | ✅ Reescrito |
| `AssignBusinessPartnerRole/` | Command + Handler + Validator | ✅ Nuevo |
| `RevokeBusinessPartnerRole/` | Command + Handler | ✅ Nuevo |
| `UpdateRoleConfig/` | Commands (3) + Handlers (3) + Validators (2) | ✅ Nuevo |
| `GetBusinessPartnerRoles/` | Query + Handler | ✅ Nuevo |
| `BpLocations/BpLocationUseCases.cs` | 5 Commands + 2 Queries + 7 Handlers + 2 Validators | ✅ Reescrito |
| `BpContacts/BpContactUseCases.cs` | 5 Commands + 2 Queries + 7 Handlers + 2 Validators | ✅ Reescrito |
| `UpsertCompanyBpTradingSettings/` | Command + Handler + Validator | ✅ Nuevo |
| `BlockBusinessPartner/` | Command + Handler + Validator | ✅ Nuevo |
| `UnblockBusinessPartner/` | Command + Handler | ✅ Nuevo |
| `GetCompanyBpTradingSettings/` | Query + Handler | ✅ Nuevo |
| `DisableBusinessPartner/` | — | ❌ **ELIMINADO** (→ Deactivate) |
| `AddBusinessPartnerRole/` | — | ❌ **ELIMINADO** (→ Assign) |
| `UpdateCustomerNotes/` | — | ❌ **ELIMINADO** (→ UpdateRoleConfig) |
| `UpdateSupplierProfile/` | — | ❌ **ELIMINADO** (→ UpdateRoleConfig) |
| `UpsertCompanyBpSettings/` | — | ❌ **ELIMINADO** (→ TradingSettings) |
| `GetCompanyBpSettings/` | — | ❌ **ELIMINADO** (→ TradingSettings) |
| `DTOs/BusinessPartnerDtos.cs` | Summary + Detail + Role + Config DTOs | ✅ Nuevo |
| `DTOs/BpLocationDtos.cs` | Location + Contact DTOs | ✅ Reescrito |
| `DTOs/CompanyBpTradingSettingsDto.cs` | Settings DTO | ✅ Nuevo |
| `DTOs/BusinessPartnerDto.cs` (flat legacy) | — | ❌ **ELIMINADO** |
| `DTOs/CompanyBpSettingsDto.cs` | — | ❌ **ELIMINADO** |
| `IBusinessPartnerOperationalLinkEnricher.cs` | — | ❌ **ELIMINADO** |

**Total aplicación: 40 archivos · 7 folders eliminados · 17 use cases nuevos**

### 2.3 INFRAESTRUCTURA (ERP.Infrastructure)

| Archivo | Estado |
|---|---|
| `Persistence/Configurations/MasterData/BusinessPartnerConfiguration.cs` | ✅ Reescrito |
| `Persistence/Configurations/MasterData/BusinessPartnerRoleConfiguration.cs` | ✅ Nuevo |
| `Persistence/Configurations/MasterData/BusinessPartnerLocationConfiguration.cs` | ✅ Reescrito |
| `Persistence/Configurations/MasterData/BusinessPartnerContactConfiguration.cs` | ✅ Reescrito |
| `Persistence/Configurations/MasterData/CompanyBpTradingSettingsConfiguration.cs` | ✅ Nuevo |
| `Persistence/Configurations/MasterData/CustomerProfileConfiguration.cs` | ❌ **ELIMINADO** |
| `Persistence/Configurations/MasterData/SupplierProfileConfiguration.cs` | ❌ **ELIMINADO** |
| `Persistence/Configurations/MasterData/CompanyBusinessPartnerSettingsConfiguration.cs` | ❌ **ELIMINADO** |
| `Persistence/Migrations/20260604030758_InitialSchemaV2.cs` | ✅ Nuevo (baseline) |
| `Persistence/Migrations/20260604031139_BusinessPartnerV2.cs` | ✅ Nuevo (constraints SQL) |
| `MasterData/Repositories/BusinessPartnerRepository.cs` | ✅ Reescrito |
| `MasterData/Repositories/BusinessPartnerRoleRepository.cs` | ✅ Nuevo |
| `MasterData/Repositories/BusinessPartnerLocationRepository.cs` | ✅ Reescrito |
| `MasterData/Repositories/BusinessPartnerContactRepository.cs` | ✅ Reescrito |
| `MasterData/Repositories/CompanyBpTradingSettingsRepository.cs` | ✅ Nuevo |
| `MasterData/Repositories/CustomerProfileRepository.cs` | ❌ **ELIMINADO** |
| `MasterData/Repositories/SupplierProfileRepository.cs` | ❌ **ELIMINADO** |
| `MasterData/Repositories/CompanyBpSettingsRepository.cs` | ❌ **ELIMINADO** |
| `MasterData/BusinessPartnerOperationalLinkEnricher.cs` | ❌ **ELIMINADO** |
| `MasterData/Reconciliation/BusinessPartnerReconciliationService.cs` | ✅ Reescrito (queries V2) |
| `Seeding/SubscriberOnboardingService.cs` | ✅ Actualizado (Consumidor Final → BusinessPartnerRole) |

### 2.4 API (ERP.API)

| Archivo | Estado |
|---|---|
| `Controllers/BusinessPartnersController.cs` | ✅ Reescrito |
| `Controllers/BusinessPartnerRolesController.cs` | ✅ Nuevo |
| `Controllers/BusinessPartnerLocationsController.cs` | ✅ Reescrito |
| `Controllers/BusinessPartnerContactsController.cs` | ✅ Nuevo |
| `Controllers/CompanyBpTradingSettingsController.cs` | ✅ Nuevo |
| `Controllers/BusinessPartnerProfilesController.cs` | ❌ **ELIMINADO** |
| `Contracts/MasterData/BusinessPartnerApiContracts.cs` | ✅ Reescrito |
| `Health/MasterDataSyncHealthCheck.cs` | ✅ Actualizado (CustomerProfiles → BusinessPartnerRoles) |

### 2.5 FRONTEND (frontend/src/modules/masterData)

| Archivo | Estado |
|---|---|
| `types/businessPartner.types.ts` | ✅ Reescrito (V2 completo) |
| `types/pickerRow.types.ts` | ✅ Reescrito (sin legacyId) |
| `types/operationalLink.types.ts` | ❌ **ELIMINADO** |
| `api/businessPartnerService.ts` | ✅ Reescrito |
| `api/businessPartnerFacade.ts` | ✅ Reescrito |
| `api/customerProfileService.ts` | ❌ **ELIMINADO** |
| `api/supplierProfileService.ts` | ❌ **ELIMINADO** |
| `api/operationalLinkResolver.ts` | ❌ **ELIMINADO** |
| `api/operationalLinkResolver.test.ts` | ❌ **ELIMINADO** |
| `adapters/businessPartnerCustomerAdapter.ts` | ✅ Reescrito |
| `adapters/businessPartnerSupplierAdapter.ts` | ✅ Reescrito |
| `store/masterDataPartnerUiStore.ts` | ✅ Actualizado |
| `pages/MasterDataCustomersPage.tsx` | ✅ Reescrito |
| `pages/MasterDataSuppliersPage.tsx` | ✅ Reescrito |
| `pages/useMasterDataCustomersPage.ts` | ✅ Reescrito |
| `pages/useMasterDataSuppliersPage.ts` | ✅ Reescrito |
| `pages/MasterDataBpFormModal.tsx` | ✅ Reescrito |
| `pages/MasterDataBusinessPartnerDetailPage.tsx` | ✅ Reescrito |
| `pages/MasterDataCompanySettingsModal.tsx` | ✅ Reescrito |
| `pages/MasterDataCustomerNotesModal.tsx` | ❌ **ELIMINADO** |
| `pages/MasterDataSupplierProfileModal.tsx` | ❌ **ELIMINADO** |
| `components/MasterDataBpFormFields.tsx` | ✅ Reescrito |
| `components/MasterDataPartnerListTab.tsx` | ✅ Reescrito |
| `components/MasterDataPartnerResumenTab.tsx` | ✅ Reescrito |
| `components/MasterDataPartnerWizard.tsx` | ✅ Reescrito |
| `schemas/businessPartnerSchema.ts` | ✅ Actualizado |

### 2.6 FRONTEND — MÓDULOS DE DOCUMENTOS (impactados)

| Archivo | Cambio |
|---|---|
| `ventas/api/ventasFacturasService.ts` | `customerId` → `businessPartnerId` |
| `ventas/pages/CreateInvoicePage.tsx` | Picker sin `pickerMeta`; usa `c.id` |
| `ventas/cotizaciones/pages/CreateQuotePage.tsx` | Picker sin `pickerMeta` |
| `ventas/ordenes/pages/CreateSalesOrderPage.tsx` | Picker sin `pickerMeta` |
| `compras/facturas/api/comprasService.ts` | `supplierId` → `businessPartnerId` |
| `compras/facturas/pages/CrearCompraPage.tsx` | Picker sin `pickerMeta` |
| `compras/ordenes/pages/CrearOrdenCompraPage.tsx` | `SupplierPickerOption` → `SupplierPickerRow` |
| `gastos/api/gastosService.ts` | `supplierId` → `businessPartnerId` |
| `gastos/pages/CrearGastoPage.tsx` | Picker sin `pickerMeta` |
| `compras/facturas/pages/ComprasListPage.tsx` | Mapa por `businessPartnerId` |
| `gastos/pages/GastosListPage.tsx` | Mapa por `businessPartnerId` |
| `lib/observability/devApiLog.ts` | Eliminados `legacyCustomerId/SupplierId` |

---

## 3. QUÉ SE ELIMINA

### Backend

| Componente | Razón |
|---|---|
| `CustomerProfile` entity | Reemplazada por `BusinessPartnerRole` con `RoleType.Customer` |
| `SupplierProfile` entity | Reemplazada por `BusinessPartnerRole` con `RoleType.Supplier` |
| `CompanyBusinessPartnerSettings` entity | Renombrada + ampliada → `CompanyBpTradingSettings` |
| `ICustomerProfileRepository` | Entidad eliminada |
| `ISupplierProfileRepository` | Entidad eliminada |
| `ICompanyBpSettingsRepository` | Renombrada → `ICompanyBpTradingSettingsRepository` |
| `IBusinessPartnerOperationalLinkEnricher` | Concepto legacy (bridge legacy IDs) eliminado |
| `DisableBusinessPartnerCommand` | Renombrado → `DeactivateBusinessPartnerCommand` |
| `AddBusinessPartnerRoleCommand` | Reemplazado → `AssignBusinessPartnerRoleCommand` (UPSERT semántico) |
| `UpdateCustomerNotesCommand` | Reemplazado → `UpdateRoleNotesCommand` (aplica a cualquier rol) |
| `UpdateSupplierProfileCommand` | Reemplazado → `UpdateSupplierRoleConfigCommand` |
| `UpsertCompanyBpSettingsCommand` | Renombrado → `UpsertCompanyBpTradingSettingsCommand` |
| `GetCompanyBpSettingsQuery` | Renombrado → `GetCompanyBpTradingSettingsQuery` |
| `BusinessPartnerDto` (flat mega-DTO) | Dividido → `BusinessPartnerSummaryDto` + `BusinessPartnerDetailDto` |
| `CompanyBpSettingsDto` | Renombrado → `CompanyBpTradingSettingsDto` |
| `BusinessPartnerProfilesController` | Fragmentado en controllers específicos |
| Todas las migraciones antiguas (52 archivos) | Reemplazadas por 2 migraciones limpias |
| Tablas BD: `master_customer_profiles`, `master_supplier_profiles`, `master_company_bp_settings` | Reemplazadas por V2 |

### Frontend

| Componente | Razón |
|---|---|
| `operationalLink.types.ts` | Concepto `legacyOperationalId` eliminado |
| `operationalLinkResolver.ts` + tests | `legacyCustomerId/SupplierId` ya no existen |
| `customerProfileService.ts` | Entidad CustomerProfile eliminada del backend |
| `supplierProfileService.ts` | Entidad SupplierProfile eliminada del backend |
| `MasterDataCustomerNotesModal.tsx` | Endpoint `/customer-notes` eliminado del backend |
| `MasterDataSupplierProfileModal.tsx` | Endpoint `/supplier-profile` eliminado del backend |
| `BusinessPartnerDto` (legacy flat) | Reemplazado por `BusinessPartnerSummaryDto`/`BusinessPartnerDetailDto` |
| `CompanyBpSettingsDto` | Renombrado |
| `UpdateSupplierProfileBody` | Reemplazado por `SupplierConfigBody` |

---

## 4. QUÉ SE RENOMBRA

| Original | Nuevo |
|---|---|
| `CompanyBusinessPartnerSettings` | `CompanyBpTradingSettings` |
| `master_company_bp_settings` (tabla) | `master_company_bp_trading_settings` |
| `ICompanyBpSettingsRepository` | `ICompanyBpTradingSettingsRepository` |
| `CompanyBpSettingsRepository` | `CompanyBpTradingSettingsRepository` |
| `CompanyBpSettingsDto` | `CompanyBpTradingSettingsDto` |
| `DisableBusinessPartnerCommand` | `DeactivateBusinessPartnerCommand` |
| `AddBusinessPartnerRoleCommand` | `AssignBusinessPartnerRoleCommand` |
| `/api/.../company-settings` endpoint | `/api/.../trading-settings` |
| `BusinessPartnerDto` (flat) | `BusinessPartnerSummaryDto` (search) + `BusinessPartnerDetailDto` (detail) |
| `isCustomer: true` (query filter) | `roles: [1]` (`RoleType.Customer`) |
| `isSupplier: true` (query filter) | `roles: [2]` (`RoleType.Supplier`) |

---

## 5. QUÉ SE CONSERVA SIN CAMBIO

| Componente | Justificación |
|---|---|
| `TaxIdentification` VO | Validación RUC/CI ecuatoriana correcta |
| `ContactRole` enum (10 valores) | Completo y correcto |
| `MasterDataPartnerToast.tsx` | Independiente de tipos BP |
| `useSriIdTypes.ts` | Catálogo SRI sin relación con BP |
| Módulos no-MasterData (Inventario, Contabilidad, etc.) | Sin dependencias directas de BP |
| `ErpDbContext.SaveChangesAsync` con outbox | Publica domain events correctamente |
| `EnterpriseQueryFilterConfigurator` | Aplica filtros automáticamente |
| `IDatabaseExceptionTranslator` | Manejo de constraints PostgreSQL |
| Suite de security tests (29 tests) | Válida sin modificación |

---

## 6. ORDEN EXACTO DE IMPLEMENTACIÓN

```
FASE 01 — Auditoría arquitectónica          [COMPLETADO]
FASE 02 — Domain Blueprint                  [COMPLETADO]
FASE 03 — Modelo relacional                 [COMPLETADO]
FASE 04 — Validación arquitectónica         [COMPLETADO]
FASE 05 — Domain: Entidades + VOs + Events  [COMPLETADO]
FASE 06 — EF Core Configurations            [COMPLETADO]
FASE 07 — Repositorios                      [COMPLETADO]
FASE 08 — CQRS: Commands + Handlers + Validators [COMPLETADO]
FASE 09 — API: Controllers + Contracts      [COMPLETADO]
FASE 10 — Frontend Impact Analysis          [COMPLETADO]
FASE 11 — Execution Plan                    [COMPLETADO]
FASE 12 — Deletion Phase (deuda técnica)    [COMPLETADO]
FASE 13 — Domain Rebuild + Migration        [COMPLETADO]
FASE 14 — Persistence: Migration + BD       [COMPLETADO]
FASE 15 — Application Rebuild               [COMPLETADO]
FASE 16 — API Rebuild                       [COMPLETADO]
FASE 17 — Contract Validation               [COMPLETADO]
FASE 18 — Frontend Foundation (types, service, facade, adapters) [COMPLETADO]
FASE 19 — Master Data UI                    [COMPLETADO]
FASE 20 — Document Integration              [COMPLETADO]
FASE 21 — ERP Integrity Audit               [COMPLETADO]
FASE 22 — UAT + Execution Plan              [ESTE DOCUMENTO]
```

**Regla de oro:** Nunca saltar de Fase N sin que `dotnet build` y `npx tsc --noEmit` pasen en verde.

---

## 7. DEPENDENCIAS ENTRE MÓDULOS

```
ERP.Domain (no dependencies)
    │
    ▼
ERP.Application (depends: Domain)
    │
    ▼
ERP.Infrastructure (depends: Application + Domain)
    │
    ▼
ERP.API (depends: Infrastructure + Application + Domain)
    │
    ▼
Frontend businessPartner.types.ts (depends: API contracts)
    │
    ├── businessPartnerService.ts     (depends: types)
    ├── businessPartnerFacade.ts      (depends: service + adapters)
    ├── adapters/                     (depends: types)
    ├── store/                        (depends: types)
    ├── pages/hooks (useMasterData*)  (depends: facade)
    ├── pages/components              (depends: hooks + types)
    │
    ▼
Document modules (ventas, compras, gastos)
    │
    └── Use businessPartnerId from picker facade
```

**Regla de dependencias crítica:** Los módulos de documentos (Ventas, Compras, Gastos) NUNCA deben importar directamente tipos de dominio del backend. Deben ir solo a través del `businessPartnerFacade.ts`.

---

## 8. IMPACTO EN VENTAS

### Cambios en payload de creación

| Antes (V1) | Ahora (V2) |
|---|---|
| `customerId: string` (legacy ID) | `businessPartnerId: string` (BP ID) |
| `businessPartnerId?: string \| null` (opcional) | — (eliminado, ahora es el principal) |
| Picker con `pickerMeta.selectable = false` → todos deshabilitados | Picker con `id = businessPartnerId` → todos habilitados |

### Filtros de búsqueda

| Antes | Ahora |
|---|---|
| `?isCustomer=true` | `?roles=1` (RoleType.Customer = 1) |
| Paginación devolvía `BusinessPartnerDto` flat | Paginación devuelve `BusinessPartnerSummaryDto` limpio |

### Archivos de ventas modificados
- `ventasFacturasService.ts`: `CreateSaleRequest.customerId` → `businessPartnerId`
- `CreateInvoicePage.tsx`: picker usa `c.id` directamente
- `CreateQuotePage.tsx`: picker sin `pickerMeta.selectable` check
- `CreateSalesOrderPage.tsx`: ídem

### Impacto en documentos históricos
- Documentos anteriores al V2 tienen `clienteId` en la respuesta del backend (campo legacy)
- Los nuevos documentos tienen `businessPartnerId`
- El mapa de nombres (`supplierMap`, `clienteMap`) usa `businessPartnerId` primero, fallback a ID legacy

---

## 9. IMPACTO EN COMPRAS

### Cambios en payload de creación

| Antes (V1) | Ahora (V2) |
|---|---|
| `supplierId: string` (legacy ID) | `businessPartnerId: string` (BP ID) |
| `businessPartnerId?: string \| null` | — |
| Picker con `pickerMeta.legacyOperationalId` como value | Picker con `id` como value |

### Archivos de compras modificados
- `comprasService.ts`: `CrearCompraManualRequest.supplierId` → `businessPartnerId`
- `CrearCompraPage.tsx`: estado único `businessPartnerId`
- `CrearOrdenCompraPage.tsx`: `SupplierPickerOption` → `SupplierPickerRow`

### Nuevo endpoint disponible
```
GET  /api/master/business-partners/{bpId}/roles?onlyActive=true
→ Verificar que el proveedor tiene RoleType.Supplier antes de crear compra
```

---

## 10. IMPACTO EN INVENTARIO

### Impacto directo: NINGUNO

Los módulos de Inventario (Stock, Movimientos, Kardex, Transferencias, Ajustes) **no consumen directamente** datos del BusinessPartner. El impacto es indirecto:

- Los documentos de compra (que mueven inventario) ahora usan `businessPartnerId` como ID del proveedor
- Los documentos de venta (que consumen inventario) ahora usan `businessPartnerId` como ID del cliente
- El módulo de inventario en sí no necesita cambios

### Verificación recomendada
1. Crear una compra con proveedor BP → verificar que el movimiento de inventario se crea correctamente
2. Crear una venta con cliente BP → verificar que el movimiento de salida se registra con `businessPartnerId`

---

## 11. ESCENARIOS UAT

### Escenario 1: Crear BP → Customer → Cotización → Factura

```
PRECONDICIÓN: DB limpia (migration aplicada)

PASO 1: POST /api/master/business-partners
  Body: { identificationType: "04", identificationNumber: "1790016919001",
          personType: 2, legalName: "Empresa Test S.A." }
  Esperado: 201 Created · businessPartnerId = UUID_A

PASO 2: POST /api/master/business-partners/{UUID_A}/roles
  Body: { roleType: 1 }  // Customer = 1
  Esperado: 201 Created · role.isActive = true

PASO 3: GET /api/master/business-partners?roles=1
  Esperado: items incluye BP con legalName "Empresa Test S.A."

PASO 4: GET /api/master/business-partners/{UUID_A}
  Esperado: roles[0].roleType = "Customer" · roles[0].isActive = true

PASO 5: POST /api/ventas/cotizaciones (si existe)
  Body: { businessPartnerId: UUID_A, ... }
  Esperado: cotización creada

PASO 6: POST /api/sales/invoices
  Body: { businessPartnerId: UUID_A, warehouseId: ..., items: [...] }
  Esperado: factura creada con businessPartnerId = UUID_A

VALIDACIÓN:
  ✓ BP tiene rol Customer activo
  ✓ Factura referencia UUID_A (businessPartnerId), no un legacyId
  ✓ Picker de cliente en Create Invoice muestra "Empresa Test S.A." habilitado
```

### Escenario 2: Crear BP → Supplier → Orden de Compra → Compra

```
PASO 1: Crear BP con RUC "0991234567001" (Persona Jurídica)
  POST /api/master/business-partners
  Body: { identificationType: "04", identificationNumber: "0991234567001",
          personType: 2, legalName: "Proveedor ABC S.A." }
  Esperado: businessPartnerId = UUID_B

PASO 2: Asignar rol Supplier con defaults SRI
  POST /api/master/business-partners/{UUID_B}/roles
  Body: { roleType: 2,
          supplierConfig: { defaultTaxSupportCode: "01",
                            defaultRetentionVatCode: "725",
                            defaultRetentionIncomeCode: "303",
                            paymentTerms: "30 días" } }
  Esperado: 201 Created · supplierConfig populated

PASO 3: Crear Orden de Compra
  POST /api/purchases/orders (si existe)
  Body: { businessPartnerId: UUID_B, ... }
  Esperado: orden creada

PASO 4: Crear Factura de Compra
  POST /api/purchases/invoices/manual
  Body: { businessPartnerId: UUID_B, invoiceNumber: "001-001-000000001", ... }
  Esperado: compra creada con businessPartnerId = UUID_B

VALIDACIÓN:
  ✓ BP tiene rol Supplier con config SRI correcta
  ✓ Compra usa UUID_B como businessPartnerId
  ✓ Picker de proveedor muestra "Proveedor ABC S.A." habilitado
```

### Escenario 3: Mismo BP con roles Customer + Supplier + Carrier

```
PASO 1: Crear BP (ya creado como Customer en Escenario 1: UUID_A)

PASO 2: Asignar rol Supplier al mismo BP
  POST /api/master/business-partners/{UUID_A}/roles
  Body: { roleType: 2 }
  Esperado: 201 Created (rol nuevo sobre BP existente)

PASO 3: Asignar rol Carrier con config
  POST /api/master/business-partners/{UUID_A}/roles
  Body: { roleType: 4,
          carrierConfig: { transportAuthorizationNumber: "MTOP-2024-001",
                           vehicleCapacityTons: 5.5 } }
  Esperado: 201 Created

PASO 4: Verificar roles
  GET /api/master/business-partners/{UUID_A}/roles?onlyActive=true
  Esperado: roles = [
    { roleType: "Customer", isActive: true },
    { roleType: "Supplier", isActive: true },
    { roleType: "Carrier",  isActive: true, carrierConfig: { ... } }
  ]

PASO 5: Intentar asignar Customer de nuevo (debe fallar)
  POST /api/master/business-partners/{UUID_A}/roles
  Body: { roleType: 1 }
  Esperado: 422 "El rol Customer ya está activo para este BusinessPartner."

PASO 6: Revocar rol Supplier
  DELETE /api/master/business-partners/{UUID_A}/roles/{roleId_Supplier}
  Esperado: 200 · isActive = false (registro conservado para auditoría)

PASO 7: Re-asignar Supplier (UPSERT — debe reactivar)
  POST /api/master/business-partners/{UUID_A}/roles
  Body: { roleType: 2 }
  Esperado: 201 · isActive = true (Reactivate() fue llamado, no Create())

VALIDACIÓN:
  ✓ Un BP puede tener múltiples roles simultáneos
  ✓ Asignar rol activo devuelve 422 (no duplica)
  ✓ Revocar + re-asignar = reactivación (preserva historial)
  ✓ AssignedAt actualizado en reactivación
```

### Escenario 4: TradingSettings por empresa (Company A y Company B)

```
PREREQUISITO: BP = UUID_A, Company A = UUID_CA, Company B = UUID_CB

PASO 1 (Company A context):
  PUT /api/master/business-partners/{UUID_A}/trading-settings
  Headers: X-Company-Id: UUID_CA
  Body: { creditLimit: 5000, paymentDays: 30, creditCurrencyCode: "USD" }
  Esperado: 200 · creditLimit = 5000

PASO 2 (Company B context):
  PUT /api/master/business-partners/{UUID_A}/trading-settings
  Headers: X-Company-Id: UUID_CB
  Body: { creditLimit: 25000, paymentDays: 60, creditCurrencyCode: "USD" }
  Esperado: 200 · creditLimit = 25000

PASO 3: Verificar aislamiento
  GET /api/master/business-partners/{UUID_A}/trading-settings (Company A)
  Esperado: creditLimit = 5000

  GET /api/master/business-partners/{UUID_A}/trading-settings (Company B)
  Esperado: creditLimit = 25000

PASO 4: Bloquear BP en Company A
  PATCH /api/master/business-partners/{UUID_A}/trading-settings/block
  Headers: X-Company-Id: UUID_CA
  Body: { reason: "Deuda vencida 60+ días" }
  Esperado: 200 · isBlocked = true · blockedReason = "Deuda vencida 60+ días"

PASO 5: Verificar bloqueo aislado
  GET .../trading-settings (Company A) → isBlocked = true
  GET .../trading-settings (Company B) → isBlocked = false (sin afectar)

PASO 6: Desbloquear en Company A
  PATCH .../trading-settings/unblock (Company A)
  Esperado: isBlocked = false · blockedReason = null · blockedAt = null

VALIDACIÓN:
  ✓ Cada company tiene su propia configuración comercial
  ✓ Bloquear en Company A no afecta Company B
  ✓ BlockedReason + BlockedAt + BlockedBy auditados correctamente
  ✓ CHECK constraint chk_cbts_block_consistency activo en BD
```

---

## 12. CHECKLIST DE VALIDACIÓN

### Build

- [ ] `dotnet build src/ERP.API/ERP.API.csproj` → 0 errores
- [ ] `dotnet build src/ERP.API.Tests/ERP.API.Tests.csproj` → 0 errores
- [ ] `npx tsc --noEmit` → Exit 0
- [ ] `dotnet ef database update` → migrations aplicadas sin error

### Integridad de BD

- [ ] Tabla `master_business_partners` existe con columnas: `person_type`, sin `email`/`phone`/`legal_representative_name`
- [ ] Tabla `master_bp_roles` existe con `role_type`, `notes`, `assigned_at`, `revoked_at`
- [ ] Tabla `master_bp_supplier_configs` existe (split table OwnsOne)
- [ ] Tabla `master_bp_carrier_configs` existe (split table OwnsOne)
- [ ] Tabla `master_bp_locations` existe sin `company_id`; tiene `location_purpose`
- [ ] Tabla `master_bp_contacts` existe sin `company_id`; tiene `other_description`
- [ ] Tabla `master_company_bp_trading_settings` existe con `blocked_reason`, `blocked_at`, `blocked_by`
- [ ] Índice `uq_mbp_identification` — UNIQUE incondicional (sin WHERE)
- [ ] Índice `uq_bpr_bp_role` — UNIQUE (subscriber_id, business_partner_id, role_type)
- [ ] Índice `uq_bpl_primary` — UNIQUE PARTIAL WHERE is_primary=true AND is_active=true
- [ ] Índice `uq_bpc_primary` — UNIQUE PARTIAL
- [ ] CHECK `chk_mbp_type_person_correlation` — activo
- [ ] CHECK `chk_cbts_block_consistency` — activo
- [ ] CHECK `chk_bpl_geo_hierarchy` — activo
- [ ] Tablas eliminadas: `master_customer_profiles`, `master_supplier_profiles`, `master_company_bp_settings` **NO existen**

### API Endpoints

- [ ] `POST /api/master/business-partners` → crea BP con `personType`, sin `asCustomer`/`asSupplier`
- [ ] `POST /api/master/business-partners/{id}/roles` → asigna rol; 422 si ya activo
- [ ] `DELETE /api/master/business-partners/{id}/roles/{roleId}` → revoca (is_active=false)
- [ ] `GET /api/master/business-partners?roles=1` → filtra Customers
- [ ] `GET /api/master/business-partners?roles=2` → filtra Suppliers
- [ ] `GET /api/master/business-partners/{id}` → incluye `roles[]` en la respuesta
- [ ] `PUT /api/master/business-partners/{id}/trading-settings` → upsert sin `isBlocked`
- [ ] `PATCH .../trading-settings/block` → requiere `reason` (422 si vacío)
- [ ] `PATCH .../trading-settings/unblock` → limpia los 3 campos de audit

### Frontend

- [ ] Picker de cliente NO muestra `disabled={!selectable}` en ninguna opción
- [ ] Picker de cliente devuelve `id = businessPartnerId` (no legacyId)
- [ ] Picker de proveedor ídem
- [ ] Formulario de crear BP muestra `personType` (obligatorio), sin email/phone
- [ ] Crear factura de venta usa `businessPartnerId` en el payload
- [ ] Crear factura de compra usa `businessPartnerId` en el payload
- [ ] Modal de condiciones comerciales tiene botones separados "Bloquear" / "Desbloquear"
- [ ] Detalle del BP muestra 4 tabs: Identidad, Roles, Ubicaciones, Contactos

### Audit de código (0 referencias legacy)

- [ ] `grep -r "legacyCustomerId" src/` → 0 resultados de código activo
- [ ] `grep -r "legacySupplierId" src/` → 0 resultados
- [ ] `grep -r "isCustomer" src/` → 0 resultados en lógica (solo comentarios)
- [ ] `grep -r "isSupplier" src/` → 0 resultados en lógica
- [ ] `grep -r "CustomerProfile" backend/src/ --include="*.cs"` → 0 resultados
- [ ] `grep -r "SupplierProfile" backend/src/ --include="*.cs"` → 0 resultados
- [ ] `grep -r "asCustomer\|asSupplier" frontend/src/` → 0 resultados en lógica
- [ ] `grep -r "pickerMeta" frontend/src/` → 0 resultados en lógica

### Security / Multi-tenant

- [ ] Ejecutar suite 29 security tests: `dotnet test --filter Category=Security`
- [ ] GET /api/master/business-partners sin JWT → 401
- [ ] GET /api/master/business-partners con JWT de tenant X → no devuelve datos de tenant Y
- [ ] POST .../roles con BP de otro tenant → 422 / 404 (query filter bloquea)

### Consumidor Final (seeding)

- [ ] Al onboardar nuevo subscriber → se crea BP "CONSUMIDOR FINAL" con `identificationType="07"`, `identificationNumber="9999999999"`
- [ ] El BP Consumidor Final tiene rol `Customer` asignado
- [ ] `GET /api/master/business-partners?roles=1` devuelve Consumidor Final

---

## 13. DECISIONES ARQUITECTÓNICAS CLAVE (ADRs)

| ADR | Decisión |
|---|---|
| ADR-BP-01 | `BusinessPartnerRole` es AR independiente de `BusinessPartner`. Invariante de unicidad delegada a DB UNIQUE + `IDatabaseExceptionTranslator`. |
| ADR-BP-02 | Locations y Contacts son subscriber-scoped (no company-scoped). Los datos maestros del tercero son del tenant. |
| ADR-BP-03 | `uq_mbp_identification` es ÚNICO INCONDICIONAL. Un RUC extinto no puede reutilizarse. |
| ADR-BP-10 | `BusinessPartnerRole` como AR independiente — eliminado de la colección de `BusinessPartner`. |
| ADR-BP-11 | El único `switch(RoleType)` existe en `BusinessPartnerRole.Create()`. Prohibido en capas superiores. |
| ADR-BP-12 | `AssignRole` tiene semántica UPSERT: si rol existe revocado → `Reactivate()`, si null → `Create()`. |
| ADR-BP-13 | FK compuestos `(business_partner_id, subscriber_id)` garantizan no-cross-tenant a nivel BD. |
| ADR-BP-14 | Revocar rol con documentos activos — TODO diferido hasta que módulo de documentos esté implementado. |
| ADR-BP-15 | `CarrierRoleConfig.vehicle_capacity_tons` simplificación consciente. Gestión de flota = módulo futuro. |

---

## 14. DEUDA TÉCNICA CONOCIDA

| Item | Tipo | Prioridad | Notas |
|---|---|---|---|
| Email del buyer en XML SRI | Funcional | Media | `// Phase 13: buyer email se cargará desde BusinessPartnerContact`. Actualmente `null` en XML. |
| Validar documentos activos antes de revocar rol | Negocio | Alta | TODO `ADR-BP-14` en `RevokeBusinessPartnerRoleHandler.cs`. |
| Merge `Carrier` entity → `BusinessPartnerRole` | Arquitectura | Media | Carrier en Logistics domain aún es entidad separada. |
| `location_purpose` bitmask en queries | Performance | Baja | Para escala > 500K BPs, considerar vista materializada. |
| Tests de integración para BusinessPartnerV2 | Testing | Alta | Solo security tests existentes; faltan integration tests para escenarios UAT. |
| `PriceListId` en TradingSettings | Funcional | Baja | Campo eliminado; módulo de listas de precios pendiente. |

---

## 15. REFERENCIAS

| Recurso | Ubicación |
|---|---|
| Documentación de arquitectura | `AI-RULES/CORE-ARCHITECTURE.md` |
| Reglas de backend | `AI-RULES/BACKEND-RULES.md` |
| Security tests | `backend/src/ERP.API.Tests/SecurityTests/` |
| Migration V2 | `backend/src/ERP.Infrastructure/Persistence/Migrations/` |
| Domain events | `backend/src/ERP.Domain/MasterData/Events/` |
| Frontend types | `frontend/src/modules/masterData/types/businessPartner.types.ts` |
| Frontend facade | `frontend/src/modules/masterData/api/businessPartnerFacade.ts` |

---

*Documento generado como parte de la Fase 22 del Business Partner V2 Rewrite.*  
*Fecha: 2026-06-04 | Sistema: ERP SaaS — ZH Technologies*
