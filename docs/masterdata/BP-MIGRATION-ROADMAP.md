# BusinessPartner Migration Roadmap — Fases 2–8

**Fecha:** 2026-05-23  
**Baseline audit:** [LEGACY-DEPENDENCY-AUDIT.md](LEGACY-DEPENDENCY-AUDIT.md)  
**Restricciones absolutas:** NO DROP TABLE · NO eliminar CustomerId/SupplierId · NO romper historial · NO romper reportes

---

## Estado de partida (post-Fase 1)

| Item | Estado actual |
|------|--------------|
| BusinessPartner aggregate | Creado — subscriber-scoped |
| CustomerProfile / SupplierProfile | Creados — 1:1 con BP |
| CompanyBpSettings | Creado — company-scoped operational config |
| Enricher (legacy ↔ BP) | Activo — resuelve `legacyCustomerId`/`legacySupplierId` |
| Reconciliation service | Activo — sincroniza Customer/Supplier → BP+Profile |
| Writes transaccionales via BP | 0% — todos pasan por Customer/Supplier legacy |
| Shadow FKs (`business_partner_id`) | 0 tablas migradas |
| Drop-readiness score | ~5 / 100 |

---

## Fase 2 — Canonical Identity Transition

**Objetivo:** Exponer `BusinessPartnerId` en todos los contratos y DTOs transaccionales, manteniendo `CustomerId`/`SupplierId` como campos de compatibilidad. Sin cambios de schema todavía.

**Modelo temporal (dual identity):**
```
SalesDocument {
  CustomerId       Guid?   // compatibility — sigue siendo la FK real
  BusinessPartnerId Guid?  // nuevo campo de aplicación (no FK en DB aún)
}
```

### 2.1 Backend — DTOs transaccionales

Agregar `BusinessPartnerId` como campo opcional en:

| DTO | Campo a agregar |
|-----|----------------|
| `CrearVentaCommand` | `BusinessPartnerId Guid?` |
| `CrearCompraCommand` | `BusinessPartnerId Guid?` |
| `CrearOrdenCompraCommand` | `BusinessPartnerId Guid?` |
| `CrearGastoCommand` | `BusinessPartnerId Guid?` |

Lógica de resolución en handlers (sin cambio de schema DB):
```csharp
// Si viene BusinessPartnerId, resolver al CustomerId legacy via enricher
// Si viene CustomerId, mantener tal cual
// Los dos campos NO pueden venir vacíos simultáneamente (validación)
var customerId = request.CustomerId
    ?? await _enricher.ResolveToLegacyCustomerIdAsync(request.BusinessPartnerId!.Value);
```

### 2.2 Backend — Queries (reads)

Agregar `BusinessPartnerId` como filtro alternativo en:

| Query | Cambio |
|-------|--------|
| `GetVentasListQuery` | Aceptar `BusinessPartnerId` como filtro alternativo a `CustomerId` |
| `GetComprasQuery` | Ídem para `SupplierId` |
| `GetGastosQuery` | Ídem para `SupplierId` |
| `GetOrdenesCompraListQuery` | Ídem para `SupplierId` |

### 2.3 Frontend — pickers

Los pickers de customer/supplier en formularios de venta/compra/gasto deben:
1. Llamar `/api/business-partners` (ya implementado) — obtener `BusinessPartner.Id`
2. Enviar `businessPartnerId` en el request (nuevo campo)
3. Mantener compatibilidad: si el backend devuelve `legacyCustomerId`, usar para display

### 2.4 Entregables Fase 2

- [ ] `CrearVentaCommand` acepta `BusinessPartnerId` alternativo
- [ ] `CrearCompraCommand` acepta `BusinessPartnerId` alternativo
- [ ] `CrearOrdenCompraCommand` acepta `BusinessPartnerId` alternativo
- [ ] `CrearGastoCommand` acepta `BusinessPartnerId` alternativo
- [ ] Handlers resuelven BP → legacy ID via `BusinessPartnerOperationalLinkEnricher`
- [ ] Queries aceptan `BusinessPartnerId` como filtro alternativo
- [ ] Frontend pickers envían `businessPartnerId`
- [ ] Tests de integración verifican path BP → legacy ID

**Rollback Fase 2:** Revertir DTOs. Sin cambio de schema — rollback en 0 migraciones.

---

## Fase 3 — Shadow FK Migration

**Objetivo:** Agregar columnas nullable `business_partner_id` en todas las tablas transaccionales. Migrar datos históricos. Las columnas legacy siguen siendo las FK reales.

### 3.1 Columnas a agregar

| Tabla | Columna nueva | Nullable | Índice sugerido |
|-------|--------------|----------|----------------|
| `sales_document` | `business_partner_id UUID` | Sí | `(subscriber_id, business_partner_id, issue_date)` |
| `sales_bill` | `business_partner_id UUID` | Sí | `(subscriber_id, business_partner_id)` |
| `purchase_document` | `business_partner_id UUID` | Sí | `(subscriber_id, business_partner_id)` |
| `purch_bill` | `business_partner_id UUID` | Sí | — |
| `purchase_order` | `business_partner_id UUID` | Sí | `(subscriber_id, business_partner_id)` |
| `expense_document` | `business_partner_id UUID` | Sí | — |
| `expense_invoice` | `business_partner_id UUID` | Sí | — |
| `purchase_invoice` | `business_partner_id UUID` | Sí | — |
| `supplier_note` | `business_partner_id UUID` | Sí | — |
| `sales_invoice` (SRI) | `business_partner_id UUID` | Sí | — |
| `credit_note` | `business_partner_id UUID` | Sí | — |
| `debit_note` | `business_partner_id UUID` | Sí | — |
| `delivery_guide` | `business_partner_id UUID` | Sí | — |
| `issued_retention` | `business_partner_id UUID` | Sí | — |
| `product_supplier_codes` | `business_partner_id UUID` | Sí | — |

> **NO agregar FK constraint todavía** — la columna es informacional. El constraint se agrega en Fase 5 cuando la columna sea NOT NULL.

### 3.2 Migración de datos históricos

Script de backfill (ejecutar como migración EF o script SQL transaccional):

```sql
-- Ejemplo para sales_document
UPDATE sales_document sd
SET business_partner_id = cp.business_partner_id
FROM master_customer_profiles cp
WHERE sd.customer_id = cp.id
  AND sd.business_partner_id IS NULL;

-- Ejemplo para purchase_document
UPDATE purchase_document pd
SET business_partner_id = sp.business_partner_id
FROM master_supplier_profiles sp
WHERE pd.supplier_id = sp.id
  AND pd.business_partner_id IS NULL;
```

**Precondiciones del backfill:**
1. El reconciliation service debe haber procesado todos los Customer → BP existentes
2. Verificar 0 Customer sin BP correspondiente antes de correr backfill
3. Ejecutar en transacción con rollback automático si `COUNT(business_partner_id IS NULL) > 0` post-backfill

### 3.3 Entidades .NET — campos shadow

Agregar en entidades (sin romper constructores):
```csharp
// SalesDocument — campo shadow, no FK constraint aún
public Guid? BusinessPartnerId { get; private set; }

internal void SetBusinessPartnerId(Guid bpId)
    => BusinessPartnerId = bpId;
```

### 3.4 Entregables Fase 3

- [ ] Migración EF con todas las columnas `business_partner_id` nullable
- [ ] Script de backfill histórico ejecutado y verificado
- [ ] Cobertura de backfill >= 99.9% (Fase 6 reconciliation detectará el 0.1%)
- [ ] Campos shadow en entidades .NET
- [ ] Handlers Fase 2 empiezan a escribir `business_partner_id` junto con el legacy ID
- [ ] Índices en columnas de alta cardinalidad (sales_document, purchase_document, purchase_order)

**Rollback Fase 3:** `DROP COLUMN business_partner_id` en todas las tablas (columnas nullable, sin constraint). No afecta operación.

---

## Fase 4 — Write Canonicalization

**Objetivo:** Todas las nuevas escrituras resuelven `BusinessPartnerId` primero. Los legacy IDs (`CustomerId`/`SupplierId`) pasan a ser **derivados** desde el BP.

### 4.1 Flujo de resolución canónico

```
Request (POST /sales/invoices)
  → Recibe businessPartnerId
  → Handler resuelve:
      customerId = BP.CustomerProfile.LegacyId   // derivado
  → Crea SalesDocument {
      CustomerId        = customerId              // compat field
      BusinessPartnerId = businessPartnerId       // canonical field
    }
```

### 4.2 Cambios en handlers WRITE

| Handler | Cambio |
|---------|--------|
| `CrearVentaCommandHandler` | `BusinessPartnerId` requerido → resolver `CustomerId` vía perfil BP |
| `CrearCompraCommandHandler` | `BusinessPartnerId` requerido → resolver `SupplierId` vía perfil BP |
| `CrearOrdenCompraCommandHandler` | `BusinessPartnerId` requerido → resolver `SupplierId` |
| `CrearGastoCommandHandler` | `BusinessPartnerId` opcional → resolver `SupplierId` si viene |
| `EmitirFacturaElectronicaCommandHandler` | Cargar datos SRI desde `BusinessPartner` + `SupplierProfile` en lugar de `Customer` |

### 4.3 Compatibilidad hacia atrás

Los endpoints legacy (`POST /api/sales/customers`, `POST /api/purchases/suppliers`) siguen activos pero:
- Internamente crean `BusinessPartner` + Profile primero
- Luego crean Customer/Supplier legacy para mantener FK compatibility
- Es decir: los writes legacy pasan a ser **consecuencia** del write canónico

```csharp
// CreateCustomerCommandHandler — nueva implementación (Fase 4)
// 1. Crear BusinessPartner (canónico)
var bp = BusinessPartner.Create(...);
await _bpRepository.AddAsync(bp);
// 2. Crear CustomerProfile
var profile = CustomerProfile.Create(bp.Id, ...);
await _profileRepository.AddAsync(profile);
// 3. Crear Customer legacy (compatibilidad FK)
var customer = Customer.Create(...);
customer.SetBusinessPartnerId(bp.Id);  // link shadow
await _customerRepository.AddAsync(customer);
```

### 4.4 Dual-write en UnifiedDocumentSync

`UnifiedDocumentSync` debe escribir `business_partner_id` en ambas tablas al sincronizar `sales_bill` ↔ `sales_document`.

### 4.5 Entregables Fase 4

- [ ] Handlers WRITE usan `BusinessPartnerId` como campo canónico principal
- [ ] `CustomerId`/`SupplierId` se derivan automáticamente desde BP profile
- [ ] `CreateCustomerCommandHandler` crea BP+Profile primero, Customer legacy segundo
- [ ] `CreateProveedorCommandHandler` ídem para BP+SupplierProfile
- [ ] `UnifiedDocumentSync` propaga `business_partner_id`
- [ ] Métrica: % nuevas escrituras con `business_partner_id != NULL` ≥ 95%
- [ ] Feature flag `bp_canonical_writes` para rollback controlado

**Rollback Fase 4:** Desactivar feature flag → vuelve a flujo Fase 3 (ambos IDs, BP derivado de legacy). Sin cambio de schema.

---

## Fase 5 — Read Compatibility Layer

**Objetivo:** Tablas legacy se vuelven **read-only** para lógica de negocio nueva. Toda lectura nueva usa `BusinessPartner` + Profiles. Adapters mantienen compatibilidad hacia frontend legacy.

### 5.1 Interfaces deprecadas (solo lectura)

Marcar `ICustomerRepository` e `IProveedorRepository` como `[Obsolete("Use IBusinessPartnerRepository")]`.

Queries que leen Customer/Supplier directamente migran a leer desde `BusinessPartner + CustomerProfile / SupplierProfile`:

| Query antes | Query después |
|------------|--------------|
| `GetCustomersQuery` → `ICustomerRepository.GetAsync` | `SearchBusinessPartnersQuery` con `hasCustomerProfile=true` |
| `GetCustomerByIdQuery` | `GetBusinessPartnerQuery` + `CustomerProfile` |
| `GetProveedoresQuery` | `SearchBusinessPartnersQuery` con `hasSupplierProfile=true` |
| `GetVentasListQuery` filtra por `CustomerId` | Filtra por `BusinessPartnerId` (columna shadow ya poblada) |
| `GetComprasQuery` filtra por `SupplierId` | Filtra por `BusinessPartnerId` |

### 5.2 Adapters en API

Los controladores `CustomersController` y `SuppliersController` se convierten en **facades**:
- Internamente llaman a `BusinessPartnerRepository`
- Proyectan respuesta al formato `CustomerDto` / `SupplierDto` existente
- Sin cambio de contrato API → frontend legacy no se rompe

```csharp
// CustomersController.GetAll — Fase 5
[HttpGet]
public async Task<IActionResult> GetAll(...)
{
    // Lee desde BusinessPartner + CustomerProfile
    var bps = await _mediator.Send(new SearchBusinessPartnersQuery {
        HasCustomerProfile = true,
        Search = search
    });
    // Proyecta al DTO legacy
    return Ok(bps.Select(CustomerProfileAdapter.ToCustomerDto));
}
```

### 5.3 Frontend

- Pickers de venta/compra/gasto usan `/api/business-partners` directamente
- Páginas `CustomersPage` y `SuppliersPage` continúan funcionando via adapter
- Adaptadores `businessPartnerCustomerAdapter.ts` y `businessPartnerSupplierAdapter.ts` (ya existentes) siguen activos

### 5.4 Entregables Fase 5

- [ ] `ICustomerRepository` marcado `[Obsolete]`
- [ ] `IProveedorRepository` marcado `[Obsolete]`
- [ ] `CustomersController` y `SuppliersController` internamente leen desde BP
- [ ] Queries de lista y detalle de Customer/Supplier leen desde BP+Profiles
- [ ] Queries de transacciones filtran por `business_partner_id` (columna shadow)
- [ ] Tests de integración actualizados para crear BP en lugar de Customer/Supplier directo
- [ ] E2E Playwright actualizados
- [ ] Métrica: % reads usando BP ≥ 95%

**Rollback Fase 5:** Quitar `[Obsolete]`, reactivar implementaciones directas de repositories legacy. Sin cambio de schema.

---

## Fase 6 — Shadow Validation (Reconciliation Jobs)

**Objetivo:** Detectar y alertar sobre inconsistencias entre `customer_id`/`supplier_id` legacy y `business_partner_id` shadow antes de hacer NOT NULL constraint.

### 6.1 Jobs de reconciliación

#### Job A: `SalesDocumentBpReconciliationJob`

```sql
-- Documentos de venta sin business_partner_id
SELECT COUNT(*) FROM sales_document
WHERE customer_id IS NOT NULL
  AND business_partner_id IS NULL;

-- Inconsistencias: business_partner_id apunta a BP diferente del customer_id
SELECT COUNT(*) FROM sales_document sd
JOIN master_customer_profiles cp ON cp.id = sd.customer_id
WHERE sd.business_partner_id != cp.business_partner_id;
```

#### Job B: `PurchaseDocumentBpReconciliationJob`

```sql
-- POs sin business_partner_id
SELECT COUNT(*) FROM purchase_order
WHERE supplier_id IS NOT NULL
  AND business_partner_id IS NULL;

-- Inconsistencias
SELECT COUNT(*) FROM purchase_document pd
JOIN master_supplier_profiles sp ON sp.id = pd.supplier_id
WHERE pd.business_partner_id != sp.business_partner_id;
```

#### Job C: `OrphanDetectionJob`

```sql
-- Customers sin BP correspondiente (huérfanos)
SELECT c.id FROM customers c
LEFT JOIN master_customer_profiles cp ON cp.id = c.id
WHERE cp.id IS NULL AND c.deleted_at IS NULL;

-- Suppliers sin BP
SELECT s.id FROM supplier s
LEFT JOIN master_supplier_profiles sp ON sp.id = s.id
WHERE sp.id IS NULL AND s.deleted_at IS NULL;
```

### 6.2 Métricas de reconciliación

Los jobs emiten métricas al sistema de observabilidad:

| Métrica | Umbral para drop | Descripción |
|---------|-----------------|-------------|
| `bp.reconciliation.missing_bp_id` | = 0 | Filas con legacy ID pero sin `business_partner_id` |
| `bp.reconciliation.inconsistent_mapping` | = 0 | Mismatch entre legacy ID y BP ID |
| `bp.reconciliation.orphan_customers` | = 0 | Customers sin BP |
| `bp.reconciliation.orphan_suppliers` | = 0 | Suppliers sin BP |

### 6.3 Proceso de corrección

Cuando un job detecta inconsistencias:
1. Loguear con nivel `Error` el ID del documento inconsistente
2. Intentar auto-repair via `BusinessPartnerReconciliationService` (ya existente)
3. Si no puede auto-reparar → enqueue en tabla `bp_reconciliation_errors` para revisión manual
4. Alert en Slack/email si count > umbral configurable

### 6.4 Entregables Fase 6

- [ ] `SalesDocumentBpReconciliationJob` implementado y ejecutándose en schedule
- [ ] `PurchaseDocumentBpReconciliationJob` implementado
- [ ] `ExpenseDocumentBpReconciliationJob` implementado
- [ ] `PurchaseOrderBpReconciliationJob` implementado
- [ ] `OrphanDetectionJob` implementado
- [ ] Tabla `bp_reconciliation_errors` creada
- [ ] Métricas emitidas a observability (`docs/observability/METRICS.md`)
- [ ] Dashboard de reconciliación visible
- [ ] Criterio de paso: todos los jobs reportan 0 inconsistencias por 7 días consecutivos

---

## Fase 7 — Production Metrics

**Objetivo:** Medir en producción el avance de la transición para tomar decisión de drop informada.

### 7.1 Métricas clave

| Métrica | Target para drop | Instrumento |
|---------|-----------------|-------------|
| `bp.writes.canonical_pct` | ≥ 99% | % de writes transaccionales con `business_partner_id != NULL` |
| `bp.reads.legacy_dependency_pct` | ≤ 1% | % de queries que leen directamente de `customers`/`supplier` |
| `bp.fk.shadow_null_pct` | = 0% | % de filas en tablas transaccionales con `business_partner_id = NULL` |
| `bp.legacy.active_handlers` | = 0 | Handlers que aún usan `ICustomerRepository`/`IProveedorRepository` directamente |
| `bp.legacy.active_endpoints` | = 0 | Endpoints que persisten en Customer/Supplier en lugar de BP |
| `bp.reconciliation.clean_days` | ≥ 30 días | Días consecutivos sin inconsistencias |
| `bp.legacy.fk_migrated_tables` | = 13 / 13 | FK constraint swapped de legacy a BP |

### 7.2 Dashboard de adopción

Documento de snapshot mensual (generado manualmente o por job):

```
=== BusinessPartner Adoption Report — [FECHA] ===
Writes usando BP como canónico:   [X]%
Reads desde BP (no legacy):       [X]%
Shadow FKs NOT NULL:              [X/13] tablas
FK constraints swapped a BP:      [X/13] tablas
Handlers legacy activos:          [X] (target: 0)
Endpoints legacy activos:         [X] (target: 0)
Días de reconciliación limpia:    [X] (target: ≥30)
DROP READINESS SCORE:             [X/100]
```

### 7.3 Entregables Fase 7

- [ ] Instrumentación de métricas en handlers (Fase 4 + 5)
- [ ] Dashboard de adopción operacional
- [ ] Proceso de review mensual del adoption report
- [ ] Criterios de drop formalmente evaluados y documentados

---

## FK Transition Plan

Plan detallado para migrar cada FK de `customer_id`/`supplier_id` a `business_partner_id`.

### Orden de migración (menor a mayor riesgo)

| Prioridad | Tabla | FK legacy | Nueva FK | Condición para swap |
|-----------|-------|-----------|----------|---------------------|
| 1 | `product_supplier_codes` | `supplier_id → supplier` | `business_partner_id → master_business_partner` | ProductSupplierCode es no transaccional; bajo riesgo |
| 2 | `expense_document` | `supplier_id → supplier` (nullable) | `business_partner_id` (nullable) | Nullable — bajo riesgo |
| 3 | `expense_invoice` | `supplier_id → supplier` (nullable) | `business_partner_id` (nullable) | Nullable |
| 4 | `supplier_note` | `supplier_id` | `business_partner_id` | Bajo volumen |
| 5 | `purch_note` | `supplier_id` | `business_partner_id` | Bajo volumen |
| 6 | `issued_retention` | `supplier_id` | `business_partner_id` | Medio volumen |
| 7 | `purchase_document` | `supplier_id` | `business_partner_id` | Medio volumen |
| 8 | `purch_bill` | `supplier_id` | `business_partner_id` | Sync con purchase_document |
| 9 | `purchase_order` | `supplier_id` (NOT NULL) | `business_partner_id` (NOT NULL) | Alto volumen; requiere 0 NULLs validados |
| 10 | `purchase_invoice` | `supplier_id` (NOT NULL) | `business_partner_id` (NOT NULL) | Alto volumen |
| 11 | `sales_document` | `customer_id` (nullable) | `business_partner_id` (nullable) | Muy alto volumen — tabla core |
| 12 | `sales_bill` | `customer_id` | `business_partner_id` | Sync con sales_document |
| 13 | `sales_invoice` (SRI) | `customer_id` | `business_partner_id` | Documento fiscal — validar SRI |

### Proceso de swap por tabla

```sql
-- Paso 1: Verificar cobertura total
SELECT COUNT(*) FROM sales_document
WHERE customer_id IS NOT NULL AND business_partner_id IS NULL;
-- Debe retornar 0

-- Paso 2: Hacer NOT NULL la columna shadow
ALTER TABLE sales_document
  ALTER COLUMN business_partner_id SET NOT NULL;

-- Paso 3: Agregar FK constraint a master_business_partner
ALTER TABLE sales_document
  ADD CONSTRAINT fk_sales_document_bp
  FOREIGN KEY (business_partner_id)
  REFERENCES master_business_partner(id)
  ON DELETE RESTRICT;

-- Paso 4: Hacer legacy_id nullable (compat temporaria)
-- NO eliminar aún
ALTER TABLE sales_document
  ALTER COLUMN customer_id DROP NOT NULL;
```

### Criterios de swap por tabla

Antes de hacer NOT NULL + FK constraint en cualquier tabla:
1. `COUNT(business_partner_id IS NULL) = 0` en esa tabla
2. Job de reconciliación para esa tabla: 0 inconsistencias por 7 días
3. Verificar que todos los writes nuevos llenan `business_partner_id`
4. Plan de rollback documentado (ver sección siguiente)

---

## Rollback Plan

### Principio

**Cada fase tiene rollback independiente.** El rollback nunca requiere DROP TABLE ni eliminar datos.

### Rollback por fase

| Fase | Rollback | Costo | Tiempo estimado |
|------|---------|-------|----------------|
| **Fase 2** (dual identity en DTOs) | Revert DTOs — quitar `BusinessPartnerId` opcional. Sin schema changes. | Bajo | < 1h |
| **Fase 3** (shadow columns) | `ALTER TABLE ... DROP COLUMN business_partner_id` en las tablas afectadas. Columnas nullable — safe. | Bajo | < 2h |
| **Fase 4** (write canonicalization) | Desactivar feature flag `bp_canonical_writes` → vuelve a flujo Fase 3. Sin schema change. | Bajo | < 30min |
| **Fase 5** (read compat layer) | Quitar `[Obsolete]`, reactivar repositories legacy en DI. Sin schema change. | Bajo | < 1h |
| **Fase 6** (reconciliation jobs) | Deshabilitar jobs. Noop — los jobs son solo lectura. | Mínimo | < 15min |
| **Fase 7** (metrics) | Deshabilitar instrumentación. Noop. | Mínimo | < 15min |
| **FK swap (por tabla)** | `ALTER TABLE ... DROP CONSTRAINT fk_..._bp; ALTER TABLE ... ALTER COLUMN customer_id SET NOT NULL;` | Medio | < 1h por tabla |

### Rollback global (emergencia)

Si se detecta corrupción de datos en producción:

```sql
-- 1. Desactivar feature flags en DB
UPDATE subscriber_feature_flags SET enabled = false WHERE key LIKE 'bp_%';

-- 2. Reactivar endpoints legacy via config
-- feature_flags.bp_canonical_writes = false

-- 3. Verificar integridad FK legacy (deben seguir activas)
SELECT COUNT(*) FROM sales_document sd
LEFT JOIN customers c ON c.id = sd.customer_id
WHERE sd.customer_id IS NOT NULL AND c.id IS NULL;
-- Debe retornar 0 (FK Restrict protege esto)
```

---

## Restricciones permanentes durante toda la transición

1. **NO DROP TABLE** `customers` ni `supplier` hasta drop-readiness score = 100
2. **NO eliminar** columnas `customer_id` / `supplier_id` hasta que FK swap esté completo para esa tabla
3. **NO romper** endpoints `/api/sales/customers` ni `/api/purchases/suppliers` — seguir funcionando como facade
4. **NO eliminar** historial de documentos — `customer_id`/`supplier_id` quedan como campos de auditoría
5. **NO hacer** el swap de FK en producción sin validación de reconciliación limpia previa
6. **NO omitir** el dual-write durante el período de transición en Fases 3-4

---

**Siguiente documento:** [LEGACY-DROP-READINESS.md](LEGACY-DROP-READINESS.md) — criterios y checklist para eliminar tablas legacy.
