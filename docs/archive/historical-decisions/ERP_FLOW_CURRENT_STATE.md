# ERP_FLOW_CURRENT_STATE — Diagnóstico del flujo real vs flujo objetivo

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

> **FASE 0 — DIAGNÓSTICO CONTROLADO** del plan "Consolidación ERP Puro".
> Fecha: 2026-06-08 · Branch: `feat/platform-kernel-refactor`
> **Documento de solo lectura — no se modificó código fuente.**
> Construido sobre evidencia ya verificada en [`TENANCY_BASELINE.md`](TENANCY_BASELINE.md) y [`AUTHORIZATION_AUDIT.md`](AUTHORIZATION_AUDIT.md) (mismas citas archivo+línea, sin reinterpretación).

---

## 1. Flujo objetivo solicitado (referencia)

```
SuperAdmin (solo sistema)
   ↓
Crear Empresa (Tenant + Company)
   ↓
Crear Admin de Empresa
   ↓
Login Admin
   ↓
ERP Operativo
```

---

## 2. Flujo real actual (evidencia)

### 2.1 Auth / JWT
- Emisor único: `AccessTokenService.GenerateToken` (`backend/src/ERP.Infrastructure/Services/AccessTokenService.cs:47-75`)
- Claims emitidos en **todo** token de sesión: `sub`, `email`, `jti`, `tenant_id`, `full_name`, `role` (= `ClaimTypes.Role`), `token_type` (líneas 56-65)
- Claims **condicionales** (solo si hay empresa activa): `company_id`, `company_role` (líneas 38-42)
- El valor de `role` (claim único de rol) es, en los tres emisores de sesión confirmados:
  | Emisor | Valor de `role` |
  |---|---|
  | `LoginHandler.cs:104-105` (con empresa) | `membership.Role` |
  | `LoginHandler.cs:80-82` (sin empresa, pendiente selección) | constante `"User"` |
  | `SwitchCompanyHandler.cs:60` | `membership.Role` de la empresa destino |

  **Es decir: hoy NO existe un claim de rol "de plataforma" separado del rol operativo de empresa — un único claim `role` sirve para ambos propósitos.**

### 2.2 Roles (SuperAdmin, Admin)
- **`SuperAdmin` no existe en ningún punto del código backend** (`grep -rln "SuperAdmin" backend/src/ERP.Application backend/src/ERP.Domain backend/src/ERP.Infrastructure backend/src/ERP.API` → 0 resultados de producción; única coincidencia es el test que **prohíbe** su existencia, ver §4.1).
- `IdentityUserType` (`ERP.Domain/Modules/Access/Enums/IdentityUserType.cs`) define: `Platform = 0`, `Tenant = 1` (*"Reservado — no implementado. No usar..."*), `Company = 2`.
- `docs/IDENTITY.md:14,28` documenta `platform_role: PlatformOperator | Support | BillingAdmin` y dice *"Bypass ERP solo operador platform global... rol JWT `PlatformOperator`"* — pero **`PlatformOperator` no aparece implementado en ningún flujo de creación de empresas ni en `RuntimePermissionAuthorizer`** (`grep` → 0 resultados en `ERP.Application/Modules/Companies`).
- `Admin` **sí existe**, pero es el **`Role` de una `CompanyUserMembership`** — un string libre asociado a una empresa concreta, no un rol de sistema/plataforma.

### 2.3 Tenant / Company
- `Tenant` (`ERP.Domain/Modules/Tenants/Entities/Tenant.cs`): aggregate root con `Name`, `Slug`, `IsActive` — sin lógica de negocio sobre creación de Companies.
- `Company` (`ERP.Domain/Modules/Company/Entities/Company.cs`): FK obligatoria `TenantId`, factories `CreateFromTenant`/`CreateManaged`.
- `CompanyUserMembership` (`ERP.Domain/Modules/Access/Entities/Membership.cs`): vínculo N:M `IdentityUser ↔ Company` con `Role` libre (string).

### 2.4 Flujo de creación de empresa (real, confirmado)
Existen **dos caminos productivos** que crean `Tenant`/`Company`:

**(A) First-run / bootstrap** — `POST /api/setup/admin` (`SetupController`, `[AllowAnonymous]`) → `CreateInitialAdminHandler`:
```
Tenant.Create("Principal", ...) 
  → IdentityUser.Create(...) 
  → ICompanyProvisioningService.EnsureDefaultCompanyAsync(tenant)
  → CompanyUserMembership.Create(..., role: "Admin")
```
Gateado por **token de setup de un solo uso** (`SetupTokenCrypto`), no por rol — es un flujo "anónimo pero token-gated".

**(B) Operación normal** — `POST /api/companies` (`CompaniesController`, `[Authorize(Policy = "perm:erp.companies.create")]`) → `CreateCompanyHandler`:
```
ICompanyAccessGuard.RequireActiveTenantAsync()   // solo valida que el Tenant esté activo
  → ICompanyProvisioningService.CreateManagedCompanyAsync(...)
  → Company.CreateManaged + CompanyUserMembership.Create(..., creatorRole: "Admin")  // fijo, línea 41
  → ICompanyBootstrapService (sucursal + almacén)
```
Resuelto por: `RuntimePermissionAuthorizer.IsAuthorizedAsync` → **`if (role == "Admin") return true`** (`RuntimePermissionAuthorizer.cs:37-38`) — bypass confirmado en Fase 1 previa.

### 2.5 Flujo de creación de usuario / admin operativo
- No existe un caso de uso `CreateAdminUser` separado de la creación de empresa: **el creador de la empresa (vía A o B) queda automáticamente como `"Admin"` de esa empresa** (`CreateInitialAdminHandler` línea ~67; `CreateCompanyHandler.cs:41` con `creatorRole: "Admin"` fijo).
- No hay un paso explícito "Crear Admin de Empresa" desacoplado de "Crear Empresa" — están fusionados en una sola transacción/handler.

### 2.6 Sistema de permisos
- Policies dinámicas `perm:<key>` (`PermissionPolicyProvider.cs`) → `PermissionRequirement` → `PermissionHandler` (thin adapter) → `IRuntimePermissionAuthorizer`.
- `RuntimePermissionAuthorizer` (única implementación, `ERP.Application/Access/Authorization/RuntimePermissionAuthorizer.cs`):
  1. Requiere `tenant_id` válido y `Tenant` existente (líneas 29-35)
  2. **Bypass total si `role == "Admin"`** (líneas 37-38)
  3. Si no, resuelve membresía operativa + perfil + claves de permiso efectivas (líneas 43-53)

---

## 3. Diferencias vs flujo objetivo

| Paso del flujo objetivo | Estado real | Brecha |
|---|---|---|
| **`SuperAdmin (solo sistema)`** | No existe. El bootstrap (`CreateInitialAdminHandler`) es **anónimo + token-gated**, no requiere ni produce un rol "SuperAdmin". El bypass de permisos lo otorga el rol de empresa `"Admin"`, no un rol de sistema | 🔴 El concepto "SuperAdmin (solo sistema)" **no tiene contraparte en el código**. Lo más cercano documentado es `PlatformOperator` (Nivel 3, `docs/IDENTITY.md`), que **no está implementado** y que, además, las reglas congeladas de Nivel 1 prohíben ubicar dentro del ERP (ver §4.1) |
| **`Crear Empresa (Tenant + Company)`** | Existe, pero por **dos caminos paralelos** (bootstrap token-gated vs `POST /api/companies` con bypass de rol `"Admin"`) — exactamente la "ruta paralela" que el flujo objetivo prohíbe | 🟠 Hay 2 entradas productivas al mismo resultado (creación de Tenant+Company), con guardas de autorización **distintas e inconsistentes entre sí** |
| **`Crear Admin de Empresa`** | No es un paso independiente: está **fusionado** dentro de la creación de empresa (`creatorRole: "Admin"` fijo) | 🟡 No hay separación de responsabilidades CQRS entre "crear empresa" y "asignar/crear su administrador" — el flujo objetivo los plantea como pasos distintos y así debería modelarse para eliminar ambigüedad |
| **`Login Admin`** | Implementado (`LoginHandler`) y emite JWT con `role = membership.Role` | ✅ Compatible — sin brecha relevante, salvo que el claim `role` es ambiguo (ver §5) |
| **`ERP Operativo`** | Implementado vía `CurrentTenantService`/`CurrentCompanyService` + query filters fail-closed | ✅ Compatible — base sólida, sin brecha |

---

## 4. Riesgos detectados

### 4.1 🔴 Choque directo entre el flujo objetivo solicitado y reglas congeladas (Nivel 1)
El flujo objetivo nombra explícitamente un rol **"SuperAdmin"**. Esto colisiona con:
- `ERP_CORE_FREEZE.md` — regla *"ERP never depends on Platform"*
- Test arquitectónico **bloqueante en CI** `PlatformControlPlaneGuardTests.cs`:
  ```csharp
  // línea 21-22
  if (Path.GetFileName(file).Contains("SuperAdmin", ...))
      violations.Add($"{rel}: legacy superadmin controller file name");
  // línea 56-58
  Directory.Exists(platformDir).Should().BeFalse(
      "ERP does not own the Platform control plane. Platform is a separate future bounded context.");
  ```
**Cualquier implementación literal de un rol/entidad/controlador llamado "SuperAdmin" dentro de `ERP.*` romperá el build de CI** y violará un acta de congelamiento ya certificada (`commit 2e51c72e`). Esto debe resolverse — a nivel de **nomenclatura**, no de intención — antes de tocar código en fases posteriores.

### 4.2 🔴 Bypass de autorización por nombre de rol de empresa (ya confirmado en Fase 1 previa)
`RuntimePermissionAuthorizer.cs:37-38` otorga acceso total a cualquier permiso (incluido `erp.companies.create`) a cualquier usuario cuyo **rol de empresa** sea `"Admin"` — sin distinguir si esa empresa es la que el usuario está intentando afectar. Esto es lo que hoy permite que "cualquier Admin de cualquier empresa" cree nuevas empresas, contradiciendo la regla objetivo *"Admin NO crea empresas"*.

### 4.3 🟠 Dos rutas paralelas de creación de empresa con guardas distintas
- Ruta A (bootstrap): gateada por token de un solo uso, anónima
- Ruta B (`/api/companies`): gateada por policy de permisos con bypass de rol

Ambas terminan en el mismo `ICompanyProvisioningService`, pero **la decisión de "quién puede crear" está duplicada y es inconsistente** — exactamente la ambigüedad que el flujo objetivo busca eliminar ("NO se permite ningún otro flujo paralelo").

### 4.4 🟡 Fusión de "crear empresa" y "crear admin"
No existe separación entre `CreateCompany` y `CreateCompanyAdmin` como casos de uso independientes — están acoplados en una sola transacción con un rol fijo (`"Admin"`). Esto dificulta modelar el flujo objetivo tal como está especificado (pasos secuenciales y auditable de forma independiente).

### 4.5 🟡 `IdentityUser.TenantId` obsoleto convive con el modelo canónico
Marcado `[Obsolete]` (`IdentityUser.cs:23`), siempre `null` — no es un riesgo de seguridad activo, pero es deuda técnica que aumenta la superficie de ambigüedad sobre "qué define el alcance de un usuario".

---

## 5. Ambigüedades de roles (resumen ejecutivo)

| Concepto | ¿Existe en código? | ¿Dónde? | Ambigüedad |
|---|---|---|---|
| `SuperAdmin` | ❌ No | — | Es el término que pide el flujo objetivo, pero **no tiene contraparte real** y su nombre literal está prohibido en el ERP por reglas de Nivel 1 |
| `PlatformOperator` | ⚠️ Solo documentado | `docs/IDENTITY.md:14,28`, `IdentityUserType.Platform` | Documentado como "el" rol con bypass de permisos, pero **no implementado** en `RuntimePermissionAuthorizer` ni en ningún flujo de creación de empresa — la documentación (Nivel 3) está desalineada con el código |
| `Admin` (rol de empresa) | ✅ Sí | `CompanyUserMembership.Role = "Admin"` | Es un **string libre por empresa**, pero el código lo trata como si fuera un **rol de sistema** (bypass total en `RuntimePermissionAuthorizer.cs:37-38`) — mezcla dos niveles de autoridad (empresa vs sistema) en un solo concepto |
| `User` | ✅ Sí | `LoginHandler.cs:80` (constante para sesión sin empresa) | Rol "placeholder" para sesiones sin empresa activa — no documentado como rol canónico en ningún lugar |
| `Manager` | ❌ No | — | Mencionado en el flujo objetivo (Fase 1 del plan), pero no existe ninguna referencia en el código actual |

**Conclusión de ambigüedad:** el sistema usa **un único claim `role`** para representar simultáneamente: (a) "rol operativo dentro de una empresa" y (b) "nivel de autoridad de sistema" (vía el bypass). Esa superposición es la raíz de todas las brechas señaladas en §3 y §4.

---

## 6. Conclusión de la fase

- **No se modificó código.**
- El flujo real difiere del flujo objetivo en **3 puntos estructurales**: (1) no existe un rol de sistema separado del rol de empresa — y el nombre que pide el objetivo ("SuperAdmin") choca con reglas congeladas de Nivel 1; (2) hay dos rutas paralelas de creación de empresa con guardas inconsistentes; (3) "crear empresa" y "crear admin" están fusionados en un único paso, no son pasos independientes como pide el objetivo.
- Estas tres brechas son precisamente las que las fases 1-3 del plan deben resolver — **pero la Fase 1 ("Definición de roles canónicos") no podrá nombrar al rol de sistema "SuperAdmin"** sin antes resolver la colisión señalada en §4.1 (cambiar el nombre o modificar el freeze — ambas son decisiones que requieren tu aprobación explícita, no pueden asumirse).

---

## ERP_FLOW_CURRENT_STATE — pendiente de aprobación
