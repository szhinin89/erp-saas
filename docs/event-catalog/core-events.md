# Core Events — ERP SaaS ZH Technologies

Catálogo de Domain Events activos y planificados del ERP core.

| Estado | Significado |
|--------|-------------|
| ✅ Activo | Emitido y persistido en Outbox |
| 🟡 Planificado | Diseñado, pendiente de implementar |
| ⏳ Futuro | Para Fase 4+ |

---

## 📋 Ventas / Facturación

### `SalesNoteAuthorizedEvent` ✅ Activo
- **Módulo:** Sales
- **AggregateRoot:** SalesNote
- **Descripción:** Nota de ventas autorizada en SRI. Dispara movimientos de stock y asientos contables.
- **Tenant scope:** Sí (SubscriberId)
- **EventVersion:** 1
- **Payload clave:** NoteId, SubscriberId, UserId, WarehouseId, CompanyId, NoteNumber, StockLines
- **Consumers futuros:**
  - Analytics: ventas diarias por bodega
  - AI: predicción de demanda por producto

---

### `InvoiceCreatedEvent` 🟡 Planificado
- **Módulo:** Sales
- **AggregateRoot:** SalesBill / SalesDocument
- **Descripción:** Factura de venta creada (estado Draft o Validated).
- **Tenant scope:** Sí
- **EventVersion:** 1
- **Payload clave:** InvoiceId, SubscriberId, CustomerId, TotalAmount, TaxAmount, DocumentDate
- **Consumers futuros:**
  - Analytics: revenue diario por empresa
  - AI: predicción de impago basada en historial de cliente
  - Automation: trigger de flujo de cobro

---

### `PaymentReceivedEvent` 🟡 Planificado
- **Módulo:** Sales / Cash
- **AggregateRoot:** SalesDocument / BankTransaction
- **Descripción:** Pago de cliente registrado y conciliado.
- **Tenant scope:** Sí
- **EventVersion:** 1
- **Payload clave:** PaymentId, InvoiceId, CustomerId, Amount, PaymentMethod, PaidAt
- **Consumers futuros:**
  - Analytics: flujo de caja por empresa
  - AI: actualización de scoring de cliente
  - Automation: cierre automático de factura

---

## 📦 Inventario

### `StockAdjustedEvent` 🟡 Planificado
- **Módulo:** Inventory
- **AggregateRoot:** StockAdjustment
- **Descripción:** Ajuste de inventario aprobado. Modifica saldo disponible en bodega.
- **Tenant scope:** Sí
- **EventVersion:** 1
- **Payload clave:** AdjustmentId, WarehouseId, SubscriberId, Lines[ProductId, Quantity, Reason]
- **Consumers futuros:**
  - Analytics: variaciones de inventario por período
  - AI: detección de anomalías en ajustes

---

### `StockTransferCompletedEvent` ✅ Activo (parcial)
- **Módulo:** Inventory
- **AggregateRoot:** StockTransfer
- **Descripción:** Transferencia entre bodegas completada. Stock movido de origen a destino.
- **Tenant scope:** Sí
- **EventVersion:** 1
- **Payload clave:** TransferId, FromWarehouseId, ToWarehouseId, Lines[ProductId, Quantity]
- **Consumers futuros:**
  - Analytics: movimientos inter-bodega

---

### `StockBelowThresholdEvent` ⏳ Futuro
- **Módulo:** Inventory
- **AggregateRoot:** CurrentStock
- **Descripción:** Stock de un producto cayó por debajo del mínimo configurado.
- **Tenant scope:** Sí
- **EventVersion:** 1
- **Payload clave:** ProductId, WarehouseId, CurrentQty, MinimumQty, SubscriberId
- **Consumers futuros:**
  - AI: recomendación automática de OC
  - Automation: alerta al responsable de bodega
  - Analytics: frecuencia de roturas de stock

---

## 🛒 Compras

### `PurchaseOrderApprovedEvent` 🟡 Planificado
- **Módulo:** Purchasing
- **AggregateRoot:** PurchaseOrder
- **Descripción:** Orden de compra aprobada y lista para recepción de mercadería.
- **Tenant scope:** Sí
- **EventVersion:** 1
- **Payload clave:** OrderId, SupplierId, TotalAmount, ApprovedBy, ApprovedAt, Lines[ProductId, Qty, UnitPrice]
- **Consumers futuros:**
  - Analytics: compras por proveedor y período
  - AI: optimización de proveedores
  - Automation: notificación a proveedor (Fase 6)

---

## 📒 Contabilidad

### `JournalEntryCreatedEvent` ✅ Activo
- **Módulo:** Accounting
- **AggregateRoot:** JournalEntry
- **Descripción:** Asiento contable creado (manual o automático).
- **Tenant scope:** Sí (SubscriberId)
- **EventVersion:** 1
- **Payload clave:** JournalEntryId, SubscriberId
- **Consumers futuros:**
  - Analytics: balance de comprobación en tiempo real
  - AI: detección de asientos inusuales

---

## 🏢 SaaS / Multi-tenant

### `TenantCreatedEvent` 🟡 Planificado
- **Módulo:** Platform / SaaS
- **AggregateRoot:** Subscriber
- **Descripción:** Nuevo tenant creado en la plataforma SaaS. Dispara onboarding automático.
- **Tenant scope:** No (es un evento de plataforma)
- **EventVersion:** 1
- **Payload clave:** SubscriberId, PlanCode, CreatedBy, CreatedAt
- **Consumers futuros:**
  - Analytics: growth de la plataforma
  - AI: predicción de churn temprano
  - Automation: welcome email, onboarding checklist

---

### `SubscriptionPlanChangedEvent` ⏳ Futuro
- **Módulo:** Platform / Billing
- **AggregateRoot:** SubscriberSubscription
- **Descripción:** Tenant cambió de plan (upgrade o downgrade).
- **Tenant scope:** No (plataforma)
- **EventVersion:** 1
- **Payload clave:** SubscriberId, OldPlanId, NewPlanId, ChangedAt, ChangedBy
- **Consumers futuros:**
  - Analytics: MRR expansion / contraction
  - AI: predicción de churn post-downgrade
  - Automation: invalidación de caché entitlements ← **alta prioridad**

---

## Cómo agregar nuevos eventos

1. Crear en `ERP.Domain/Modules/{Modulo}/Events/{NombreEvent}.cs`
2. Extender `BaseDomainEvent` con `required` + `init` properties
3. Agregar entrada en este archivo con todos los campos del template
4. Emitir desde el AggregateRoot con `RaiseDomainEvent(...)`
5. El `OutboxMessage` se persiste automáticamente vía `ErpDbContext`

Template mínimo para nueva entrada:

```markdown
### `NombreDelEventoEvent` 🟡 Planificado
- **Módulo:** {modulo}
- **AggregateRoot:** {clase}
- **Descripción:** {qué ocurrió en el negocio}
- **Tenant scope:** Sí / No
- **EventVersion:** 1
- **Payload clave:** {propiedades principales}
- **Consumers futuros:** {quién reaccionará}
```
