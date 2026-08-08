# ADR-ERP-001 — ERP Core Independence

**Status:** Accepted  
**Date:** 2026-06-06  
**Author:** Sebastian Zhinin  
**Context:** FASE 5 — ERP Core Purification

---

## Contexto

El sistema ERP comenzó con conceptos de SaaS mezclados en el núcleo: `CommercialPlan`, `SubscriberSubscription`, `PlatformFeature`, `SaasBilling`, `PlatformOperator`, `BillingAdmin`. Esta mezcla impedía usar el ERP en modo on-premise, white-label o single-tenant sin arrastrar una plataforma comercial completa.

---

## Decisión

**El ERP es el producto. La plataforma SaaS será un bounded context separado e independiente.**

### Reglas permanentes

| Regla | Descripción |
|-------|-------------|
| **ERP nunca conoce Platform** | `ERP.*` no puede importar nada de `ZH.Platform.*` |
| **Platform puede conocer ERP** | `ZH.Platform → ERP` es la única dirección permitida |
| **Tenant ≠ Suscripción** | `Tenant` es una unidad organizacional del ERP, no un contrato comercial |
| **Sin límites en ERP** | El ERP funciona sin restricciones; los límites son responsabilidad de Platform |

### Qué se eliminó en FASE 5

- `SaasBilling` de `Permissions.cs`
- `IsPlatformAdmin` de `ISessionContext` e `IOperationalContext`
- `IsPlatformOnlyFeature` de `AppFeature` (entidad + DB column `is_platform_only_feature`)
- `PlatformFeatureId` de `UiNavItem` (entidad + DB column)
- `PlatformAuthorizationRoles` (roles `PlatformOperator`, `BillingAdmin`, `Support`, `Auditor`)
- Métodos stub de plataforma de `IAccessRepository` (`GetPlatformOperatorByEmailAsync`, etc.)
- Constantes `TypePlatform` / `TypePlatformOperator` de `RefreshToken`
- Constantes `KindPlatform` / `KindPlatformOperator` de `PasswordResetToken`
- `RefreshUserType.Platform` / `IsPlatformRefreshUserType()`
- Nombre de servicio `ERP.SaaS` → `ERP`
- Rate limiter `per-subscriber` → `per-tenant`

### Punto de extensión para capacidades futuras

```csharp
// ERP.Application.Common
public interface ITenantCapabilities
{
    bool CanCreateUser(Guid tenantId);
    bool CanCreateCompany(Guid tenantId);
    bool CanUseFeature(Guid tenantId, string featureCode);
}

// Implementación ERP pure (sin restricciones)
public sealed class UnlimitedTenantCapabilities : ITenantCapabilities
{
    public bool CanCreateUser(Guid tenantId) => true;
    public bool CanCreateCompany(Guid tenantId) => true;
    public bool CanUseFeature(Guid tenantId, string featureCode) => true;
}
```

`ZH.Platform` puede registrar su propia implementación de `ITenantCapabilities` vía override de DI para imponer límites de plan sin modificar el dominio ERP.

---

## Arquitectura objetivo

```
ERP (este repositorio)
├── ERP.Domain          ← sin conceptos SaaS
├── ERP.Application     ← ITenantCapabilities (extensión)
├── ERP.Infrastructure
├── ERP.API
└── ERP.Web

FUTURO (repositorio separado)
ZH.Platform
├── Platform.Domain
├── Platform.Application
├── Platform.Infrastructure
├── Platform.API
└── Platform.AdminPortal
```

---

## Consecuencias

### Positivas
- ERP puede ejecutarse **on-premise**, **Docker**, **cloud privado** o **single-tenant** sin depender de infraestructura SaaS.
- El dominio principal puede crecer (Inventario, Ventas, Compras, Contabilidad, RRHH, CRM) sin reintroducir lógica comercial.
- White Label: un operador puede reselling ERP con su propia capa Platform sin alterar el ERP.
- El esquema de BD es operativo puro: sin `subscriptions`, `billing_cycles`, `tenant_quotas`.

### Restricciones
- Toda monetización, gestión de planes y límites de tenants **vive fuera del ERP**.
- Cambios en `Tenant` que agreguen campos SaaS (`PlanCode`, `TrialEndsAt`, etc.) requieren **Architecture Review** y son bloqueantes en PR.

---

## Invariantes que no deben romperse

1. `Tenant` solo tiene: `Id`, `Name`, `Slug`, `IsActive`, `PreferredLanguage`.
2. `ITenantCapabilities` es el único punto de entrada para restricciones de capacidad.
3. Ningún namespace `ERP.*` puede importar `ZH.Platform.*` o equivalentes.
4. `IAccessRepository` no tiene métodos `Platform*` ni `PlatformOperator*`.
5. Los únicos tipos de token válidos son `Identity` y `Legacy` (backward compat).

---

## Referencias

- [FASE 4 — SubscriberId→TenantId consolidation](../../STATUS.md)
- [ITenantCapabilities](../../../backend/src/ERP.Application/Common/ITenantCapabilities.cs)
- [UnlimitedTenantCapabilities](../../../backend/src/ERP.Application/Common/UnlimitedTenantCapabilities.cs)
- [Tenant entity](../../../backend/src/ERP.Domain/Modules/Tenants/Entities/Tenant.cs)
