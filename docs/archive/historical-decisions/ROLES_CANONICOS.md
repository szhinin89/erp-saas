# ROLES_CANONICOS — Definición oficial de roles del ERP (flujo puro)

> ## ⚠️ HISTÓRICO
>
> Este documento representa una decisión, auditoría o estado anterior del proyecto.
>
> **NO representa la arquitectura actual del ERP.** La propuesta `SystemOwner` documentada aquí no fue implementada en el código (`grep` sin resultados en `backend/src`).
>
> La fuente de verdad actual es:
> - [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md)
> - [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md)
> - El código fuente actual (`frontend/src`, `backend/src`)

---

> **FASE 1 — DEFINICIÓN DE ROLES CANÓNICOS** del plan "Consolidación ERP Puro".
> Fecha: 2026-06-08 · Branch: `feat/platform-kernel-refactor`
> **Documento de definición — NO se tocan endpoints ni código en esta fase.**
> Resuelve la colisión señalada en [`ERP_FLOW_CURRENT_STATE.md §4.1`](ERP_FLOW_CURRENT_STATE.md#4-riesgos-detectados): el rol de sistema se nombra **`SystemOwner`** (decisión del usuario), un término **ERP-nativo** que no colisiona con `PlatformControlPlaneGuardTests` ni con `ERP_CORE_FREEZE.md` (verificado: 0 coincidencias de "SystemOwner" en el código, y no figura entre los términos prohibidos `SuperAdmin`/`Subscription`/`CommercialPlan`/`BillingCycle`/`SaasBilling`).

---

## 1. Jerarquía canónica de roles (definición obligatoria)

```
Nivel SISTEMA (bootstrap, fuera del ciclo operativo)
   └─ SystemOwner
         · Crea Empresas (Tenant + Company)
         · Crea el Admin inicial de cada empresa
         · NO opera el ERP día a día
         · Existe SOLO para el flujo de arranque/gobernanza, no para uso recurrente

Nivel EMPRESA (operación diaria, scoped a una Company)
   ├─ Admin
   │     · Administra SU empresa (configuración, usuarios, permisos de su Company)
   │     · NO crea nuevas empresas
   │     · NO tiene bypass de autorización fuera del alcance de su Company
   ├─ Manager
   │     · Rol intermedio operativo — alcance definido por perfil/permisos asignados
   │     · Sin privilegios administrativos de empresa
   └─ User
         · Rol operativo base — alcance mínimo, definido por perfil/permisos asignados
```

### Reglas obligatorias (no negociables, definidas por el usuario)
1. **`SystemOwner` NO opera el ERP** — su única función es el arranque/gobernanza (crear empresas y sus administradores iniciales). No debe usarse para operación diaria ni aparecer en flujos de negocio del ERP.
2. **`Admin` NO crea empresas** — su autoridad está estrictamente acotada a la(s) empresa(s) donde tiene membresía activa.
3. **`Admin` SOLO opera su empresa** — ninguna acción de un `Admin` debe poder afectar datos o configuración de una empresa donde no tiene membresía.

---

## 2. Mapeo: rol canónico ↔ representación técnica actual/propuesta

| Rol canónico | ¿Existe hoy en código? | Representación técnica actual | Representación técnica propuesta (a validar en fases de implementación) |
|---|---|---|---|
| `SystemOwner` | ❌ No existe | — (el bootstrap es anónimo + token-gated, sin rol asociado — `CreateInitialAdminHandler`) | Nuevo concepto ERP-nativo. **No debe** mapearse a `IdentityUserType.Platform` (eso es `PlatformOperator`, perteneciente al bounded context `Platform`, prohibido en ERP). Debe vivir dentro del dominio `Tenant`/`Access` del ERP — p.ej. como un flag/claim de gobernanza acotado al proceso de bootstrap, NO como un `CompanyUserMembership.Role` operativo |
| `Admin` | ✅ Existe | `CompanyUserMembership.Role = "Admin"` (string libre, scoped a `Company`) | Mantener como rol de empresa, pero **eliminar su bypass global** en `RuntimePermissionAuthorizer.cs:37-38` — su autoridad debe resolverse igual que cualquier otro rol: vía membresía + permisos de perfil, nunca por `string.Equals(role, "Admin")` |
| `Manager` | ❌ No existe | — (mencionado en el flujo objetivo, sin contraparte en código) | Si se requiere, modelarlo como **perfil de permisos** (vía el sistema de perfiles ya existente — `ProfileId` en `CompanyUserMembership`), no como un nuevo valor mágico de `Role` |
| `User` | ⚠️ Existe parcialmente | Constante `"User"` usada solo como placeholder de sesión sin empresa activa (`LoginHandler.cs:80`) | Formalizar como rol operativo base real (no solo placeholder de transición), resuelto vía perfiles de permisos |

---

## 3. Por qué `SystemOwner` y no otro de los términos descartados

| Término | Por qué se descarta |
|---|---|
| `SuperAdmin` | Prohibido textualmente por `PlatformControlPlaneGuardTests.cs:21-22,47-50` y por la regla congelada *"ERP never depends on Platform"* (`ERP_CORE_FREEZE.md`) |
| `PlatformOperator` | Es un concepto **documentado pero perteneciente al bounded context `Platform`** (`docs/IDENTITY.md:14,28`, `IdentityUserType.Platform`). Usarlo dentro del ERP violaría la separación de fronteras certificada en el acta de congelamiento (`commit 2e51c72e`) — el ERP no debe depender ni implementar conceptos de `Platform` |
| `TenantOwner` | Recomendación inicial de este auditor; el usuario eligió proponer su propio nombre |
| **`SystemOwner`** ✅ | Término ERP-nativo, sin colisión textual verificada con código ni con reglas congeladas; comunica correctamente la semántica "máxima autoridad de gobernanza del sistema, fuera del ciclo operativo" sin invocar conceptos de `Platform` |

---

## 4. Consecuencias de esta definición para fases posteriores (sin implementar aún)

- **Fase 2 (Flujo de empresas)** deberá decidir *cómo* se materializa `SystemOwner` técnicam/concretamente: ¿un claim JWT especial emitido solo durante bootstrap?, ¿un flag en `IdentityUser`?, ¿una policy nueva `perm:system.companies.create` resuelta sin pasar por `CompanyUserMembership`? — esa es una decisión de implementación que corresponde a esa fase, no a esta.
- **Fase 2** también deberá resolver la doble ruta de creación de empresas (bootstrap vs `/api/companies`) **unificándolas bajo `SystemOwner`**, conforme a la regla *"NO se permite ningún otro flujo paralelo"*.
- **Esta fase NO modifica `RuntimePermissionAuthorizer`, controladores, ni JWT** — esos cambios pertenecen a fases posteriores y requieren su propia aprobación.

---

## ROLES_CANONICOS — pendiente de aprobación
