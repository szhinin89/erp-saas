# AUTHORIZATION AUDIT — Bypass `role == "Admin"` en `RuntimePermissionAuthorizer`

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

> **FASE 1** del Plan de Remediación Controlada por Fases.
> Fecha: 2026-06-08 · Branch: `feat/platform-kernel-refactor`
> **Documento de solo lectura — no se modificó código fuente.**

---

## 1. ¿Existe realmente el bypass?

**Sí. Confirmado textualmente.**

```csharp
// backend/src/ERP.Application/Access/Authorization/RuntimePermissionAuthorizer.cs:37-38
if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
    return true;
```

Contexto completo del método (`IsAuthorizedAsync`, líneas 26-54):
1. Si no hay `tenant_id` en contexto → `false` (línea 29-30)
2. Si el `Tenant` no existe en BD → `false` (línea 34-35)
3. **Si `role == "Admin"` (case-insensitive) → `true` incondicional, sin más validación (líneas 37-38)**
4. Solo si `role != "Admin"`: resuelve membresía operativa + claves de permiso efectivas por perfil (líneas 43-53)

### Origen del valor `role`
Rastreado hasta su fuente (`PermissionHandler.cs:27`):
```csharp
var role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
```
El claim `ClaimTypes.Role` del JWT se genera en `AccessTokenService.GenerateToken` (línea 63: `new Claim(ClaimTypes.Role, role)`), y el valor `role` que llega allí proviene — confirmado en los 3 emisores de sesión:

| Emisor | Archivo | Valor de `role` pasado |
|---|---|---|
| Login con empresa activa | `LoginHandler.cs:104-105` | `membership.Role` (rol del usuario en **esa** `CompanyUserMembership`) |
| Login sin empresa (pendiente selección) | `LoginHandler.cs:80-82` | constante `"User"` (línea 80) |
| Switch company | `SwitchCompanyHandler.cs:60` | `membership.Role` de la empresa destino |

**Conclusión de origen:** el claim `ClaimTypes.Role` (y por tanto el `role` evaluado en el bypass) es **el rol operativo de `CompanyUserMembership` para la empresa activa de la sesión** — un concepto *scoped a una empresa* — no un rol de plataforma ni un "SuperAdmin" global.

---

## 2. Contradicción con la documentación oficial (Nivel 3 — `docs/IDENTITY.md`)

`docs/IDENTITY.md:28` documenta explícitamente el modelo **pretendido**:

> *"Bypass ERP solo operador platform global (`tenant_id=Guid.Empty`, rol JWT `PlatformOperator`)."*

Es decir: según la documentación vigente, **el único bypass de permisos debería estar reservado al rol `PlatformOperator`** (un `IdentityUser` de tipo `Platform`, con `tenant_id = Guid.Empty`), no a un rol de empresa como `"Admin"`.

**El código implementado NO coincide con esta documentación**:
- El bypass real evalúa `role == "Admin"` (rol de `CompanyUserMembership`, scoped a una `Company` dentro de un `Tenant` concreto — `tenant_id != Guid.Empty`)
- No existe ninguna verificación de `role == "PlatformOperator"` ni de `tenant_id == Guid.Empty` en `RuntimePermissionAuthorizer`
- `grep -rn "SuperAdmin|PlatformOperator" backend/src/ERP.Application/Modules/Companies` → **0 resultados**: el flujo de creación de empresas no contempla en absoluto el concepto `PlatformOperator`

➡️ **Esto es una divergencia real entre lo documentado (Nivel 3) y lo implementado**, jerárquicamente inferior y por tanto "desactualizado" según `CLAUDE.md` — pero el hecho de que el código contradiga su propia documentación de seguridad es, en sí, el hallazgo crítico.

---

## 3. ¿"SuperAdmin" es un concepto válido para el ERP?

**No, y no debe serlo — por diseño explícito y verificado.**

`backend/src/ERP.Architecture.Tests/PlatformControlPlaneGuardTests.cs` es un **test arquitectónico bloqueante en CI** que afirma:

```csharp
// línea 21-22
if (Path.GetFileName(file).Contains("SuperAdmin", StringComparison.OrdinalIgnoreCase))
    violations.Add($"{rel}: legacy superadmin controller file name");
...
// línea 56-58
Directory.Exists(platformDir).Should().BeFalse(
    "ERP does not own the Platform control plane. Platform is a separate future bounded context.");
```

Y `IdentityUserType.cs` documenta:
```csharp
/// <summary>Operador de plataforma (operador platform global). Sin subscriber operativo.</summary>
Platform = 0,
```

**Conclusión:** "SuperAdmin" tal como lo planteó la auditoría original **no es el término correcto para este ERP** — el concepto equivalente y correcto es **`PlatformOperator`** (`IdentityUserType.Platform`, `tenant_id = Guid.Empty`), que **según `docs/IDENTITY.md` debería ser el único bypass**, pero que **no está implementado en `RuntimePermissionAuthorizer`** (ni en ningún punto del flujo de creación de empresas).

Esto reencuadra el hallazgo: no se trata de "falta una validación de SuperAdmin", sino de que **el bypass documentado para `PlatformOperator` fue, en algún punto, sustituido/simplificado por un bypass genérico a `role == "Admin"`** — probablemente como atajo durante el desarrollo del modo "ERP no-SaaS, single-tenant", sin actualizar ni el código ni alinear con `docs/IDENTITY.md`.

---

## 4. ¿Quién puede crear empresas HOY?

Cadena de autorización real para `POST /api/companies`:

```
[Authorize(Policy = CompanyPermissions.PolicyCreate)]   // = "perm:erp.companies.create"
  → PermissionPolicyProvider → PermissionRequirement("erp.companies.create")
  → PermissionHandler → role = claim ClaimTypes.Role (= membership.Role de la empresa activa)
  → RuntimePermissionAuthorizer.IsAuthorizedAsync
       → role == "Admin" ? true : (resolver permisos efectivos por perfil)
```

**Respuesta empírica:** *cualquier `IdentityUser` cuya `CompanyUserMembership.Role` para la empresa activa de su sesión sea exactamente `"Admin"` (case-insensitive)* puede invocar `POST /api/companies` y crear nuevas empresas — **sin importar de qué empresa sea Admin**, porque el chequeo del bypass ocurre **antes** de cualquier validación de alcance por empresa, y `ICompanyAccessGuard.RequireActiveTenantAsync()` (la única guarda en `CreateCompanyHandler`) solo exige que el **Tenant** esté activo, no valida nada sobre la empresa de origen del solicitante.

Adicionalmente: `CreateCompanyHandler.cs:41` asigna `creatorRole: "Admin"` de forma fija al crear la membresía del creador en la nueva empresa — es decir, **todo creador se convierte automáticamente en `"Admin"` de la empresa que crea**, perpetuando la cadena de bypass.

## 5. ¿Quién DEBERÍA poder crear empresas?

Basado en la documentación oficial vigente y el modelo de capas (`docs/IDENTITY.md`, `ERP_CORE_FREEZE.md`, arquitectura tests):

- **Modelo documentado (`docs/IDENTITY.md:28`)**: el bypass — y por extensión, las operaciones de alcance de plataforma como crear nuevas empresas/tenants — debería reservarse a `PlatformOperator` (`tenant_id = Guid.Empty`).
- **Pero**: el ERP actual opera en **modo no-SaaS, single-tenant** (confirmado en el prompt del usuario y en `ERP_CORE_FREEZE.md`), y `PlatformOperator` pertenece a un **bounded context futuro** (`Platform`) que el ERP **no debe implementar** (test bloqueante `PlatformControlPlaneGuardTests`).
- **Esto crea una tensión real**: si solo `PlatformOperator` puede crear empresas, pero `PlatformOperator` no debe existir en el ERP puro, **¿quién crea la segunda, tercera, etc. empresa de un Tenant operativo?**

➡️ **Esta es una decisión de producto/arquitectura que debe tomar el responsable del ERP, no asumirse**: las dos opciones consistentes son:

| Opción | Descripción | Implicación |
|---|---|---|
| **(a)** Mantener la creación de empresas como capacidad **del Tenant** (rol "Admin" de cualquier empresa del mismo Tenant, validado explícitamente — no por bypass genérico) | Coherente con "ERP single-tenant, sin Platform" | Requiere reescribir el chequeo para que sea una policy explícita y auditable (`erp.companies.create` resuelta por permisos de perfil reales, no por bypass de rol) — eliminando la contradicción con `docs/IDENTITY.md` (que quedaría desactualizado y debería corregirse para reflejar esta decisión) |
| **(b)** Reservar la creación de empresas/tenants para un futuro `PlatformOperator` | Coherente con `docs/IDENTITY.md:28` tal como está redactado hoy | Bloquea la creación de nuevas empresas hasta que exista la plataforma SaaS — probablemente **no deseable** para el modo operativo actual descrito por el usuario (`SuperAdmin → Crear Empresa → Crear Admin → Operar`) |

**Recomendación de este auditor (no vinculante, requiere aprobación):** Opción (a), pero **eliminando el bypass genérico por nombre de rol** y sustituyéndolo por una verificación explícita y con alcance correcto (p. ej., "Admin de *cualquier* empresa activa del Tenant actual" validado contra el repositorio de membresías — no un `string.Equals` ciego), dejando además una nota de actualización pendiente en `docs/IDENTITY.md:28` para alinear la documentación con la decisión tomada.

---

## 6. Clasificación del hallazgo

| Aspecto | Evaluación |
|---|---|
| ¿Existe el bypass? | ✅ Confirmado (`RuntimePermissionAuthorizer.cs:37-38`) |
| ¿Contradice la documentación de seguridad vigente? | ✅ Sí (`docs/IDENTITY.md:28` documenta bypass solo para `PlatformOperator`) |
| ¿Es explotable hoy de forma anómala? | ⚠️ Parcialmente — en el modo single-tenant actual, "Admin" suele ser efectivamente el rol más alto disponible, por lo que el impacto práctico inmediato es menor; el riesgo crece si el Tenant tiene múltiples empresas con Admins independientes (cualquiera podría crear empresas adicionales sin que el sistema lo distinga de una decisión "del Tenant") |
| ¿Bloquea la migración a SaaS? | ✅ Sí — sin corregirlo, introducir `PlatformOperator`/`Subscriber` heredaría un modelo de autorización inconsistente con su propia documentación |

**Severidad confirmada: 🔴 Crítico** — no por explotabilidad inmediata (el sistema es single-tenant hoy), sino porque el código **contradice su propia documentación de seguridad** y **no hay forma de distinguir entre "Admin de empresa" y "operador con privilegios de plataforma"**, lo cual es exactamente la ambigüedad que debe resolverse antes de avanzar a SaaS.

---

## 7. Decisión requerida antes de Fase 2

Esta fase **no modifica código**. Para proceder a la Fase 2 (remediación), se requiere que el responsable del ERP **apruebe explícitamente una de las dos opciones del §5** (o proponga una tercera), ya que la implementación de la corrección depende directamente de esa decisión de producto.

---

## AUTHORIZATION_AUDIT — pendiente de aprobación y de decisión sobre §5
