# ADR-001: BusinessPartner como entidad subscriber-scoped

**Estado:** Aceptado  
**Fecha:** 2026-05-22  
**Autores:** Sebastian Zhinin, Claude Sonnet 4.6  
**Contexto:** ERP SaaS multiempresa — Normalización Arquitectónica Fase BP-1

---

## Contexto

El sistema tiene entidades legacy `Customer` (Sales BC) y `Supplier` (Purchasing BC) que mezclan:
- Identidad fiscal (RUC, LegalName) — pertenece al Subscriber
- Condiciones comerciales (CreditLimit, PaymentDays) — varían por Company

En un modelo multiempresa, el mismo RUC puede operar como cliente en Company A y Company B
del mismo Subscriber. Sin un modelo compartido, el RUC se duplica en cada empresa.

---

## Decisión

**BusinessPartner es subscriber-scoped (`ISubscriberScopedEntity`). NO tiene `CompanyId`.**

```
Subscriber
└── BusinessPartner (identidad fiscal única por RUC/CI)
    ├── CustomerProfile (rol cliente)
    └── SupplierProfile (rol proveedor)

Company A ──┐
Company B ──┤──► CompanyBusinessPartnerSettings (condiciones por empresa)
Company C ──┘
```

### Sobre Supplier — Opción A (elegida)

**Supplier permanece subscriber-scoped.** No se convierte a `ICompanyOperationalEntity`.

Alternativa rechazada (Opción B):
```csharp
// RECHAZADO — recrea el problema histórico
public sealed class Supplier : MasterEntity, ISubscriberScopedEntity, ICompanyOperationalEntity
```

Razón del rechazo: si Supplier fuera company-scoped, el mismo proveedor (mismo RUC)
debería duplicarse en cada Company — exactamente el problema que BusinessPartner resuelve.

---

## Consecuencias positivas

1. Un RUC existe UNA sola vez por Subscriber — no N veces (una por empresa)
2. Las condiciones comerciales pueden diferir por empresa sin duplicar la identidad
3. `CustomerProfile` y `SupplierProfile` son roles del mismo BP — un BP puede ser ambos
4. Los query filters de EF trabajan correctamente con `ISubscriberScopedEntity`
5. Las transacciones (SalesInvoice, PurchaseOrder) siguen siendo `ICompanyOperationalEntity`

## Consecuencias negativas / deuda conocida

1. Los campos fiscales de `Customer` y `Supplier` (legacy) quedan duplicados respecto a BP
   → Resolver en fases BP-6 y BP-7 (migración de datos)
2. `Customer.CompanyId` (ICompanyOperationalEntity) y `BusinessPartner` (ISubscriberScopedEntity)
   coexisten durante la migración → período de inconsistencia controlado
3. `FIXME(phase5-db)` en `Subscriber.Ruc` etc. siguen pendientes de migración DB

---

## Alternativas consideradas

### Alternativa A: BusinessPartner company-scoped
Rechazada. Recrea el problema de duplicación por empresa.

### Alternativa B: Mantener solo Customer/Supplier sin BusinessPartner
Rechazada. Imposible sin duplicación al escalar a multiempresa.

### Alternativa C: Fusionar Customer y Supplier en una sola tabla
Posible como optimización futura, pero requiere migración masiva. No prioritario.

---

## Scope definitivo

| Entidad | Interface | Justificación |
|---------|-----------|---------------|
| BusinessPartner | ISubscriberScopedEntity | Identidad fiscal única por subscriber |
| CustomerProfile | ISubscriberScopedEntity | Rol — compartido entre empresas |
| SupplierProfile | ISubscriberScopedEntity | Rol — compartido entre empresas |
| CompanyBusinessPartnerSettings | ICompanyScopedEntity | Condiciones comerciales por empresa |
| SalesInvoice | ICompanyOperationalEntity | Transacción de empresa |
| PurchaseOrder | ICompanyOperationalEntity | Transacción de empresa |
| Warehouse | ICompanyOperationalEntity | Recurso de empresa |

---

## Constraint de unicidad (futuro DB)

```sql
-- Una sola identidad fiscal por subscriber
CREATE UNIQUE INDEX uq_bp_identification
  ON master_business_partners (subscriber_id, identification_type, identification_number)
  WHERE is_active = true;
```

---

## Roadmap de implementación

| Fase | Descripción | Estado |
|------|-------------|--------|
| BP-1 | Foundation: entidades, VOs, interfaces | ✅ Completado |
| BP-2 | API CRUD BusinessPartner | Pendiente |
| BP-3 | CreateCustomer → dual-write BusinessPartner | Pendiente |
| BP-4 | CreateSupplier → vincula BusinessPartner | Pendiente |
| BP-5 | CompanyBusinessPartnerSettings CRUD | Pendiente |
| BP-6 | Migración datos Customer → BusinessPartner | Pendiente |
| BP-7 | Migración datos Supplier → BusinessPartner | Pendiente |
| BP-8 | Deprecación campos fiscales en Customer/Supplier | Pendiente |
