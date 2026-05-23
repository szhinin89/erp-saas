# BusinessPartner Readiness Report

**Fecha:** 2026-05-22  
**Versión:** 1.0 — post normalización arquitectónica (Fases 1-7)

---

## Resumen ejecutivo

El sistema completó 8 commits de normalización arquitectónica controlada
antes de introducir el MasterData BC. El foundation está en producción.

---

## Checklist de prerequisitos

### ✅ Scopes

| Check | Estado |
|-------|--------|
| IOperationalContext centraliza SubscriberId + CompanyId + UserId + Role | ✅ |
| Query filters fail-closed (guard Guid.Empty en subscriber scope) | ✅ |
| CompanyScopeBehavior cubre Purchasing, Sales, Inventory | ✅ |
| IRequiresCompanyContext disponible para handlers fuera del prefix-list | ✅ |

### ✅ JWT

| Check | Estado |
|-------|--------|
| Session token emite `subscriber_id` | ✅ |
| Session token emite `company_id` cuando hay empresa activa | ✅ |
| `token_type` diferencia bootstrap de session | ✅ |
| DefaultPolicy = Session — [Authorize] sin policy requiere session token | ✅ |

### ✅ Filtros EF

| Check | Estado |
|-------|--------|
| ISubscriberScopedEntity — fail-closed | ✅ |
| ICompanyOperationalEntity — fail-closed en subscriber, opcional en company | ✅ |
| IPlatformQueryAccessor.Unfiltered() — solo infraestructura con razón documentada | ✅ |

### ✅ Ownership

| Check | Estado |
|-------|--------|
| Subscriber = SaaS account — metadatos de plataforma | ✅ |
| Company = entidad legal operativa con RUC, SRI, fiscal | ✅ |
| CompanyUserMembership = fuente canónica de autorización | ✅ |
| IMembershipAuthority disponible | ✅ |

### ✅ Nomenclatura

| Check | Estado |
|-------|--------|
| Naming Tenant → Subscriber 100% en código ejecutable | ✅ |
| Namespaces ERP.Domain.Subscribers.* coherentes con carpetas | ✅ |
| No hay clases de autorización con "Tenant" en nombre | ✅ |

---

## Gaps conocidos (no bloqueantes para BP-1)

| Gap | Impacto | Fase resolución |
|-----|---------|-----------------|
| Supplier sin ICompanyOperationalEntity | Intencional — Opción A | No aplica |
| 8 FIXME(phase5-db) en Subscriber | DB migration futura | BP-8 |
| Customer.CompanyId coexiste con BP subscriber-scoped | Período de migración controlado | BP-6 |

---

## Estado de implementación MasterData BC

```
ERP.Domain/MasterData/
  ✅ Entities/BusinessPartner.cs
  ✅ Entities/CustomerProfile.cs
  ✅ Entities/SupplierProfile.cs
  ✅ Entities/CompanyBusinessPartnerSettings.cs
  ✅ ValueObjects/TaxIdentification.cs
  ✅ Interfaces/IBusinessPartnerRepository.cs
  ✅ Interfaces/ICustomerProfileRepository.cs
  ✅ Interfaces/ISupplierProfileRepository.cs

ERP.Application/MasterData/
  ⬜ Commands/CreateBusinessPartner (BP-2)
  ⬜ Commands/CreateCustomerProfile (BP-3)
  ⬜ Commands/CreateSupplierProfile (BP-4)
  ⬜ Queries/SearchBusinessPartners (BP-2)

ERP.Infrastructure/MasterData/
  ⬜ Repositories/BusinessPartnerRepository (BP-2)
  ⬜ EF Configuration (BP-2)
  ⬜ DB Migration (BP-2)

ERP.API/
  ⬜ BusinessPartnersController (BP-2)
```

---

## Próxima acción recomendada (BP-2)

1. `BusinessPartnerConfiguration.cs` — EF mapping de `master_business_partners`
2. `DbSet<BusinessPartner>` en `ErpDbContext`
3. Migración DB: `create table master_business_partners`
4. `BusinessPartnerRepository` implementando `IBusinessPartnerRepository`
5. `CreateBusinessPartnerCommandHandler`
6. `BusinessPartnersController` con CRUD básico
