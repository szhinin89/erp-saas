# ADR-001: BusinessPartner como entidad subscriber-scoped

**Estado:** ✅ FROZEN — Módulo cerrado definitivamente  
**Fecha aprobación:** 2026-05-22  
**Fecha de cierre:** 2026-06-02  
**Autores:** Sebastian Zhinin, Claude Sonnet 4.6  
**Contexto:** ERP SaaS multiempresa — Normalización Arquitectónica Fase BP-1  

> **FROZEN:** La arquitectura, el modelo de datos y el comportamiento funcional de este
> módulo están cerrados. No se aceptan cambios estructurales sin una nueva ADR aprobada.
> Ver sección "Restricciones definitivas" al final de este documento.

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

## Constraint de unicidad — IMPLEMENTADO

```sql
-- Migración: AddBPUniqueIdentificationIndex (2026-06-02)
CREATE UNIQUE INDEX uq_mbp_subscriber_identification
  ON master_business_partners (subscriber_id, identification_type, identification_number)
  WHERE is_active = true;
```

---

## Roadmap de implementación — COMPLETADO

| Fase | Descripción | Estado |
|------|-------------|--------|
| BP-1 | Foundation: entidades, VOs, interfaces | ✅ Completado 2026-05-22 |
| BP-2 | API CRUD BusinessPartner | ✅ Completado 2026-05-22 |
| BP-3 | Customer Profiles (rol cliente) | ✅ Completado 2026-05-22 |
| BP-4 | Supplier Profiles (rol proveedor) | ✅ Completado 2026-05-22 |
| BP-5 | CompanyBusinessPartnerSettings CRUD | ✅ Completado 2026-05-22 |
| BP-6 | LegalRepresentativeName (Persona Jurídica) | ✅ Completado 2026-06-02 |
| BP-7 | Frontend wizard + formulario completo | ✅ Completado 2026-06-02 |
| BP-8 | Unique index en DB | ✅ Completado 2026-06-02 |

---

## Estado final del modelo de datos

### master_business_partners
| Columna | Tipo | Notas |
|---------|------|-------|
| id | uuid PK | |
| subscriber_id | uuid FK | Tenant scope |
| identification_type | varchar(20) | RUC / CI / PASSPORT / OTHER |
| identification_number | varchar(32) | |
| legal_name | varchar(200) | Razón social o nombre completo |
| trade_name | varchar(200) nullable | Solo Persona Jurídica |
| legal_representative_name | varchar(300) nullable | Solo Persona Jurídica / RUC |
| email | varchar(120) nullable | |
| phone | varchar(40) nullable | |
| country_code | char(3) nullable | ISO-3 |
| is_active | bool | |
| created_at, updated_at, created_by, updated_by | audit | |

### Índices en master_business_partners
| Índice | Tipo | Columnas |
|--------|------|----------|
| PK_master_business_partners | UNIQUE | id |
| ix_mbp_subscriber | INDEX | subscriber_id |
| uq_mbp_subscriber_identification | UNIQUE PARTIAL (WHERE is_active) | subscriber_id, identification_type, identification_number |

---

## Backlog documentado (NO bloqueante para FROZEN)

| ID | Descripción | Estado |
|----|-------------|--------|
| BP-002 | Validación ecuatoriana de formato por tipo (RUC=13 dígitos, CI=10 dígitos, CONSUMER_FINAL) | Pendiente — no bloquea |
| BP-003 | Migración de validaciones de dominio hacia FluentValidation (consistencia con el resto del proyecto) | Pendiente — no bloquea |

---

## Restricciones definitivas

Las siguientes acciones están **PROHIBIDAS** sin una nueva ADR aprobada:

1. ❌ Eliminar `subscriber_id` de `master_business_partners`
2. ❌ Convertir Business Partners en catálogo global (sin subscriber scope)
3. ❌ Compartir clientes entre tenants (Subscribers)
4. ❌ Compartir proveedores entre tenants (Subscribers)
5. ❌ Crear sincronización automática de BPs entre tenants
6. ❌ Rediseñar hacia modelos `Person` / `Company` / `LegalEntity` / `NaturalPerson`
7. ❌ Mover `LegalRepresentativeName` a una tabla separada
8. ❌ Agregar `CompanyId` directo a `master_business_partners`
