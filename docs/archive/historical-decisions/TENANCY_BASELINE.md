# TENANCY BASELINE — Fotografía del estado actual (Tenant → Company → Membership)

> ## ⚠️ HISTÓRICO
>
> Este documento representa una decisión, auditoría o estado anterior del proyecto.
>
> **NO representa la arquitectura actual del ERP.**
>
> La fuente de verdad actual es:
> - [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md)
> - [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md)
> - El código fuente actual (`frontend/src`, `backend/src`)

---

> **FASE 0** del Plan de Remediación Controlada por Fases (tenancy/autorización).
> Fecha: 2026-06-08 · Branch: `feat/platform-kernel-refactor`
> **Este documento es de solo lectura: no se modificó código fuente para producirlo.**
> Toda afirmación cita archivo + líneas verificadas por lectura directa.

---

## 1. Alcance auditado

| Componente | Archivo principal | Líneas |
|---|---|---|
| `RuntimePermissionAuthorizer` | `backend/src/ERP.Application/Access/Authorization/RuntimePermissionAuthorizer.cs` | 1-55 |
| `Tenant` | `backend/src/ERP.Domain/Modules/Tenants/Entities/Tenant.cs` | 1-51 |
| `Company` | `backend/src/ERP.Domain/Modules/Company/Entities/Company.cs` | 1-207 |
| `CompanyUserMembership` | `backend/src/ERP.Domain/Modules/Access/Entities/Membership.cs` | 1-60 |
| `IdentityUser` | `backend/src/ERP.Domain/Modules/Access/Entities/IdentityUser.cs` | 1-86 |
| `CompanyProvisioningService` | `backend/src/ERP.Infrastructure/Services/CompanyProvisioningService.cs` | 1-127 |
| `Subscriber` | `backend/src/ERP.Domain/Modules/Subscribers/**` | (carpeta sin archivos `.cs`) |

---

## 2. Archivos involucrados (mapa de dependencias)

```
RuntimePermissionAuthorizer (ERP.Application/Access/Authorization)
 ├─ ICurrentTenant ─────────────► CurrentTenantService (ERP.Infrastructure/Services)
 ├─ ITenantRepository ──────────► TenantRepository (resuelve Tenant por Id)
 ├─ ICompanyContextProvider ────► resuelve membresía operativa activa del usuario
 └─ IEffectivePermissionKeysProvider ► claves de permiso efectivas (perfil + empresa)

CreateInitialAdminHandler (ERP.Application/Setup/CreateInitialAdmin)
 ├─ Tenant.Create(...)                         [Tenant.cs:14]
 ├─ IdentityUser.Create(...)                   [IdentityUser.cs:30]
 ├─ ICompanyProvisioningService.EnsureDefaultCompanyAsync(tenant)
 │     └─ CompanyProvisioningService.cs:23 → CreateDefaultCompanyForTenantAsync (línea 34) → Company.CreateManaged
 └─ CompanyUserMembership.Create(companyId, userId, role:"Admin", ...) [Membership.cs:22]

CreateCompanyHandler (ERP.Application/Modules/Companies/UseCases/CreateCompany)
 ├─ ICompanyAccessGuard.RequireActiveTenantAsync()
 ├─ ICompanyProvisioningService.CreateManagedCompanyAsync(...)        [CompanyProvisioningService.cs:57]
 │     ├─ Company.CreateManaged(...)
 │     └─ CompanyUserMembership.Create(..., creatorRole, ...)         [línea 89-90]
 └─ ICompanyBootstrapService (sucursal + almacén principal)

CompanyAccessGuard (ERP.Infrastructure/Services)
 ├─ ICurrentUser / ICurrentTenant / ICurrentCompany
 ├─ IAccessRepository.GetCompanyUserMembershipAsync(...)
 └─ ITenantRepository _subscribers   ⚠️ campo nombrado "_subscribers" pero tipado ITenantRepository

EnterpriseQueryFilterConfigurator (ERP.Infrastructure/Persistence)
 └─ aplica HasQueryFilter sobre ITenantScopedEntity / ICompanyOperationalEntity
       (Company implementa ISubscriberScopedEntity → alias de ITenantScopedEntity)
```

---

## 3. Flujo actual de creación de Tenant/Company (paso a paso, con evidencia)

1. **Primer arranque (sin sistema inicializado)**
   `POST /api/setup/admin` → `SetupController.cs` (`[AllowAnonymous]`) → `CreateInitialAdminHandler`:
   - Crea `Tenant.Create("Principal", "principal", bootstrapId)` — sin validar unicidad de slug a nivel de aplicación (delegado a índice único en BD, `TenantConfiguration.cs`)
   - Crea `IdentityUser.Create(...)` con `TenantId = null` (línea 48, marcado `[Obsolete]`)
   - Llama `ICompanyProvisioningService.EnsureDefaultCompanyAsync(tenant)` → genera RUC provisional (`ResolveTaxIdAsync`, `CompanyProvisioningService.cs:97-114`) → `Company.CreateManaged(...)`
   - Crea `CompanyUserMembership.Create(company.Id, user.Id, role: "Admin", ...)`
   - Invalida el token de setup (one-shot)

2. **Creación posterior de empresas (operación normal)**
   `POST /api/companies` → `CompaniesController.cs` (`[Authorize(Policy = CompanyPermissions.PolicyCreate)]` = `perm:erp.companies.create`) → `CreateCompanyHandler`:
   - `ICompanyAccessGuard.RequireActiveTenantAsync()` valida que `_currentTenant.TenantId != Guid.Empty` y que el `Tenant` existe y está activo (mensajes "suscriptor" — ver §5)
   - `ICompanyProvisioningService.CreateManagedCompanyAsync(...)` → valida unicidad global de RUC (`EnsureTaxIdAvailableGloballyAsync`) → `Company.CreateManaged` → `CompanyUserMembership.Create(..., creatorRole, ...)`
   - `ICompanyBootstrapService` crea sucursal/almacén principal

3. **Resolución de la policy `perm:erp.companies.create`**
   `PermissionPolicyProvider` reconoce el prefijo `perm:` → `PermissionHandler` → `IRuntimePermissionAuthorizer.IsAuthorizedAsync(permissionKey, userId, role, ...)` → implementado por `RuntimePermissionAuthorizer`:
   ```csharp
   // RuntimePermissionAuthorizer.cs:37-38
   if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
       return true;
   ```
   El parámetro `role` proviene del claim `company_role` del JWT de sesión (ver `AccessTokenService.cs:38-42`), es decir, **el rol operativo dentro de `CompanyUserMembership`**, no un rol de plataforma.

4. **Switch de empresa**
   `POST /api/auth/switch-company` (`AuthController.cs:131`) re-emite JWT con nuevos claims `company_id`/`company_role` tras validar membresía.

---

## 4. Riesgos detectados (sin clasificar todavía — eso ocurre en Fase 1 para autorización y Fase 3 para Subscriber)

### 4.1 Autorización (foco de Fase 1)
- `RuntimePermissionAuthorizer.cs:37-38` otorga `true` universal a cualquier `role == "Admin"`, incluyendo la policy `perm:erp.companies.create`. **Esto debe confirmarse y resolverse formalmente en Fase 1/2** — aquí solo se documenta su existencia textual.
- No existe ningún concepto de **"SuperAdmin"** o **"PlatformOperator"** en el código auditado (`grep` sin resultados en `ERP.Application/Modules/Companies`); el único "Admin" presente es el rol de `CompanyUserMembership`.

### 4.2 Naming drift Tenant ↔ Subscriber (foco de Fase 3)
- `CompanyAccessGuard.cs`: campo `ITenantRepository _subscribers` (línea 19) y mensajes de error `"Contexto de suscriptor no establecido"`, `"Suscriptor no válido o inactivo"`, `"Empresa no encontrada o no pertenece al suscriptor activo"`.
- `ISubscriberScopedEntity` (`ERP.Domain/Common/ISubscriberScopedEntity.cs`) es un alias declarado *"backward-compat"* de `ITenantScopedEntity`, e implementado por **~50 entidades de dominio** (`Company`, `BusinessPartner*`, `Item*`, `Inventory*`, `Product*`, etc. — lista completa relevada vía grep, ver §6).
- La carpeta `ERP.Domain/Modules/Subscribers/{Entities,Events,Exceptions,Interfaces}` existe pero **no contiene ningún archivo `.cs`** — estructura vacía/vestigial.
- `SubscriberId` como identificador solo aparece en `ERP.Architecture.Tests/TenantIsolationInvariantTests.cs` (1 archivo).

### 4.3 `IdentityUser.TenantId` obsoleto (foco de Fase 5)
- Marcado `[Obsolete("Use CompanyUserMembership chain instead...")]` (`IdentityUser.cs:23`), siempre seteado a `null` en `Create` (línea 48, con `#pragma warning disable CS0618`). Doc-comment explícito: *"No usar en guards, policies ni handlers de autorización nuevos"* (línea 21).

### 4.4 Provisioning duplicado (foco de Fase 7)
- `CompanyProvisioningService` expone tres métodos con responsabilidades solapadas: `EnsureDefaultCompanyAsync` (línea 23), `CreateDefaultCompanyForTenantAsync` (línea 34, *público* pero solo invocado internamente desde `EnsureDefaultCompanyAsync`), y `CreateManagedCompanyAsync` (línea 57). Pendiente determinar si `CreateDefaultCompanyForTenantAsync` tiene callers externos.

---

## 5. Observaciones textuales relevantes (evidencia cruda)

```csharp
// CompanyAccessGuard.cs:19  — naming drift
private readonly ITenantRepository _subscribers;

// CompanyAccessGuard.cs:47
return Result<Guid>.Failure("Contexto de suscriptor no establecido.");

// CompanyAccessGuard.cs:51
return Result<Guid>.Failure("Suscriptor no válido o inactivo.");

// CompanyAccessGuard.cs:73
return Result<CompanyAccessContext>.Failure("Empresa no encontrada o no pertenece al suscriptor activo.");

// ISubscriberScopedEntity.cs
/// <summary>Alias backward-compat — implementaciones heredan TenantId de ITenantScopedEntity.</summary>
public interface ISubscriberScopedEntity : ITenantScopedEntity { }

// IdentityUser.cs:17-24
/// Legado: asociación directa usuario→subscriber para usuarios single-tenant.
/// En arquitectura multi-empresa la relación canónica es:
/// IdentityUser → CompanyUserMembership → Company → Subscriber.
/// No usar en guards, policies ni handlers de autorización nuevos.
[Obsolete("Use CompanyUserMembership chain instead. Kept for backward compat with single-tenant user records.")]
public Guid? TenantId { get; private set; }
```

---

## 6. Inventario crudo — referencias a "Subscriber" (preparación para Fase 3)

Conteo de archivos con coincidencia (no implica uso productivo — clasificación detallada en Fase 3):

| Patrón | # archivos | Archivos |
|---|---|---|
| `\bSubscriber\b` (identificador) | 12 | `BusinessPartnerApiContracts.cs`, `ICompanyBootstrapService.cs`, `BpContactUseCases.cs`, `BpLocationUseCases.cs`, `CreateBusinessPartnerCommand.cs`, `AuthDto.cs`, `TenantIsolationInvariantTests.cs`, `BaseDomainEvent.cs`, `IdentityUser.cs`, `IdentityUserType.cs`, `Configurations/README.md.cs`, `OutboxMessage.cs` |
| `ISubscriberScopedEntity` (implementado/referenciado) | ~52 | Mayoritariamente entidades de dominio (`Company`, `BusinessPartner*`, `Item*`, `Inventory*`, `Product*`, `RefreshToken`, interceptores y configuraciones EF — lista completa disponible en log de auditoría) |
| `SubscriberId` | 1 | `ERP.Architecture.Tests/TenantIsolationInvariantTests.cs` |

---

## 7. Conclusión de la fase

- **No se modificó ningún archivo de código fuente.**
- El flujo `Tenant → Company → Membership` está mapeado de extremo a extremo con evidencia citada.
- Quedan confirmados, **a nivel textual**, los 4 puntos de riesgo que las fases siguientes deben resolver en orden: (1) bypass de autorización `role == "Admin"`, (2) drift Tenant/Subscriber, (3) `IdentityUser.TenantId` obsoleto, (4) posible duplicación en `CompanyProvisioningService`.

---

## BASELINE APROBADO — pendiente de confirmación del usuario
