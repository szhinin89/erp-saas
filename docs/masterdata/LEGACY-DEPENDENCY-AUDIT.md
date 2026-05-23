# Legacy Dependency Audit — customers / suppliers → BusinessPartner

**Fecha:** 2026-05-23  
**Estado:** Activo — coexistencia strangler en curso  
**Restricciones absolutas:** NO DROP TABLE · NO eliminar CustomerId/SupplierId · NO romper historial

---

## Índice

1. [Resumen ejecutivo](#1-resumen-ejecutivo)  
2. [Leyenda de clasificación](#2-leyenda-de-clasificación)  
3. [Dominio — entidades legacy](#3-dominio--entidades-legacy)  
4. [FK activas — columnas con dependencia estructural](#4-fk-activas--columnas-con-dependencia-estructural)  
5. [EF Core — configuraciones y DbSets](#5-ef-core--configuraciones-y-dbsets)  
6. [Repositorios e interfaces](#6-repositorios-e-interfaces)  
7. [Handlers MediatR — CRUD legacy](#7-handlers-mediatR--crud-legacy)  
8. [Handlers MediatR — transacciones operacionales](#8-handlers-mediatR--transacciones-operacionales)  
9. [DTOs y contratos API](#9-dtos-y-contratos-api)  
10. [Controladores API](#10-controladores-api)  
11. [Migraciones EF](#11-migraciones-ef)  
12. [Mappers de infraestructura](#12-mappers-de-infraestructura)  
13. [Servicios de reconciliación (puente)](#13-servicios-de-reconciliación-puente)  
14. [Tests backend](#14-tests-backend)  
15. [Frontend — componentes y servicios](#15-frontend--componentes-y-servicios)  
16. [E2E — Playwright](#16-e2e--playwright)  
17. [Dependencias DEAD / candidatas a eliminar primero](#17-dependencias-dead--candidatas-a-eliminar-primero)  
18. [Scorecard consolidado](#18-scorecard-consolidado)  
19. [Dependency graph (texto)](#19-dependency-graph-texto)  

---

## 1. Resumen ejecutivo

El codebase tiene **dos sistemas de identidad de contraparte coexistiendo**:

| Sistema | Tablas | Estado |
|---------|--------|--------|
| **Legacy** | `sales.customers`, `purchases.supplier` | Operacional activo — TODAS las transacciones lo usan |
| **Canónico (target)** | `master_business_partner`, `master_customer_profiles`, `master_supplier_profiles`, `company_business_partner_settings` | Creado, enriquecido vía strangler, NO recibe writes transaccionales todavía |

La transición es un **strangler fig progresivo**: el aggregate `BusinessPartner` absorbe gradualmente identidad, después escrituras, después FK, y finalmente las tablas legacy se vuelven read-only.

**Conteo de dependencias activas:**

| Categoría | Customer | Supplier | Total |
|-----------|----------|----------|-------|
| FK activas en tablas transaccionales | 6 | 7 | **13** |
| DbSet / config EF | 1 | 1 | 2 |
| Repositorios (interface + impl) | 2 | 2 | 4 |
| Handlers CRUD | 12 | 12 | 24 |
| Handlers transaccionales | 4 | 13 | 17 |
| DTOs / contratos | 3 | 3 | 6 |
| Controladores | 1 | 1 | 2 |
| Tests | 3 | 3 | 6 |
| Frontend componentes/hooks | 9 | 1 | 10 |
| Frontend servicios/API | 3 | 3 | 6 |
| E2E Playwright | 5 | 3 | 8 |

---

## 2. Leyenda de clasificación

| Tipo | Descripción |
|------|-------------|
| **READ** | Lee datos de customers/suppliers. Migrable a BusinessPartner sin tocar schema. |
| **WRITE** | Crea o modifica registros en customers/suppliers. Requiere dual-write o redirección. |
| **FK** | Columna de clave foránea en tabla transaccional. Requiere migración de schema (nullable → shadow FK → swap). |
| **REPORT** | Agrega datos de customers/suppliers para reportes/dashboards. Migrable vía projection. |
| **DEAD** | Sin uso activo confirmado. Candidato a eliminar antes del resto. |

---

## 3. Dominio — entidades legacy

### 3.1 Customer

**Tipo: WRITE + READ**

| Archivo | Descripción |
|---------|-------------|
| `backend/src/ERP.Domain/Modules/Sales/Entities/Customer.cs` | Aggregate root legacy. Extiende `MasterEntity`. Implementa `ISubscriberScopedEntity`, `ICompanyOperationalEntity`. Campos: `IdentificationType`, `IdentificationNumber`, `LegalName`, `TradeName`, `AddressLine`, `Phone`, `Email`, `Notes`, `CountryCode`, `PaymentDays`, `CreditLimit`. Soft delete vía `SetActive()`/`SetInactive()`. |
| `backend/src/ERP.Domain/Modules/Sales/ValueObjects/CustomerIdentification.cs` | Value Object que encapsula tipo+número con normalización y validación. |
| `backend/src/ERP.Domain/Modules/Sales/ValueObjects/CustomerEmail.cs` | Value Object de email. |
| `backend/src/ERP.Domain/Modules/Sales/Interfaces/ICustomerRepository.cs` | Contrato: `AddAsync`, `GetByIdAsync(subscriberId, id)`, `ExistsIdentificationAsync`, `GetAsync(subscriberId, ...)`, `SaveChangesAsync`. |

### 3.2 Supplier

**Tipo: WRITE + READ**

| Archivo | Descripción |
|---------|-------------|
| `backend/src/ERP.Domain/Modules/Purchasing/Entities/Supplier.cs` (o `Proveedor.cs`) | Aggregate root legacy. Extiende `MasterEntity`. Implementa `ISubscriberScopedEntity`. Campos: `PersonType` (Natural/Legal), `LegalName`, `Ruc`, `Email`, `Phone`, `Address`, `PaymentTerms`, `CountryCode`, `TaxSupportCode`, `PaymentDays`, `CreditLimit`. Validación RUC SRI módulo 10/11. |
| `backend/src/ERP.Domain/Modules/Purchasing/Interfaces/IProveedorRepository.cs` | Contrato: `AddAsync`, `GetByIdAsync`, `GetByRucAsync`, `ExistsRucAsync`, `GetAsync(subscriberId, ...)`, `SaveChangesAsync`. |

---

## 4. FK activas — columnas con dependencia estructural

Estas son las dependencias más críticas: columnas `customer_id` / `supplier_id` en tablas transaccionales. Requieren **Shadow FK Migration** (Fase 3) antes de poder eliminar las tablas legacy.

### 4.1 Dependencias de `customers`

**Tipo: FK**

| Tabla transaccional | Entidad .NET | Columna | Nullable | Comportamiento OnDelete | Uso |
|---------------------|--------------|---------|----------|------------------------|-----|
| `sales_document` | `SalesDocument` | `customer_id` | Sí (`Guid?`) | `Restrict` | FK + navegación `Cliente`. Filtrado en listas. |
| `sales_bill` | `SalesBill` | `customer_id` | Sí (`Guid?`) | `Restrict` | Facturas SRI legacy (tabla sincronizada). |
| `sales_withholding` / `sales_retention` | `SalesWithholding`, `SalesRetention` | `customer_id` | Probable | — | Retenciones recibidas en ventas. |
| `electronic_docs` / `sales_invoice` (SRI) | `SalesInvoice` | `customer_id` | Sí (`Guid?`) | — | FK viva + snapshot inmutable de datos comprador. |
| `credit_note` / `debit_note` | `CreditNote`, `DebitNote` | `customer_id` | Sí | — | Notas de crédito/débito de ventas. |
| `delivery_guide` | `DeliveryGuide` | `customer_id` | Probable | — | Guías de remisión. |

### 4.2 Dependencias de `supplier`

**Tipo: FK**

| Tabla transaccional | Entidad .NET | Columna | Nullable | Uso |
|---------------------|--------------|---------|----------|-----|
| `purchase_document` | `PurchaseDocument` | `supplier_id` | Sí (`Guid?`) | Compras generales. FK sin navegación explícita en config. |
| `purch_bill` | `PurchBill` | `supplier_id` | Sí | Facturas de compra legacy (tabla sincronizada). |
| `purchase_order` | `PurchaseOrder` | `supplier_id` | No (`Guid`) — requerido | Órdenes de compra. |
| `expense_document` | `ExpenseDocument` | `supplier_id` | Sí (`Guid?`) | Gastos — proveedor opcional. |
| `expense_invoice` | `ExpenseInvoice` | `supplier_id` | Sí | Facturas de gasto. |
| `purchase_invoice` (Purchases BC) | `PurchaseInvoice` | `supplier_id` | No (`Guid`) — requerido | Facturas recibidas de proveedores. |
| `supplier_note` | `SupplierNote` | `supplier_id` | Sí | Notas de crédito de proveedores. |
| `product_supplier_codes` | `ProductSupplierCode` | `supplier_id` | Probable | Códigos internos de producto por proveedor. |
| `issued_retention` / `purchase_withholding` | `IssuedRetention`, `PurchaseWithholding` | `supplier_id` | Probable | Retenciones emitidas a proveedores. |
| `purch_note` | `PurchNote` | `supplier_id` | Sí | Notas de compra. |

---

## 5. EF Core — configuraciones y DbSets

### 5.1 DbContext

**Tipo: READ + WRITE**

| Archivo | Línea aprox. | DbSet |
|---------|-------------|-------|
| `backend/src/ERP.Infrastructure/Persistence/ErpDbContext.cs` | ~264 | `public DbSet<Customer> Customers => Set<Customer>();` |
| `backend/src/ERP.Infrastructure/Persistence/ErpDbContext.cs` | ~296 | `public DbSet<Supplier> Suppliers => Set<Supplier>();` |

DbSets del nuevo aggregate (ya existentes):

| DbSet | Descripción |
|-------|-------------|
| `DbSet<BusinessPartner> BusinessPartners` | Canónico — subscriber scoped |
| `DbSet<CustomerProfile> CustomerProfiles` | Rol cliente del BP |
| `DbSet<SupplierProfile> SupplierProfiles` | Rol proveedor del BP |
| `DbSet<CompanyBusinessPartnerSettings> CompanyBusinessPartnerSettings` | Settings operacionales por compañía |

### 5.2 Configuraciones EF (fluent)

**Tipo: FK**

| Archivo | Tabla mapeada | Detalles clave |
|---------|--------------|----------------|
| `backend/.../Configurations/Sales/CustomerConfiguration.cs` | `customers` | UNIQUE idx en `(subscriber_id, company_id, identification_type, identification_number)` |
| `backend/.../Configurations/Purchasing/SupplierConfiguration.cs` | `supplier` | UNIQUE idx en `(subscriber_id, ruc)` |
| `backend/.../Configurations/Sales/SalesDocumentConfiguration.cs` | `sales_document` | FK `customer_id → customers(id)` OnDelete Restrict. Índice en `(subscriber_id, customer_id, issue_date)` |
| `backend/.../Configurations/Sales/SalesBillConfiguration.cs` | `sales_bill` | FK `customer_id` |
| `backend/.../Configurations/Sales/SalesWithholdingConfiguration.cs` | `sales_withholding` | FK `customer_id` |
| `backend/.../Configurations/Sales/SalesRetentionConfiguration.cs` | `sales_retention` | FK `customer_id` |
| `backend/.../Configurations/Purchasing/PurchaseDocumentConfiguration.cs` | `purchase_document` | FK `supplier_id` (sin FK explícita configurada — posible gap) |
| `backend/.../Configurations/Purchasing/PurchaseOrderConfiguration.cs` | `purchase_order` | FK `supplier_id` requerida |
| `backend/.../Configurations/Purchasing/PurchBillConfiguration.cs` | `purch_bill` | FK `supplier_id` |
| `backend/.../Configurations/Purchasing/PurchNoteConfiguration.cs` | `purch_note` | FK `supplier_id` |
| `backend/.../Configurations/Purchasing/IssuedRetentionConfiguration.cs` | `issued_retention` | FK `supplier_id` |
| `backend/.../Configurations/Purchasing/PurchaseWithholdingConfiguration.cs` | `purchase_withholding` | FK `supplier_id` |
| `backend/.../Configurations/Expenses/ExpenseDocumentConfiguration.cs` | `expense_document` | FK `supplier_id` nullable |
| `backend/.../Configurations/Expenses/ExpenseInvoiceConfiguration.cs` | `expense_invoice` | FK `supplier_id` |
| `backend/.../Configurations/Purchases/PurchaseInvoiceConfiguration.cs` | `purchase_invoice` | FK `supplier_id` requerida |
| `backend/.../Configurations/Purchases/SupplierNoteConfiguration.cs` | `supplier_note` | FK `supplier_id` |
| `backend/.../Configurations/ProductConfiguration.cs` | `product_supplier_codes` | FK `supplier_id` |
| `backend/.../Configurations/ElectronicDocuments/SalesInvoiceConfiguration.cs` | `sales_invoice` (SRI) | FK `customer_id` nullable |
| `backend/.../Configurations/ElectronicDocuments/WithholdingCertConfiguration.cs` | `withholding_cert` | FK probable `supplier_id` / `customer_id` |
| `backend/.../Configurations/ElectronicDocuments/CreditNoteConfiguration.cs` | `credit_note` | FK `customer_id` |
| `backend/.../Configurations/ElectronicDocuments/DebitNoteConfiguration.cs` | `debit_note` | FK `customer_id` |
| `backend/.../Configurations/ElectronicDocuments/DeliveryGuideConfiguration.cs` | `delivery_guide` | FK `customer_id` |
| `backend/.../Configurations/Auxiliary/VatRefundConfiguration.cs` | `vat_refund` | FK probable `customer_id` |
| `backend/.../Configurations/Purchases/ReceivedWithholdingConfiguration.cs` | `received_withholding` | FK probable `supplier_id` |

---

## 6. Repositorios e interfaces

### 6.1 Customer

**Tipo: READ + WRITE**

| Archivo | Tipo | Descripción |
|---------|------|-------------|
| `backend/src/ERP.Domain/Modules/Sales/Interfaces/ICustomerRepository.cs` | Interface | Contrato de acceso a `customers` |
| `backend/src/ERP.Infrastructure/Persistence/Repositories/CustomerRepository.cs` | Implementation | Usa `.ForOperationalScope(subscriberId, _company)`. Search: LegalName, TradeName, IdentificationNumber, Email, Phone. |

### 6.2 Supplier

**Tipo: READ + WRITE**

| Archivo | Tipo | Descripción |
|---------|------|-------------|
| `backend/src/ERP.Domain/Modules/Purchasing/Interfaces/IProveedorRepository.cs` | Interface | Contrato acceso a `supplier` |
| `backend/src/ERP.Infrastructure/Persistence/Repositories/ProveedorRepository.cs` | Implementation | Filtro directo por subscriber (SIN extensión ForOperationalScope). `GetByRucAsync`, `ExistsRucAsync`. Search: LegalName, Ruc, Email, Phone. Filtro por PersonType. |

> **Gap de seguridad**: `ProveedorRepository` no usa `ForOperationalScope` — verificar si aplica company-scope o si Supplier es subscriber-only.

### 6.3 Repositorios del nuevo aggregate (ya existentes)

| Archivo | Descripción |
|---------|-------------|
| `backend/src/ERP.Infrastructure/MasterData/Repositories/BusinessPartnerRepository.cs` | CRUD + search BP |
| `backend/src/ERP.Infrastructure/MasterData/Repositories/CustomerProfileRepository.cs` | CustomerProfile |
| `backend/src/ERP.Infrastructure/MasterData/Repositories/SupplierProfileRepository.cs` | SupplierProfile |
| `backend/src/ERP.Infrastructure/MasterData/Repositories/CompanyBpSettingsRepository.cs` | Settings por compañía |

---

## 7. Handlers MediatR — CRUD legacy

### 7.1 Customer CRUD (12 handlers)

**Tipo: WRITE (create/update/enable/disable) + READ (list/get)**

| Directorio | Comando/Query | Operación |
|-----------|---------------|-----------|
| `UseCases/CrearCliente/` | `CreateCustomerCommand` + Handler + Validator | Crea Customer. Feature gate: `SubscriptionFeatureCodes.Sales`. Scope: `ICompanyScopedRequest`. |
| `UseCases/ActualizarCliente/` | `UpdateCustomerCommand` + Handler + Validator | Actualiza Customer. |
| `UseCases/ListarClientes/` | `GetCustomersQuery` + Handler | Lista con filtros `activeFilter`, `search`. |
| `UseCases/ObtenerCliente/` | `GetCustomerByIdQuery` + Handler | Single fetch → `CustomerDetailDto`. |
| `UseCases/HabilitarCliente/` | `EnableCustomerCommand` + Handler + Validator | Soft-enable. |
| `UseCases/DeshabilitarCliente/` | `DisableCustomerCommand` + Handler + Validator | Soft-disable. |

Todos en: `backend/src/ERP.Application/Modules/Sales/UseCases/`

### 7.2 Supplier CRUD (12 handlers)

**Tipo: WRITE (create/update/enable/disable) + READ (list/get)**

| Directorio | Comando/Query | Operación |
|-----------|---------------|-----------|
| `UseCases/CrearProveedor/` | `CreateProveedorCommand` + Handler + Validator | Crea Supplier. Feature gate: `SubscriptionFeatureCodes.Inventory`. Validación RUC SRI módulo 10/11. |
| `UseCases/ActualizarProveedor/` | `UpdateProveedorCommand` + Handler + Validator | Actualiza Supplier. |
| `UseCases/ListarProveedores/` | `GetProveedoresQuery` + Handler | Lista con `activeFilter`, `search`, `tipoPersona`. |
| `UseCases/ObtenerProveedor/` | `GetProveedorByIdQuery` + Handler | Single fetch → `SupplierDetailDto`. |
| `UseCases/HabilitarProveedor/` | `EnableProveedorCommand` + Handler + Validator | Soft-enable. |
| `UseCases/DeshabilitarProveedor/` | `DisableProveedorCommand` + Handler + Validator | Soft-disable. |

Todos en: `backend/src/ERP.Application/Modules/Purchasing/UseCases/`

---

## 8. Handlers MediatR — transacciones operacionales

Estos handlers usan `CustomerId` / `SupplierId` para crear o leer documentos transaccionales. Son la dependencia **WRITE más crítica** porque bloquean la Fase 4.

### 8.1 Customer (4 handlers transaccionales)

**Tipo: WRITE**

| Archivo | Handler | Uso de CustomerId |
|---------|---------|------------------|
| `Modules/Sales/UseCases/CrearVenta/CrearVentaCommand.cs` | `CreateSaleCommand` | Recibe `CustomerId` como parámetro |
| `Modules/Sales/UseCases/CrearVenta/CrearVentaCommandHandler.cs` | `CreateSaleCommandHandler` | Valida que customer existe; crea `SalesDocument.CustomerId` |
| `Modules/Sales/UseCases/EmitirFacturaElectronica/EmitirFacturaElectronicaCommandHandler.cs` | `EmitirFacturaElectronicaCommandHandler` | Carga Customer via `Cliente` navigation para generar XML SRI |
| `Modules/Sales/UseCases/ReceivedRetentions/RegistrarVentasRetencionRecibidaCommandHandler.cs` | `RegistrarVentasRetencionRecibidaCommandHandler` | Vincula retención recibida al cliente |

**Tipo: READ**

| Archivo | Handler | Uso de CustomerId |
|---------|---------|------------------|
| `Modules/Sales/UseCases/GetVentasList/GetVentasListQueryHandler.cs` | `GetVentasListQueryHandler` | Filtra ventas por `CustomerId` |
| `Modules/Sales/UseCases/GetVentaById/GetVentaByIdQueryHandler.cs` | `GetVentaByIdQueryHandler` | Proyecta datos de cliente en respuesta |
| `Modules/Sales/UseCases/ReceivedRetentions/GetVentasRetencionesRecibidasListQuery.cs` | `GetVentasRetencionesRecibidasListQuery` | Filtra por `CustomerId` |

### 8.2 Supplier (13 handlers transaccionales)

**Tipo: WRITE**

| Archivo | Handler | Uso de SupplierId |
|---------|---------|------------------|
| `Modules/Purchasing/UseCases/CreatePurchase/CrearCompraCommand.cs` | `CreatePurchaseCommand` | Recibe `SupplierId`; crea `PurchaseDocument` |
| `Modules/Purchasing/UseCases/CreatePurchase/CrearCompraCommandHandler.cs` | `CrearCompraCommandHandler` | Valida supplier, procesa XML/manual, asigna `SupplierId` |
| `Modules/Purchasing/UseCases/CreatePurchaseOrder/CrearOrdenCompraCommand.cs` | `CreatePurchaseOrderCommand` | Recibe `SupplierId` |
| `Modules/Purchasing/UseCases/CreatePurchaseOrder/CrearOrdenCompraCommandHandler.cs` | `CrearOrdenCompraCommandHandler` | Valida supplier, crea `PurchaseOrder.SupplierId` |
| `Modules/Expenses/UseCases/CreateExpense/CrearGastoCommand.cs` | `CreateExpenseCommand` | `SupplierId` opcional |
| `Modules/Expenses/UseCases/CreateExpense/CrearGastoCommandHandler.cs` | `CrearGastoCommandHandler` | Asigna `SupplierId` en `ExpenseDocument` |
| `Modules/Purchasing/UseCases/SupplierNotes/ImportarCompraNotaProveedorCommandHandler.cs` | `ImportarCompraNotaProveedorCommandHandler` | Crea `SupplierNote` con `SupplierId` |
| `Modules/Purchasing/UseCases/Retentions/GenerarCompraRetencionEmitidaCommandHandler.cs` | `GenerarCompraRetencionEmitidaCommandHandler` | Genera retención emitida al proveedor |
| `Modules/Purchasing/UseCases/ValidatePurchase/ValidarCompraCommandHandler.cs` | `ValidarCompraCommandHandler` | Valida compra contra datos del supplier |
| `Modules/Purchasing/UseCases/ApprovePurchaseOrder/AprobarOrdenCompraCommandHandler.cs` | `AprobarOrdenCompraCommandHandler` | Aprueba PO — transición de estado |
| `Modules/Purchasing/UseCases/CancelPurchaseOrder/CancelarOrdenCompraCommandHandler.cs` | `CancelarOrdenCompraCommandHandler` | Cancela PO |
| `Modules/Purchasing/UseCases/LinkInvoiceToPurchaseOrder/VincularFacturaAOrdenCompraCommandHandler.cs` | `VincularFacturaAOrdenCompraCommandHandler` | Vincula factura proveedor a PO |
| `Modules/Purchasing/UseCases/SendPurchaseOrder/EnviarOrdenCompraCommandHandler.cs` | `EnviarOrdenCompraCommandHandler` | Envía PO al proveedor |

**Tipo: READ**

| Archivo | Handler | Uso de SupplierId |
|---------|---------|------------------|
| `Modules/Purchasing/UseCases/GetCompras/GetComprasQueryHandler.cs` | `GetComprasQueryHandler` | Filtra por `SupplierId` |
| `Modules/Purchasing/UseCases/GetCompraById/GetCompraByIdQueryHandler.cs` | `GetCompraByIdQueryHandler` | Proyecta datos proveedor |
| `Modules/Purchasing/UseCases/GetPurchaseOrdersList/GetOrdenesCompraListQueryHandler.cs` | `GetOrdenesCompraListQueryHandler` | Filtra POs por `SupplierId` |
| `Modules/Purchasing/UseCases/GetOrdenCompraById/GetOrdenCompraByIdQueryHandler.cs` | `GetOrdenCompraByIdQueryHandler` | Proyecta datos proveedor |
| `Modules/Purchasing/UseCases/GetOrdersPendingBilling/GetOrdenesPendientesPorFacturarQueryHandler.cs` | `GetOrdenesPendientesPorFacturarQueryHandler` | Filtra POs pendientes por `SupplierId` |
| `Modules/Purchasing/UseCases/SupplierNotes/GetComprasNotasProveedorQueryHandler.cs` | `GetComprasNotasProveedorQueryHandler` | Lista notas por `SupplierId` |
| `Modules/Purchasing/UseCases/SupplierNotes/AprobarCompraNotaProveedorCommandHandler.cs` | `AprobarCompraNotaProveedorCommandHandler` | Aprueba nota — usa `SupplierId` para validación |
| `Modules/Expenses/UseCases/GetExpenses/GetGastosQueryHandler.cs` | `GetGastosQueryHandler` | Filtra gastos por `SupplierId` |
| `Modules/Expenses/UseCases/GetExpenseById/GetGastoByIdQueryHandler.cs` | `GetGastoByIdQueryHandler` | Proyecta datos proveedor en gasto |

---

## 9. DTOs y contratos API

### 9.1 Customer DTOs

**Tipo: READ**

| Archivo | DTOs |
|---------|------|
| `backend/src/ERP.Application/Modules/Sales/DTOs/CustomerDtos.cs` | `CustomerDto` (Id, IdentificationType, IdentificationNumber, LegalName, TradeName, AddressLine, Phone, Email, Notes, IsActive) · `CustomerDetailDto` (+ auditoría) |
| `backend/src/ERP.Application/Modules/Sales/DTOs/VentasDtos.cs` | Proyecciones de ventas con campos de cliente |
| `backend/src/ERP.Application/Modules/Sales/DTOs/VentasRetencionRecibidaListItemDto.cs` | DTO retenciones con `CustomerId` |

### 9.2 Supplier DTOs

**Tipo: READ**

| Archivo | DTOs |
|---------|------|
| `backend/src/ERP.Application/Modules/Purchasing/DTOs/` | `SupplierDto`, `SupplierDetailDto` |
| `backend/src/ERP.Application/Modules/Purchasing/DTOs/CompraDtos.cs` | Proyecciones compras con `SupplierId` |
| `backend/src/ERP.Application/Modules/Purchasing/DTOs/OrdenCompraDtos.cs` | DTOs PO con `SupplierId` |
| `backend/src/ERP.Application/Modules/Purchasing/DTOs/CompraRetencionEmitidaListItemDto.cs` | DTO retenciones emitidas |
| `backend/src/ERP.Application/Modules/Expenses/DTOs/GastoDtos.cs` | DTOs gastos con `SupplierId` |

### 9.3 MasterData DTO (canónico — ya existe)

| Archivo | Descripción |
|---------|-------------|
| `backend/src/ERP.Application/MasterData/DTOs/BusinessPartnerDto.cs` | DTO canónico BP. Incluye `legacyCustomerId` / `legacySupplierId` durante coexistencia. |

---

## 10. Controladores API

### 10.1 Customers

**Tipo: WRITE + READ**

| Endpoint | Método | Permiso | Operación |
|----------|--------|---------|-----------|
| `GET /api/sales/customers` | `GetAll` | `perm:sales.customers.view` | Lista con filtros |
| `GET /api/sales/customers/{id}` | `GetById` | `perm:sales.customers.view` | Detalle |
| `POST /api/sales/customers` | `Create` | `perm:sales.customers.create` | Crear |
| `PUT /api/sales/customers/{id}` | `Update` | `perm:sales.customers.update` | Actualizar |
| `PATCH /api/sales/customers/{id}/disable` | `Disable` | `perm:sales.customers.delete` | Soft-disable |
| `PATCH /api/sales/customers/{id}/enable` | `Enable` | `perm:sales.customers.update` | Soft-enable |

Archivo: `backend/src/ERP.API/Controllers/CustomersController.cs`

### 10.2 Suppliers

**Tipo: WRITE + READ**

| Endpoint | Método | Permiso | Operación |
|----------|--------|---------|-----------|
| `GET /api/purchases/suppliers` | `GetAll` | `perm:purchases.suppliers.view` | Lista con filtros + tipoPersona |
| `GET /api/purchases/suppliers/{id}` | `GetById` | `perm:purchases.suppliers.view` | Detalle |
| `POST /api/purchases/suppliers` | `Create` | `perm:purchases.suppliers.create` | Crear |
| `PUT /api/purchases/suppliers/{id}` | `Update` | `perm:purchases.suppliers.update` | Actualizar |
| `PATCH /api/purchases/suppliers/{id}/disable` | `Disable` | `perm:purchases.suppliers.delete` | Soft-disable |
| `PATCH /api/purchases/suppliers/{id}/enable` | `Enable` | `perm:purchases.suppliers.update` | Soft-enable |

Archivo: `backend/src/ERP.API/Controllers/SuppliersController.cs`

### 10.3 BusinessPartners (canónico — ya existe)

Archivo: `backend/src/ERP.API/Controllers/BusinessPartnersController.cs`  
Operaciones: Search, GetById, Create, Disable.

---

## 11. Migraciones EF

| Archivo | Nombre | Impacto |
|---------|--------|---------|
| `backend/src/ERP.Infrastructure/Migrations/20260521034018_InitialEnterpriseBaseline.cs` | `InitialEnterpriseBaseline` | Crea `customers`, `supplier`, y todas sus FKs e índices. Define las **13 FK activas**. |
| `backend/src/ERP.Infrastructure/Migrations/20260523034515_AddMasterDataBC.cs` | `AddMasterDataBC` | Crea `master_business_partner`, `master_customer_profiles`, `master_supplier_profiles`, `company_business_partner_settings`. |
| `backend/src/ERP.Infrastructure/Migrations/20260523052815_AddSupplierProfileSriDefaults.cs` | `AddSupplierProfileSriDefaults` | Defaults SRI en `SupplierProfile`. |
| `backend/src/ERP.Infrastructure/Migrations/ErpDbContextModelSnapshot.cs` | Snapshot actual | Refleja estado completo — Customer + Supplier + BusinessPartner coexistiendo. |

---

## 12. Mappers de infraestructura

**Tipo: READ**

| Archivo | Función | Dependencia |
|---------|---------|-------------|
| `backend/src/ERP.Infrastructure/Persistence/Mapping/SalesDocumentMapper.cs` | Entity ↔ DTO/DB | Proyecta `CustomerId` |
| `backend/src/ERP.Infrastructure/Persistence/Mapping/SalesWithholdingMapper.cs` | Entity ↔ DB | `CustomerId` en retenciones |
| `backend/src/ERP.Infrastructure/Persistence/Mapping/PurchaseDocumentMapper.cs` | Entity ↔ DTO/DB | Proyecta `SupplierId` |
| `backend/src/ERP.Infrastructure/Persistence/Mapping/PurchaseWithholdingMapper.cs` | Entity ↔ DB | `SupplierId` en retenciones |
| `backend/src/ERP.Infrastructure/Persistence/Mapping/ExpenseDocumentMapper.cs` | Entity ↔ DB | `SupplierId` nullable |

---

## 13. Servicios de reconciliación (puente)

Estos son el **puente actual** entre legacy y canónico. Son READ + sync, no escritura transaccional.

| Archivo | Tipo | Descripción |
|---------|------|-------------|
| `backend/src/ERP.Infrastructure/MasterData/BusinessPartnerOperationalLinkEnricher.cs` | READ + enrichment | Enriquece links operacionales BP ↔ Customer/Supplier legacy. Permite que el frontend reciba `legacyCustomerId`/`legacySupplierId`. |
| `backend/src/ERP.Infrastructure/MasterData/Reconciliation/BusinessPartnerReconciliationService.cs` | READ + sync | Reconcilia Customer/Supplier legacy → BusinessPartner + Profiles. |
| `backend/src/ERP.Infrastructure/Persistence/UnifiedDocumentSync.cs` | WRITE (sync) | Sincroniza `sales_bill`/`purch_bill` ↔ `sales_document`/`purchase_document`. Toca ambos mundos — cuidar en Fase 4. |

---

## 14. Tests backend

### 14.1 Tests de dominio Customer

**Tipo: READ (test coverage)**

| Archivo | Tests | Scope |
|---------|-------|-------|
| `backend/src/ERP.Domain.Tests/Customers/CustomerTests.cs` | Lógica de dominio Customer | Unitario |

### 14.2 Tests de aplicación Supplier

| Archivo | Tests | Scope |
|---------|-------|-------|
| `backend/src/ERP.Application.Tests/Proveedores/CreateProveedorCommandValidatorTests.cs` | Validación RUC SRI (módulo 10/11) | Unitario |
| `backend/src/ERP.Application.Tests/Purchasing/CrearOrdenCompraCommandValidatorTests.cs` | Validación PO con `SupplierId` | Unitario |
| `backend/src/ERP.Application.Tests/Purchasing/VincularFacturaAOrdenCompraCommandHandlerTests.cs` | Link factura → PO supplier | Integración |

### 14.3 Tests E2E con Customer/Supplier como pre-condición

| Archivo | Flujo cubierto |
|---------|---------------|
| `backend/src/ERP.API.Tests/Integration/VentasEndToEndTests.cs` | Venta completa — requiere Customer |
| `backend/src/ERP.API.Tests/Integration/VentasHttpTests.cs` | HTTP ventas con Customer |
| `backend/src/ERP.API.Tests/Integration/CompraGastoEndToEndTests.cs` | Compra + Gasto — requiere Supplier |
| `backend/src/ERP.API.Tests/Integration/OrdenesCompraEndToEndTests.cs` | PO flow — requiere Supplier |
| `backend/src/ERP.API.Tests/Integration/OrdenCompraFlujoCompletoTests.cs` | PO completo |
| `backend/src/ERP.API.Tests/Integration/NotasYRetencionesEndToEndTests.cs` | Notas y retenciones — Customer + Supplier |
| `backend/src/ERP.API.Tests/Integration/KardexFlujoCompletoTests.cs` | Kardex — requiere Supplier (PO) |
| `backend/src/ERP.API.Tests/Integration/KardexInventarioTests.cs` | Inventario — requiere Supplier |
| `backend/src/ERP.API.Tests/Support/IntegrationSeedData.cs` | Seed data — crea Customer + Supplier para todos los tests |
| `backend/src/ERP.Infrastructure.Tests/Persistence/UnifiedIntegrationTestSeed.cs` | Seed unificado |
| `backend/src/ERP.Infrastructure.Tests/Persistence/UnifiedDocumentSyncIntegrationTests.cs` | Sync legacy ↔ unified |

---

## 15. Frontend — componentes y servicios

### 15.1 Customer — módulo legacy

**Tipo: READ + WRITE**

| Archivo | Tipo | Descripción |
|---------|------|-------------|
| `frontend/src/modules/customers/pages/useCustomersPage.ts` | Hook | Lógica de página (customerId en categorías/contactos) |
| `frontend/src/modules/customers/pages/CustomersPageCategoriesPanel.tsx` | Component | Usa `customerId` para asignar categorías |
| `frontend/src/modules/customers/pages/CustomersPageContactModal.tsx` | Component | Usa `customerId` para gestionar contactos |
| `frontend/src/modules/customers/pages/customersPageUtils.ts` | Util | Helpers con `customerId` |
| `frontend/src/modules/ventas/pages/CreateInvoicePage.tsx` | Component | Selecciona `customerId` para crear venta |
| `frontend/src/modules/ventas/schemas/createInvoiceSchema.ts` | Schema | Valida `customerId` en factura |
| `frontend/src/modules/ventas/api/ventasFacturasService.ts` | Service | Envía `customerId` en requests |
| `frontend/src/modules/ventas/api/withholdingReceivedService.ts` | Service | Filtra retenciones por `customerId` |

### 15.2 Supplier — módulo legacy

**Tipo: READ + WRITE**

| Archivo | Tipo | Descripción |
|---------|------|-------------|
| `frontend/src/modules/compras/facturas/pages/CrearCompraPage.tsx` | Component | Selecciona `supplierId` para crear compra |
| `frontend/src/modules/compras/facturas/pages/ComprasListPage.tsx` | Component | Filtra compras por `supplierId` |
| `frontend/src/modules/gastos/pages/CrearGastoPage.tsx` | Component | Selecciona `supplierId` para gasto |
| `frontend/src/modules/gastos/pages/GastosListPage.tsx` | Component | Filtra gastos por `supplierId` |
| `frontend/src/modules/compras/facturas/api/comprasService.ts` | Service | Envía `supplierId` en requests |
| `frontend/src/modules/compras/credit-notes/api/purchaseCreditNotesService.ts` | Service | `supplierId` en notas de crédito |
| `frontend/src/modules/compras/withholding-issued/api/withholdingIssuedService.ts` | Service | `supplierId` en retenciones emitidas |
| `frontend/src/modules/gastos/api/gastosService.ts` | Service | `supplierId` en gastos |

### 15.3 MasterData — adaptadores de coexistencia (ya implementados)

| Archivo | Tipo | Descripción |
|---------|------|-------------|
| `frontend/src/modules/masterData/adapters/businessPartnerCustomerAdapter.ts` | Adapter | Mapea `BusinessPartner` ↔ interfaz `Customer` legacy |
| `frontend/src/modules/masterData/adapters/businessPartnerSupplierAdapter.ts` | Adapter | Mapea `BusinessPartner` ↔ interfaz `Supplier` legacy |
| `frontend/src/modules/masterData/api/businessPartnerService.ts` | Service | Llama endpoints `/api/business-partners` |
| `frontend/src/modules/masterData/api/operationalLinkResolver.ts` | Resolver | Resuelve `CustomerProfile`/`SupplierProfile` → `BusinessPartner.Id` |
| `frontend/src/modules/masterData/types/businessPartner.types.ts` | Types | Tipos TS canónicos |
| `frontend/src/modules/masterData/types/operationalLink.types.ts` | Types | Links operacionales |

---

## 16. E2E — Playwright

| Archivo | Tipo | Dependencia |
|---------|------|-------------|
| `frontend/e2e/enterprise-masterdata-coexistence.spec.ts` | READ + WRITE | Tests de coexistencia BP ↔ Customer/Supplier |
| `frontend/e2e/enterprise-masterdata-pickers.spec.ts` | READ | Pickers con `customerId`/`supplierId` |
| `frontend/e2e/enterprise-sales-company.spec.ts` | WRITE | Selección de cliente en flujo de venta |
| `frontend/e2e/enterprise-company-ui.spec.ts` | READ | UI de compañía con datos customer/supplier |
| `frontend/e2e/enterprise-refresh-company.spec.ts` | READ | Refresh de contexto — verifica IDs |
| `frontend/e2e/helpers/api.ts` | WRITE | Helper que crea customers/suppliers de prueba |
| `frontend/e2e/helpers/sales.ts` | WRITE | Helper de flujos de venta con Customer |

---

## 17. Dependencias DEAD / candidatas a eliminar primero

| Archivo | Motivo sospecha de DEAD | Verificación requerida |
|---------|------------------------|----------------------|
| `backend/.../Configurations/Purchasing/PurchaseDocumentConfiguration.cs` — FK supplier_id | FK configurada pero sin navegación explícita en entidad | Confirmar en migration snapshot si FK está definida en DB |
| `backend/.../Configurations/Auxiliary/VatRefundConfiguration.cs` — customer_id | VatRefund puede ser standalone sin FK real a customers | Grep VatRefund entity para confirmar |
| `backend/.../Configurations/ElectronicDocuments/WithholdingCertConfiguration.cs` — supplier/customer | WithholdingCert puede referenciar snapshot en lugar de FK viva | Verificar schema y entity |
| Doble módulo frontend customers (`/modules/customers/` + `/pages/CustomersPage.tsx` legacy) | Puede haber páginas duplicadas sin uso activo | Audit imports activos |

---

## 18. Scorecard consolidado

| Dimensión | Count | Criticidad | Bloqueante para DROP |
|-----------|-------|-----------|---------------------|
| FK activas en DB (customer_id) | ~6 tablas | CRÍTICA | Sí |
| FK activas en DB (supplier_id) | ~10 tablas | CRÍTICA | Sí |
| Handlers WRITE legacy activos | 8 CRUD + 10 transaccional | ALTA | Sí (Fase 4) |
| Handlers READ legacy activos | 4 customer + 9 supplier | MEDIA | No (Fase 5 read compat) |
| Endpoints activos consumidos | 12 | ALTA | Sí |
| Repositorios legacy activos | 4 (2 iface + 2 impl) | ALTA | Sí |
| Tests con seed customer/supplier | 12+ | MEDIA | Necesitan mock/BP |
| Frontend writes legacy | 8 | ALTA | Sí (Fase 4) |
| Frontend reads legacy | 8 | MEDIA | No (Fase 5) |
| E2E activos | 7 | MEDIA | Actualizar en Fase 5 |
| **Reconciliación limpia** | 0 registros huérfanos (a medir) | CRÍTICA | Sí |
| **Drop-readiness score actual** | **~5 / 100** | — | — |

---

## 19. Dependency graph (texto)

```
BusinessPartner (canónico)
├── CustomerProfile  ─────────────────────────── [via enricher] ──> Customer (legacy)
│   └── CompanyBpSettings                                               ↓
│                                                               SalesDocument.customer_id
│                                                               SalesBill.customer_id
│                                                               SalesInvoice.customer_id
│                                                               CreditNote.customer_id
│                                                               DebitNote.customer_id
│                                                               DeliveryGuide.customer_id
│
└── SupplierProfile  ─────────────────────────── [via enricher] ──> Supplier (legacy)
    └── CompanyBpSettings                                              ↓
                                                              PurchaseDocument.supplier_id
                                                              PurchasOrder.supplier_id
                                                              PurchBill.supplier_id
                                                              PurchNote.supplier_id
                                                              ExpenseDocument.supplier_id
                                                              ExpenseInvoice.supplier_id
                                                              PurchaseInvoice.supplier_id
                                                              SupplierNote.supplier_id
                                                              IssuedRetention.supplier_id
                                                              PurchaseWithholding.supplier_id
                                                              ProductSupplierCode.supplier_id

Flujo actual (coexistencia):
  API legacy (POST /customers)
    → CreateCustomerCommandHandler → Customer (write)
    → [reconciliation service] → BusinessPartner + CustomerProfile (sync)

Target (Fase 4):
  API canónica (POST /business-partners)
    → CreateBusinessPartnerCommandHandler → BusinessPartner + Profile (write)
    → [compatibility layer] → Customer.id = legacy_customer_id (read-only derivado)
    → [shadow FK] → SalesDocument.business_partner_id = BP.id (nueva columna)
                    SalesDocument.customer_id = BP.CustomerProfile.LegacyId (compat)
```

---

**Próximo documento:** [BP-MIGRATION-ROADMAP.md](BP-MIGRATION-ROADMAP.md) — plan de implementación Fases 2–8.  
**Relacionado:** [FRONTEND-MIGRATION-STATUS.md](FRONTEND-MIGRATION-STATUS.md) · [LEGACY-DROP-READINESS.md](LEGACY-DROP-READINESS.md)
