# COMPANY_CREATION_FLOW — Flujo canónico de creación de empresa

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

> **FASE 2 — FLUJO DE EMPRESAS** del plan "Consolidación ERP Puro".
> Fecha: 2026-06-08 · Branch: `feat/platform-kernel-refactor`
> **Documento de diseño — NO se modifica código en esta fase.**
> Depende de: [`ROLES_CANONICOS.md`](ROLES_CANONICOS.md) (definición de roles aprobada en FASE 1).

---

## 1. Estado actual verificado (dos rutas paralelas)

El sistema hoy tiene **dos caminos productivos** que crean un `Tenant` + `Company`. Son mutuamente inconsistentes en sus guardas de autorización:

### Ruta A — Bootstrap (first-run)
**Endpoint:** `POST /api/setup/admin` (`SetupController.cs:31`, `[AllowAnonymous]`)
**Comando:** `CreateInitialAdminCommand` (FirstName, LastName, Email, Password, SetupToken)
**Handler:** `CreateInitialAdminHandler.cs`

Flujo verificado (citando líneas exactas):
```
1. SetupState.IsInitialized == false && token válido && no expirado   [Handler:36-50]
2. Tenant.Create("Principal", "principal", bootstrapId)               [Handler:57]
3. IdentityUser.Create(...)                                           [Handler:59-60]
4. CompanyProvisioningService.EnsureDefaultCompanyAsync(tenant)       [Handler:65]
     └─ Company.CreateManaged(...)  con RUC provisional generado      [CompanyProvisioningService:34-55]
5. CompanyUserMembership.Create(company.Id, user.Id, role:"Admin")    [Handler:66-67]
6. state.MarkInitialized(email)  ← token quemado, irrepetible        [Handler:74]
```

**Limitaciones de la Ruta A:**
- El nombre del Tenant queda fijo como `"Principal"` (hardcoded en `Handler:57`)
- El slug queda fijo como `"principal"` (hardcoded en `Handler:57`)
- El RUC es provisional (generado automáticamente, marcado `TaxIdStatus.Pending`) — sin datos reales de la empresa
- **Solo puede ejecutarse UNA vez** — el token es de un solo uso (`MarkInitialized`)
- No existe validación de alcance de empresa: es una operación global fuera del ciclo de autorización normal

---

### Ruta B — Operación normal (crear empresa adicional)
**Endpoint:** `POST /api/companies` (`CompaniesController.cs:63`, `[Authorize(Policy = "perm:erp.companies.create")]`)
**Comando:** `CreateCompanyCommand` (TaxId, LegalName, MainAddress, ...)
**Handler:** `CreateCompanyHandler.cs`

Flujo verificado (citando líneas exactas):
```
1. RequireActiveTenantAsync()                                          [Handler:29-31]
     └─ CurrentUser.IsAuthenticated == true                            [CompanyAccessGuard:40]
     └─ CurrentTenant.TenantId != Guid.Empty                          [CompanyAccessGuard:44]
     └─ Tenant existe && IsActive                                      [CompanyAccessGuard:47-49]
2. CompanyProvisioningService.CreateManagedCompanyAsync(...)           [Handler:35-50]
     ├─ NormalizeProvidedTaxId()                                       [CompanyProvisioningService:74]
     ├─ EnsureTaxIdAvailableGloballyAsync()  (unicidad global)        [CompanyProvisioningService:75]
     ├─ Company.CreateManaged(...)                                      [CompanyProvisioningService:77-84]
     ├─ CompanyUserMembership.Create(creatorRole: "Admin")             [CompanyProvisioningService:89-90]
3. CompanyBootstrapService.BootstrapCompanyAsync(...)                  [Handler:52]
     └─ Crea sucursal principal + almacén principal
```

**Guarda de autorización real (Ruta B):**
```
[Authorize(Policy = "perm:erp.companies.create")]
  → PermissionHandler → role = ClaimTypes.Role (= membership.Role de empresa activa en JWT)
  → RuntimePermissionAuthorizer:
       if (string.Equals(role, "Admin", OrdinalIgnoreCase)) return true;   ← bypass
```

Resultado: **cualquier Admin de cualquier empresa del Tenant activo puede crear nuevas empresas** — sin restricción adicional.

---

## 2. Diferencias entre las dos rutas

| Aspecto | Ruta A (Bootstrap) | Ruta B (Operación) |
|---|---|---|
| Autenticación | Anónima (token de setup) | JWT requerido |
| ¿Quién puede ejecutarla? | Nadie (en sentido estricto) — token de arranque de un solo uso | Cualquier usuario con `membership.Role == "Admin"` en la empresa activa de la sesión |
| Datos de empresa | Hardcoded (`"Principal"`, RUC provisional) | Datos reales proporcionados por el caller |
| ¿Crea Tenant también? | ✅ Sí — crea `Tenant` + `Company` en una sola transacción | ❌ No — requiere `Tenant` preexistente |
| ¿Crea usuario inicial? | ✅ Sí — fused: crea usuario + empresa + membresía | ❌ No — el caller ya debe ser usuario autenticado |
| ¿Reutilizable? | ❌ Una vez por sistema | ✅ Irrestricta (siempre que el bypass lo permita) |
| Bootstrap de empresa | ❌ No — solo `Company`, sin sucursal/almacén | ✅ Sí — `ICompanyBootstrapService.BootstrapCompanyAsync` |

---

## 3. Problema arquitectónico confirmado

Las dos rutas no tienen un concepto de "quién tiene autoridad para crear empresas" — tienen dos mecanismos ad-hoc:
- Ruta A: token de arranque (válido para primer uso, inadecuado como modelo general)
- Ruta B: bypass por nombre de rol (`"Admin"`) — no tiene precedencia formal en el dominio

Según `ROLES_CANONICOS.md` (FASE 1, aprobado), la regla obligatoria es:
> **`Admin` NO crea empresas** — su autoridad está estrictamente acotada a la(s) empresa(s) donde tiene membresía activa.

Esto implica que la **Ruta B, tal como existe, viola directamente la regla canónica de roles**.

Y según el flujo objetivo del plan:
> **`SystemOwner (solo sistema) → Crear Empresa (Tenant + Company)`**
> **NO se permite ningún otro flujo paralelo.**

Esto implica que **solo una ruta debe existir**, y debe estar controlada por `SystemOwner`.

---

## 4. Flujo canónico propuesto

### 4.1 Principio rector

> La creación de un par `Tenant + Company` es una **operación de gobernanza del sistema**, no una operación de empresa. Solo `SystemOwner` puede iniciarla. Un `Admin` puede administrar las empresas donde tiene membresía, pero no crear nuevas.

### 4.2 Ruta única propuesta: `POST /api/setup/company`

```
SystemOwner (autenticado vía JWT con claim system_role: "SystemOwner")
  ↓
POST /api/setup/company
  [Authorize(Policy = "perm:system.company.create")]   ← policy nueva, solo SystemOwner
  ↓
CreateCompanyWithTenantCommand {
    TaxId, LegalName, MainAddress,
    AdminFirstName, AdminLastName, AdminEmail, AdminPassword  ← datos del primer Admin
}
  ↓
CreateCompanyWithTenantHandler:
  1. Validar que el caller tiene system_role == "SystemOwner"
  2. Validar unicidad de TaxId globalmente
  3. Tenant.Create(legalName, slug-generado, SystemOwner.UserId)
  4. Company.CreateManaged(tenant.Id, ...)
  5. IdentityUser.Create(adminFirstName, adminLastName, adminEmail, ...)
  6. CompanyUserMembership.Create(company.Id, user.Id, role: "Admin")
  7. CompanyBootstrapService.BootstrapCompanyAsync(...)  ← sucursal + almacén
```

### 4.3 Impacto sobre las rutas actuales

| Ruta actual | Destino propuesto |
|---|---|
| **Ruta A** (`POST /api/setup/admin`) | **Reemplazada** por la primera ejecución de `POST /api/setup/company` cuando el sistema no tiene `SystemOwner`. Debe conservarse SOLO la parte de inicialización del `SystemOwner` en sí — no la creación de empresa. |
| **Ruta B** (`POST /api/companies`) | **Eliminado** `[Authorize(Policy = "perm:erp.companies.create")]` o restringido a operaciones que no crean Tenant. El endpoint puede subsistir si existe un caso de uso legítimo de crear `Company` adicional bajo el mismo `Tenant`, pero no como punto de entrada para crear nuevas empresas independientes. |

### 4.4 Separación de responsabilidades por endpoint resultante

| Endpoint | Actor | Función |
|---|---|---|
| `POST /api/setup/system-owner` | Anónimo + token | Inicializar `SystemOwner` (único usuario de sistema, una sola vez) |
| `POST /api/setup/company` | `SystemOwner` | Crear nuevo par `Tenant + Company + Admin inicial` |
| `POST /api/companies` | `Admin` (empresa activa) | [Posible] Crear empresa **adicional** bajo el mismo Tenant ya existente — decisión pendiente (ver §5) |

---

## 5. Decisiones pendientes de aprobación explícita antes de implementar

Estas preguntas NO deben asumirse — requieren decisión explícita del usuario antes de pasar a FASE 3:

### D1 — ¿Existe el caso de uso "empresa adicional bajo el mismo Tenant"?
- **Sí (mantener Ruta B, restringida):** el `Admin` puede crear una `Company` adicional dentro de SU `Tenant` — no crea un nuevo `Tenant`, solo una empresa adicional subordinada. Requiere reescribir la guarda para que sea explícita y no pase por el bypass de `RuntimePermissionAuthorizer`.
- **No (eliminar Ruta B):** toda creación de empresa (sea la primera o adicional) pasa por `SystemOwner` vía `POST /api/setup/company`. El `Admin` solo administra, nunca crea.

### D2 — ¿Cómo se materializa `SystemOwner` en el JWT?
- **Opción A — Claim separado:** `system_role: "SystemOwner"` como claim adicional en el JWT, resuelto en `AccessTokenService.GenerateToken` al detectar que el usuario tiene un flag especial de gobernanza (p.ej., `IdentityUser.IsSystemOwner = true`). El claim `role` sigue siendo el rol operativo de empresa (o vacío si `SystemOwner` no tiene empresa activa).
- **Opción B — Tipo de usuario enum:** usar `IdentityUserType` (ya existe: `Platform = 0`, `Tenant = 1`, `Company = 2`) — agregar `SystemOwner = 3` o reutilizar `Platform = 0` con semántica redefinida. Más invasivo — requiere migración de datos.
- **Opción C — Policy sin claim nuevo:** una policy `perm:system.company.create` resuelta contra un flag en BD (`IdentityUser.IsSystemOwner`), sin agregar claims nuevos al JWT. Más limpio, más coherente con el patrón existente de `PermissionPolicyProvider`.

### D3 — ¿Se conserva la Ruta A (`POST /api/setup/admin`) en alguna forma?
La Ruta A actual crea `SystemOwner` + `Tenant` + `Company` de golpe. En el flujo canónico, la inicialización del `SystemOwner` (sin empresa) y la primera empresa son pasos separados:
- **Opción A:** Dividir en dos pasos (`/api/setup/system-owner` → luego `/api/setup/company`).
- **Opción B:** Conservar `/api/setup/admin` pero redefinirlo: crea `SystemOwner` sin empresa, la empresa se crea en un paso posterior. El nombre del endpoint puede mantenerse o cambiarse.

---

## 6. Riesgos de la transición

| Riesgo | Severidad | Mitigación |
|---|---|---|
| Si se elimina Ruta B sin reemplazar, el Admin queda sin forma de crear empresas adicionales si existe ese caso de uso real | 🔴 Bloqueante si D1 = "Sí" | Resolver D1 antes de implementar |
| Si `SystemOwner` se implementa con `IdentityUserType.Platform` (valor 0, actualmente `[Obsolete]` en `IdentityUserType.cs`) se invaden conceptos del bounded context `Platform` prohibidos por `ERP_CORE_FREEZE.md` | 🔴 Bloqueante | Solo usar `IdentityUserType.SystemOwner` nuevo o mantenerlo en BD como flag de negocio (no como enum `Platform`) — evitar todo nombre/tipo que colisione con el test `PlatformControlPlaneGuardTests` |
| `CreateManagedCompanyAsync` escribe su propio `SaveChangesAsync` (línea 87) — si la operación nueva es transaccional con creación de `Tenant` + `User`, hay riesgo de transacción parcial | 🟠 Medio | Envolver en una unidad de trabajo única o confirmar que `DbContext` compartido garantiza atomicidad |
| El test `PlatformControlPlaneGuardTests` fallará si cualquier nuevo archivo contiene "SuperAdmin" — verificado que "SystemOwner" no colisiona | 🟢 Resuelto | Verificado en FASE 1: "SystemOwner" = 0 colisiones |

---

## 7. Lo que esta fase NO toca

- No se modifican `CreateInitialAdminHandler`, `CreateCompanyHandler`, ni `CompanyAccessGuard`.
- No se agrega ningún endpoint nuevo.
- No se modifica `RuntimePermissionAuthorizer.cs`.
- El código actual sigue funcionando exactamente igual que antes de esta fase.

---

## COMPANY_CREATION_FLOW — pendiente de aprobación
