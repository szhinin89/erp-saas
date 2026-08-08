# ARCHITECTURE GOVERNANCE — ERP SaaS ZH Technologies

> ℹ️ **Nota de auditoría (2026-06-08, resuelta 2026-07-23):** la sección "SUBSCRIBER (Control Plane SaaS)" que existía aquí describía entidades (`SubscriberBillingProfile`, `SubscriberBillingAccount`, `SubscriberSubscription`, tablas `subscriber_billing_*`) que [`docs/STATUS.md`](../docs/STATUS.md) registra como **eliminadas** en "FASE 1 — ERP Kernel Cleanup" (2026-06-05) y excluidas permanentemente del ERP Core por [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md). Esa sección fue removida de este documento normativo; el modelo se conserva como registro histórico en [`docs/archive/SUBSCRIBER-SCOPE-SEALED.md`](../docs/archive/SUBSCRIBER-SCOPE-SEALED.md).

> **Estado: ACTIVO — PRE-PRODUCTION SAFE MODE.**
> Este documento define las reglas de gobernanza arquitectónica del sistema ERP.
> Aplica a todos los agentes IA, desarrolladores y revisores de PR.

---

## CANONICAL MODEL MAP

El sistema tiene **3 scopes** con responsibilities exclusivas. Ningún scope puede
contener lógica o datos del otro.

### GLOBAL IMMUTABLE (schema `global`)

**Propósito:** Catálogos regulatorios y estándares externos. Sin tenant scope.

| Tabla | Concepto canónico |
|---|---|
| `global.sri_vat_rate` | Tarifas IVA Ecuador (SRI) |
| `global.sri_ice_rate` | Tarifas ICE Ecuador (SRI) |
| `global.sri_retention_code` | Códigos de retención SRI |
| `global.sri_uom` | Unidades de medida SRI |
| `global.sri_doc_type` | Tipos de comprobante SRI |
| `global.sri_id_type` | Tipos de identificación SRI |
| `global.sri_payment_method` | Formas de pago SRI |
| `global.sri_tax_support` | Sustentos tributarios SRI |
| `global.sri_tax_regime` | Regímenes tributarios SRI |
| `global.sri_retention_code` | Códigos de retención SRI |
| `global.sri_vat_rate` | Tarifas IVA SRI |
| `global.sri_ice_rate` | Tarifas ICE SRI |
| `global.sri_error_code` | Códigos de error SRI |
| `global.sri_environment` | Ambientes SRI |
| `global.sri_emission_type` | Tipos de emisión SRI |
| `global.sri_country` | Países (ISO) |
| `global.geo_provinces` | Provincias Ecuador (INEC) |
| `global.geo_cantons` | Cantones Ecuador (INEC) |
| `global.geo_parishes` | Parroquias Ecuador (INEC) |

**Regla GLOBAL:** Sin `tenant_id`. Sin `company_id`. Sin lógica de negocio.
Referencia histórica (Control Plane SaaS, no vigente): [docs/archive/SUBSCRIBER-SCOPE-SEALED.md](../docs/archive/SUBSCRIBER-SCOPE-SEALED.md)

---

### COMPANY (ERP Operativo)

**Propósito:** Todos los datos operativos del ERP. Con `company_id` obligatorio
(o en transición via `ICompanyOperationalEntity`).

| Módulo | Entidades canónicas |
|---|---|
| **Ventas** | `SalesBill`, `SalesNote`, `Invoice`, `SalesWithholding` |
| **Compras** | `PurchBill`, `PurchNote`, `PurchaseDocument`, `IssuedRetention` |
| **Inventario** | `Warehouse`, `StockMovement`, `CurrentStock`, `StockAdjustment` |
| **Contabilidad** | `Account`, `JournalEntry`, `AccountingPeriod` |
| **Productos** | `Product` (con `UomCode`→`global.sri_uom`, `SaleVatCode`→`global.sri_vat_rate`) |
| **MasterData** | `BusinessPartner`, `CompanyBusinessPartnerSettings` |
| **Sucursales** | `Branch`, `Establishment`, `EmissionPoint` |
| **Gastos** | `ExpenseInvoice`, `ExpenseDocument` |
| **Comercial** | `Quote`, `SalesOrder` |
| **Caja** | `BankAccount`, `PettyCash`, `BankStatement` |
| **Fiscal** | `SriSettings`, `DocumentSequence`, `DigitalCertificate` |

---

## REGLA FUNDAMENTAL: 1 CONCEPTO = 1 IMPLEMENTACIÓN

```
1 Entidad canónica
1 DTO principal (+ DTO detallado si difiere en campos)
1 Command de escritura por operación
1 Query de lectura por caso de uso
1 Repository por agregado raíz
```

---

## PATRONES PERMITIDOS

### DTO List vs Detail (PERMITIDO)

Es válido tener dos DTOs del mismo concepto cuando:
- **ListDto**: campos para tabla/listado (id, nombre, estado, fecha)
- **DetailDto**: campos para vista de detalle (+ navegaciones, líneas, historial)

```csharp
// ✅ CORRECTO — mismo concepto, propósito distinto
public record PurchBillDto(Guid Id, string InvoiceNumber, PurchaseStatus Status, ...);
public record PurchBillDetailDto(Guid Id, ..., IReadOnlyList<PurchBillLineDto> Lines);
```

**Límite:** Máximo 2 DTOs por entidad (List + Detail). Si se necesita un tercero,
revisar si no hay duplicación semántica.

### Queries especializadas (PERMITIDO)

```csharp
// ✅ CORRECTO — mismo agregado, propósito distinto
GetProductsQuery       // listado con filtros
GetProductByIdQuery    // detalle completo
GetProductFullReport   // reporte con todos los children
```

---

## PATRONES PROHIBIDOS

### ❌ Variantes de mismo concepto

```csharp
// ❌ PROHIBIDO — mismo concepto, distinto nombre
CreateProductCommand
AddProductCommand       // duplicado semántico de CreateProductCommand
RegisterProductCommand  // duplicado semántico
```

### ❌ DTOs con mismo propósito y distinto nombre

```csharp
// ❌ PROHIBIDO — mismo shape, distinto nombre
ProductDto
ProductSummaryDto       // si tiene los mismos campos que ProductDto
ProductResponseDto      // wrapping innecesario
```

### ❌ Naming patterns de degradación

```csharp
// ❌ PROHIBIDO — naming patterns que indican duplicación
BillingSettingsV2
SubscriberProfileLegacy
AlternativeTaxRate
ExtendedProductDto
ShadowInvoice
FallbackBillingConfig
```

### ❌ Cross-domain injection

```csharp
// ❌ PROHIBIDO — lógica SaaS en módulo ERP
namespace ERP.Application.Modules.Sales {
    // No puede referenciar SubscriberBillingAccount
    // No puede referenciar CommercialPlan
    // No puede tener lógica de suscripción
}
```

```csharp
// ❌ PROHIBIDO — lógica ERP en SUBSCRIBER
namespace ERP.Domain.Billing.Entities {
    // No puede tener Invoice (ERP)
    // No puede tener Product (ERP)
    // No puede tener TaxRate (GLOBAL)
}
```

### ❌ Tablas per-subscriber que duplican datos GLOBAL

```csharp
// ❌ PROHIBIDO — estas tablas fueron eliminadas y no pueden recrearse
tax_rates           // → global.sri_vat_rate + global.sri_ice_rate
units_of_measure    // → global.sri_uom
retention_settings  // → global.sri_retention_code
billing_settings    // → subscriber_billing_profile
```

---

## ENFORCEMENT RULESET

### B-08 — Single Command per Operation (BLOQUEANTE)

**Regla:** Para cada operación de escritura sobre un agregado, solo puede existir
UN Command. Dos Commands con el mismo target y misma acción son duplicación.

```
✅ CreateProductCommand     — crear producto
✅ UpdateProductCommand     — actualizar producto
❌ AddProductCommand        — duplica CreateProductCommand
❌ ModifyProductCommand     — duplica UpdateProductCommand
```

### B-09 — No Semantic DTO Duplication (BLOQUEANTE)

**Regla:** Máximo 2 DTOs por entidad (List + Detail). Un tercer DTO requiere
justificación documentada en el PR que demuestre propósito distinto.

```
✅ ProductDto              — campos para listado
✅ ProductDetailDto        — campos para vista completa + children
❌ ProductSummaryDto       — si tiene los mismos campos que ProductDto
❌ ProductResponseDto      — wrapping sin valor añadido
```

### B-10 — Scope Boundary Enforcement (BLOQUEANTE)

**Regla:** Las entidades de dominio no pueden cruzar boundaries de scope:

| Desde | Puede referenciar | NO puede referenciar |
|---|---|---|
| SUBSCRIBER | Solo entidades SUBSCRIBER | Entidades COMPANY, lógica ERP |
| COMPANY | Entidades COMPANY + catálogos GLOBAL | Entidades SUBSCRIBER billing |
| GLOBAL | Solo datos estáticos propios | Nada de SUBSCRIBER ni COMPANY |

### B-11 — No Per-Subscriber Regulatory Data (BLOQUEANTE)

**Regla:** Ninguna tabla per-subscriber puede almacenar datos que ya existen en
`global.*`. Los productos, facturas y retenciones deben referenciar directamente
`global.sri_*` por código.

```
✅ Product.UomCode = "19"          → referencia global.sri_uom
✅ Product.SaleVatCode = "10"      → referencia global.sri_vat_rate
❌ TaxRate(subscriber_id, 15%)     → duplica global.sri_vat_rate
❌ UnitOfMeasure(subscriber_id)    → duplica global.sri_uom
```

---

## GOVERNANCE CHECKLIST (PR REVIEW)

Antes de mergear cualquier PR que afecte Domain, Application o Infrastructure:

```
□ ¿La nueva entidad duplica semánticamente alguna existente?
□ ¿El nuevo DTO tiene propósito diferente a los DTOs existentes del mismo concepto?
□ ¿El nuevo Command hace algo diferente al Command de escritura existente?
□ ¿El nuevo servicio tiene responsabilidad distinta al servicio existente?
□ ¿Los datos regulatorios referencian global.sri_* en lugar de tablas per-subscriber?
□ ¿El código del módulo COMPANY NO importa entidades SUBSCRIBER billing?
□ ¿El código del módulo SUBSCRIBER NO importa lógica ERP operativa?
□ ¿El naming evita patrones prohibidos (V2, Legacy, Alternative, Extended, Shadow)?
```

Si cualquier checkbox es "SÍ" (hay violación) → **BLOQUEAR PR**.

---

## ESTADO ACTUAL VERIFICADO (2026-06-03)

Resultado del inventario pre-producción:

| Check | Resultado |
|---|---|
| DTOs duplicados semánticamente | ✅ NINGUNO — patrón List/Detail es intencional |
| Commands redundantes | ✅ NINGUNO detectado |
| Servicios paralelos | ✅ NINGUNO — responsabilidades distintas |
| Cross-domain violations | ✅ NINGUNO detectado |
| Naming prohibidos activos | ✅ NINGUNO en código (solo en migraciones históricas) |
| Tablas per-subscriber duplicando GLOBAL | ✅ ELIMINADAS — `tax_rates`, `units_of_measure`, `retention_settings` |
| Entidades billing duplicadas | ✅ RESUELTO — `SubscriberBillingProfile` es el modelo canónico único |

**Deuda técnica activa:** NINGUNA arquitectónica.

---

## REFERENCIA CRUZADA

| Área | Documento |
|---|---|
| Subscriber scope (histórico, archivado) | [docs/archive/SUBSCRIBER-SCOPE-SEALED.md](../docs/archive/SUBSCRIBER-SCOPE-SEALED.md) |
| Política legacy pre-prod | [CORE-ARCHITECTURE.md § Política de compatibilidad legacy](./CORE-ARCHITECTURE.md) |
| Reglas bloqueantes PR | [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md) (B-07 a B-11) |
| Conversiones de tipo | [CORE-ARCHITECTURE.md § Domain Purity](./CORE-ARCHITECTURE.md) |
| Catálogos globales | Schema `global` — 16 tablas SRI + INEC |
