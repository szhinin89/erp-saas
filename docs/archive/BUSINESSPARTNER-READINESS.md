# BusinessPartner — Estado Final: FROZEN

**Estado:** ✅ FROZEN  
**Fecha de cierre:** 2026-06-02  
**Versión:** 2.0 — post-implementación completa  

> Este documento reemplaza la versión 1.0 (2026-05-22) que describía el estado de prerequisitos.
> El módulo está completamente implementado y cerrado. No se aceptan modificaciones
> estructurales sin una nueva ADR aprobada.

---

## Decisión arquitectónica oficial

**Business Partners son TENANT-SCOPED.**

```
Subscriber (tenant)
└── BusinessPartner (identidad fiscal única — subscriber-scoped)
    ├── CustomerProfile  (rol cliente — subscriber-scoped)
    └── SupplierProfile  (rol proveedor — subscriber-scoped)

Company A ──┐
Company B ──┤──► CompanyBusinessPartnerSettings (condiciones por empresa — company-scoped)
Company C ──┘
```

- `master_business_partners` **mantiene `subscriber_id`** como campo de scope.
- NO existe catálogo global de clientes.
- NO existe catálogo global de proveedores.
- Cada Subscriber es propietario exclusivo de sus Business Partners.
- No se compartirán Business Partners entre Subscribers.

---

## Checklist de implementación — 100% completado

### Backend

| Componente | Estado |
|------------|--------|
| `BusinessPartner` entidad + VOs (`TaxIdentification`) | ✅ |
| `CustomerProfile` entidad | ✅ |
| `SupplierProfile` entidad | ✅ |
| `CompanyBusinessPartnerSettings` entidad | ✅ |
| `LegalRepresentativeName` campo en BP | ✅ |
| EF Configuration completa (columnas + navegación + índices) | ✅ |
| Migración `AddBPUniqueIdentificationIndex` | ✅ |
| Unique index `uq_mbp_subscriber_identification` en DB | ✅ |
| `IBusinessPartnerRepository` + implementación | ✅ |
| `ICustomerProfileRepository` + implementación | ✅ |
| `ISupplierProfileRepository` + implementación | ✅ |
| CRUD Commands: Create, Update, Disable, Activate | ✅ |
| CRUD Queries: Search (paginado + filtros), GetById | ✅ |
| `BusinessPartnerDto` con todos los campos | ✅ |
| `CompanyBusinessPartnerSettings` handlers (CRUD + bloqueo) | ✅ |
| Customer Notes handler | ✅ |
| Supplier Profile update handler | ✅ |
| `BusinessPartnersController` completo | ✅ |
| Multi-tenant: query filters vía `ISubscriberScopedEntity` | ✅ |
| Integridad referencial: FKs hacia `sales_bill`, `purch_note`, retenciones | ✅ |

### Frontend

| Componente | Estado |
|------------|--------|
| `businessPartner.types.ts` — todos los tipos TS | ✅ |
| `businessPartnerService.ts` — `normalizeRow()` completo | ✅ |
| `MasterDataBpFormFields` — todos los campos incluyendo "Representante legal" (solo RUC) | ✅ |
| `MasterDataPartnerWizard` — 4 pasos: búsqueda, identidad, contacto, revisión | ✅ |
| `MasterDataCustomersPage` — listado, detalle, crear, editar, activar/desactivar | ✅ |
| `MasterDataSuppliersPage` — ídem | ✅ |
| `MasterDataBusinessPartnerDetailPage` — detalle unificado | ✅ |
| `MasterDataCompanySettingsModal` — configuración de crédito por empresa | ✅ |
| `MasterDataCustomerNotesModal` — notas por cliente | ✅ |

---

## Modelos de negocio soportados

### Persona Natural (CI)
- Requiere: `identificationType`, `identificationNumber`, `legalName`
- Opcional: `email`, `phone`, `countryCode`
- `tradeName` y `legalRepresentativeName` se ignoran (null)

### Persona Jurídica (RUC)
- Requiere: `identificationType`, `identificationNumber`, `legalName` (Razón Social)
- Opcional: `tradeName` (Nombre Comercial), `legalRepresentativeName`, `email`, `phone`

### Consumidor Final
- `identificationNumber` = `9999999999999`
- `legalName` = `CONSUMIDOR FINAL`
- `legalRepresentativeName` no aplica

---

## Backlog NO bloqueante

| ID | Descripción |
|----|-------------|
| BP-002 | Validación ecuatoriana de formato: RUC=13 dígitos, CI=10 dígitos, CONSUMER_FINAL forced values |
| BP-003 | Migración de validaciones de dominio hacia FluentValidation |

---

## Restricciones definitivas

Ver [`docs/adr/ADR-017-business-partner-scope.md`](../adr/ADR-017-business-partner-scope.md) sección "Restricciones definitivas".
