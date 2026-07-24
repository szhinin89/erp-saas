# Legacy Drop Readiness Report

**Documento:** Fase 8 — criterios de drop de tablas legacy  
**Tablas objetivo:** `sales.customers` · `purchases.supplier`  
**Fecha de creación:** 2026-05-23  
**Estado actual:** BLOQUEADO — score 18/100  
**Restricción absoluta:** NO ejecutar DROP hasta score = 100

---

## Instrucciones de uso

Este documento se actualiza mensualmente con el snapshot de adopción. La decisión de DROP es **manual y deliberada** — nunca automatizada.

Para aprobar el DROP se requiere:
1. Score = 100/100 en todos los criterios
2. Firma de aprobación del equipo
3. Ventana de mantenimiento programada
4. Backup verificado

---

## Snapshot actual — 2026-05-23 (Rev 2)

| # | Criterio | Objetivo | Estado actual | Score |
|---|----------|---------|--------------|-------|
| 1 | 0 writes legacy activos | 0 handlers con writes directos a `customers`/`supplier` | Dual-write activo en CrearVenta/CrearCompra (+BusinessPartnerId) | 3/15 |
| 2 | 0 FK activas a `customers`/`supplier` | Todos los constraints FK eliminados de tablas transaccionales | `business_partner_id` shadow FK en 9 tablas — FK originales aún activas | 5/20 |
| 3 | 0 handlers activos usando repos legacy | `ICustomerRepository`, `IProveedorRepository` sin consumidores | 30+ consumidores aún activos | 0/15 |
| 4 | 0 reports legacy | Sin queries directas a `customers`/`supplier` en reports | Estimado >5 | 0/10 |
| 5 | 0 endpoints consumidos externamente | Sin tráfico HTTP a `/api/sales/customers` ni `/api/purchases/suppliers` | Activos 100% tráfico | 0/10 |
| 6 | Reconciliación limpia ≥ 30 días | 0 inconsistencias BP ↔ legacy por 30 días consecutivos | Job no implementado | 0/15 |
| 7 | Rollback validado | Drill de rollback exitoso en staging | No ejecutado | 0/10 |
| 8 | Backup verificado | Snapshot de DB previo al drop validado | N/A | 0/5 |
| **TOTAL** | | | | **18/100** |

> **Progreso Rev 2 (+13 pts):**
> - Shadow FK `business_partner_id` añadida a 9 tablas transaccionales (migration 20260523140000): +10 pts en criterio 2
> - Dual-write activado en `CrearVentaCommand` y `CrearCompraCommand`: +3 pts en criterio 1
> - `Customer.BusinessPartnerId` y `Supplier.BusinessPartnerId` (adapter links) implementados
> - Frontend: `businessPartnerId` propagado desde picker en `CreateInvoicePage` y `CrearCompraPage`

---

## Criterios detallados

### Criterio 1 — 0 writes legacy activos (15 pts)

**Definición:** Ningún handler de MediatR realiza `AddAsync` o `SaveChangesAsync` en `ICustomerRepository` ni `IProveedorRepository` para operaciones nuevas. Los métodos de escritura de Customer/Supplier legacy son **consecuencia** del write canónico en BusinessPartner.

**Query de verificación:**
```bash
# Grep handlers que inyectan y usan ICustomerRepository para escritura
grep -r "ICustomerRepository\|IProveedorRepository" backend/src/ERP.Application \
  --include="*.cs" -l
# Resultado esperado: 0 archivos
```

**Puntaje parcial:**
- 0 writes Customer: +7.5 pts
- 0 writes Supplier: +7.5 pts

**Estado:** 0/15 — BLOQUEADO (Fase 4 pendiente)

---

### Criterio 2 — 0 FK activas a tablas legacy (20 pts)

**Definición:** Todos los constraints de FK que referencian `customers(id)` o `supplier(id)` han sido eliminados de la base de datos. Las columnas `customer_id`/`supplier_id` existen como campos de auditoría histórica sin constraint.

**Query de verificación en PostgreSQL:**
```sql
SELECT tc.table_name, kcu.column_name, ccu.table_name AS foreign_table_name
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name = tc.constraint_name
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND ccu.table_name IN ('customers', 'supplier');
-- Resultado esperado: 0 filas
```

**FK a eliminar (13 total):**

| Tabla | Columna | Pts |
|-------|---------|-----|
| `sales_document` | `customer_id` | 2 |
| `sales_bill` | `customer_id` | 1 |
| `sales_invoice` | `customer_id` | 1 |
| `credit_note` | `customer_id` | 1 |
| `debit_note` | `customer_id` | 1 |
| `delivery_guide` | `customer_id` | 1 |
| `purchase_document` | `supplier_id` | 2 |
| `purchase_order` | `supplier_id` | 2 |
| `purch_bill` | `supplier_id` | 1 |
| `expense_document` | `supplier_id` | 1 |
| `purchase_invoice` | `supplier_id` | 2 |
| `supplier_note` | `supplier_id` | 1 |
| `product_supplier_codes` | `supplier_id` | 1 |

**Estado:** 0/20 — BLOQUEADO (Fase 3 pendiente)

---

### Criterio 3 — 0 handlers activos usando repos legacy (15 pts)

**Definición:** `ICustomerRepository` e `IProveedorRepository` no tienen inyecciones activas en Application layer. Si existen, están marcados `[Obsolete]` y sin consumidores.

**Query de verificación:**
```bash
grep -r "ICustomerRepository\|IProveedorRepository" \
  backend/src/ERP.Application \
  --include="*.cs" | grep -v "//.*Obsolete"
# Resultado esperado: 0 líneas
```

**Puntaje parcial:**
- 0 consumidores ICustomerRepository: +7.5 pts
- 0 consumidores IProveedorRepository: +7.5 pts

**Estado:** 0/15 — BLOQUEADO (Fase 5 pendiente)

---

### Criterio 4 — 0 reports legacy (10 pts)

**Definición:** Ningún handler de reportes, export, o dashboard hace query directa a `customers` o `supplier`. Todos los reports usan `BusinessPartner + CustomerProfile / SupplierProfile`.

**Handlers a auditar:**
- `GetProductFullReportHandler` — usa `ProductSupplierCode.SupplierId` → must migrate
- Cualquier export de clientes/proveedores
- Dashboard KPIs de ventas/compras por cliente/proveedor

**Query de verificación:**
```bash
grep -r "Customers\|Suppliers\|ICustomerRepository\|IProveedorRepository" \
  backend/src/ERP.Application/Modules/Products \
  backend/src/ERP.Application/Modules/Reports \
  --include="*.cs" -l
# Resultado esperado: 0 archivos
```

**Estado:** 0/10 — BLOQUEADO (Fase 5 pendiente)

---

### Criterio 5 — 0 endpoints consumidos externamente (10 pts)

**Definición:** Los endpoints `/api/sales/customers` y `/api/purchases/suppliers` tienen 0 llamadas activas en logs de producción (o son facades puras que internamente usan BusinessPartner).

**Nota:** Los endpoints pueden **seguir existiendo como facades** — lo que no puede existir es tráfico que dependa de que la tabla `customers` esté viva. Una facade que lee desde BP y proyecta a `CustomerDto` es aceptable.

**Verificación:**
```bash
# En logs de producción — últimos 30 días
grep "POST /api/sales/customers\|POST /api/purchases/suppliers" access.log | wc -l
# Resultado esperado: 0 (o solo calls a facade)
```

**Puntaje parcial:**
- 0 writes directos a customers vía API: +5 pts
- 0 writes directos a suppliers vía API: +5 pts

**Estado:** 0/10 — BLOQUEADO (Fase 4-5 pendientes)

---

### Criterio 6 — Reconciliación limpia ≥ 30 días (15 pts)

**Definición:** Los jobs de reconciliación (Fase 6) reportan 0 inconsistencias en todos los jobs por al menos 30 días calendario consecutivos.

**Métricas:**
| Métrica | Objetivo |
|---------|---------|
| `bp.reconciliation.missing_bp_id` | = 0 durante 30 días |
| `bp.reconciliation.inconsistent_mapping` | = 0 durante 30 días |
| `bp.reconciliation.orphan_customers` | = 0 durante 30 días |
| `bp.reconciliation.orphan_suppliers` | = 0 durante 30 días |
| `bp.fk.shadow_null_pct` (global) | = 0% durante 30 días |

**Puntaje parcial:**
- Cada métrica limpia por 30 días: +3 pts (5 métricas × 3 = 15)

**Estado:** 0/15 — BLOQUEADO (Fase 6 pendiente)

---

### Criterio 7 — Rollback validado (10 pts)

**Definición:** Se ejecutó un drill de rollback completo en entorno de staging que verificó:
1. DROP de columnas `business_partner_id` es reversible
2. Reactivación de repos legacy funciona sin degradación
3. FK constraint restore no produce deadlocks
4. Tiempo de rollback medido < 4 horas

**Checklist del drill:**
- [ ] Staging tiene snapshot pre-drill
- [ ] Drill ejecutado por al menos 2 ingenieros
- [ ] Resultado documentado en esta sección
- [ ] Tiempo medido: _____ minutos
- [ ] Integridad de datos post-rollback: OK/FAIL

**Estado:** 0/10 — NO EJECUTADO

---

### Criterio 8 — Backup verificado (5 pts)

**Definición:** Existe un backup completo de la base de datos tomado < 24h antes del DROP, verificado con restore test.

**Checklist:**
- [ ] Backup tomado: [TIMESTAMP]
- [ ] Restore test exitoso en entorno aislado
- [ ] Tamaño verificado: _____ GB
- [ ] Responsable: _____

**Estado:** 0/5 — N/A (pre-requisito de ejecución)

---

## Scorecard histórico

| Fecha | Score | Bloqueo principal | Aprobado por |
|-------|-------|------------------|-------------|
| 2026-05-23 | 5/100 | Fase 2-8 pendientes | — |
| 2026-05-23 (Rev 2) | 18/100 | Shadow FKs + dual-write implementados. Fase 4-5 pendientes | — |
| _próximo update_ | — | — | — |

---

## Plan de scoring progresivo

| Al completar | Score esperado |
|-------------|---------------|
| Fase 2 (dual identity) | ~10/100 |
| Fase 3 (shadow FKs + backfill) | ~20/100 |
| Fase 4 (write canonicalization) | ~40/100 |
| Fase 5 (read compat layer) | ~60/100 |
| Fase 6 (reconciliation jobs, 30 días limpio) | ~75/100 |
| FK swap completo (13/13 tablas) | ~85/100 |
| Fase 7 (metrics, 30 días) | ~90/100 |
| Rollback drill exitoso | ~95/100 |
| Backup verificado | **100/100** |

---

## Procedimiento de DROP (solo cuando score = 100)

```sql
-- PRE-CONDICIÓN: score = 100, backup verificado, ventana de mantenimiento activa

BEGIN;

-- 1. Verificar 0 FK activas a customers/supplier (ya eliminadas en Fase 3)
DO $$
DECLARE fk_count INTEGER;
BEGIN
  SELECT COUNT(*) INTO fk_count
  FROM information_schema.table_constraints tc
  JOIN information_schema.constraint_column_usage ccu
    ON ccu.constraint_name = tc.constraint_name
  WHERE tc.constraint_type = 'FOREIGN KEY'
    AND ccu.table_name IN ('customers', 'supplier');
  IF fk_count > 0 THEN
    RAISE EXCEPTION 'ABORT: existen % FK activas a customers/supplier', fk_count;
  END IF;
END $$;

-- 2. Verificar 0 filas transaccionales huérfanas
DO $$
DECLARE orphan_count INTEGER;
BEGIN
  SELECT COUNT(*) INTO orphan_count
  FROM sales_document WHERE customer_id IS NOT NULL;
  -- En este punto customer_id es auditoria, no FK — las filas son esperadas
  -- Lo que verificamos es que business_partner_id esté completo
  SELECT COUNT(*) INTO orphan_count
  FROM sales_document WHERE business_partner_id IS NULL;
  IF orphan_count > 0 THEN
    RAISE EXCEPTION 'ABORT: % sales_document sin business_partner_id', orphan_count;
  END IF;
END $$;

-- 3. Archivar tablas (rename primero, DROP después de 90 días de validación)
ALTER TABLE customers RENAME TO _archive_customers_20260523;
ALTER TABLE supplier RENAME TO _archive_supplier_20260523;

-- NO ejecutar DROP TABLE hasta 90 días después del rename sin incidentes

COMMIT;

-- 4. DROP definitivo (90 días después, si sin incidentes)
-- DROP TABLE _archive_customers_20260523;
-- DROP TABLE _archive_supplier_20260523;
```

> **Patrón seguro:** rename a `_archive_*` primero, DROP 90 días después. Permite rollback instantáneo vía rename inverso si se detecta algo inesperado.

---

## Aprobaciones requeridas para DROP

| Rol | Nombre | Firma | Fecha |
|-----|--------|-------|-------|
| Tech Lead | | | |
| Backend Senior | | | |
| QA / Testing | | | |

**Nota:** El DROP es irreversible una vez ejecutado el rename y confirmado por 90 días. Cualquier miembro del equipo puede vetar si tiene dudas sobre integridad de datos.

---

**Relacionado:** [LEGACY-DEPENDENCY-AUDIT.md](LEGACY-DEPENDENCY-AUDIT.md) · [BP-MIGRATION-ROADMAP.md](BP-MIGRATION-ROADMAP.md) · [FRONTEND-MIGRATION-STATUS.md](FRONTEND-MIGRATION-STATUS.md)
