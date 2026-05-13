# Guía: flujo ordenado de creación (sin datos automáticos)

Este documento alinea el **objetivo de producto** con lo que **ya existe** en el monolito y lo que quedó **recién ajustado** (mayo 2026).

## 1. Primer inicio y SuperAdmin (sin seed de demo)

| Qué pedías | Estado en código |
|------------|-------------------|
| No crear tenant/planes/bodegas al arrancar | **Hecho:** `DevDatabaseSeeder` ya **no** corre salvo `Development:SeedDemoTenant: true` en `appsettings.Development.json` (por defecto `false`). Ver `Program.cs`. |
| First run solo emite token y permite crear SuperAdmin | **Ya era así:** `IFirstRunSetupService.EnsureTokenIssuedAsync` + `SetupController` / claim SuperAdmin. No inserta tenants. |
| Tras crear SuperAdmin: solo `first_run_setup_state` + usuario global | El SuperAdmin vive en **`users`** con rol SuperAdmin y `tenant_id` vacío (convención JWT). Revisa `docs/SUPERADMIN-Y-FIRST-RUN.md`. |

**Seed de demo (`tenant-demo`, `admin@erp.com`, cuentas, IVA, etc.):** sigue en `DevDatabaseSeeder.cs` solo para quien ponga `SeedDemoTenant: true` (p. ej. demos locales).

## 2. Planes comerciales (CRUD SuperAdmin)

Tu especificación (`GET/POST/PUT/DELETE /api/superadmin/planes`, columna `MenuConfig`) se mapea al diseño **actual** del producto:

| Tu idea | Implementación actual |
|---------|------------------------|
| Lista / crear / editar / borrar planes | **`SaasPlansAdminController`** ruta base **`/api/superadmin/saas-plans`** (no literal `/planes`). |
| Características / módulos en JSON | **`saas_plan_features`** + catálogo **`saas_feature_definitions`**. Sustituye a una única columna `MenuConfig`: más normalizado y ya usado por suscripciones. |
| Asignar módulos al plan | `PUT /api/superadmin/saas-plans/{planId}/features` con cuerpo `{ features: [ { featureId, isIncluded, limitPerPeriod } ] }`. La UI de administración está en **`SuperAdminPlansSection`** (pestaña **Planes** del panel). |
| Borrar solo si no hay empresas | **`DeletePlanAsync`** bloquea si existe fila en **`tenant_saas_subscriptions`** con ese `planId`. Las empresas nuevas guardan hoy **`tenants.plan_code`** (string); si necesitas bloqueo también por `plan_code`, conviene unificar suscripción en fila `TenantSaasSubscription` al crear tenant (trabajo futuro). |

**Features del sistema:** se administran en la pestaña **Características** (`SuperAdminFeaturesSection` + `SaasFeaturesAdminController`).

## 3. Creación de empresa sin datos extra

| Tu idea | Implementación actual |
|---------|------------------------|
| `POST /api/superadmin/empresas` | **`POST /api/access/superadmin/tenants`** con cuerpo **`SuperAdminCreateTenantWithAdminCommand`** (camelCase en JSON). Ver `AccessController` + `SuperAdminCreateTenantWithAdminHandler`. |
| `planId` obligatorio | Hoy el contrato usa **`planCode`** (string, código del plan en catálogo). **Regla nueva:** debe existir al menos un plan en catálogo y **debe enviarse un `planCode` no vacío** que coincida con un plan **activo**. |
| Admin local | Se crea **`IdentityUser`** + **`Membership`** rol **`Admin`** (no “Administrador” literal; el producto usa `Admin`). |
| Sin bodega / catálogos / cuentas | **`SuperAdminCreateTenantWithAdminHandler`** solo persiste tenant + usuario + membresía (+ auditoría). **No** llama a creación de bodegas ni plan contable (eso antes solo lo hacía `DevDatabaseSeeder`). |
| RUC | Campo opcional **`ruc`** en el comando. |
| Dirección | **No hay** columna genérica “dirección” en `Tenant` hoy; añadirla implica dominio + migración + formulario. |

**Campos típicos del modal actual:** `tenantName`, `tenantSlug`, `planCode`, admin nombre/apellido/email/password, `passwordResetMode`, restricción opcional de módulos (`enabledModules`).

## 4. Frontend SuperAdmin

| Tu idea | Estado |
|---------|--------|
| Tabs Empresas / Planes | Ya existían: **Resumen**, **Empresas**, **Características**, **Planes**. |
| Sin empresas → bienvenida + “Crear primera empresa” | **Añadido** en pestaña **Resumen** cuando `tenants.length === 0`, con enlace a **Planes** si aún no hay catálogo. |
| Crear empresa sin plan | **Bloqueado** en UI y API: mensaje si no hay planes activos o si no se selecciona plan. |

## 5. Orden operativo recomendado

1. Arrancar API → completar **first run** → crear **SuperAdmin** (token único).
2. (Opcional) Definir **features** en catálogo si el entorno está vacío.
3. Crear **planes** en pestaña Planes y asignar **features** al plan.
4. Crear **empresa** con plan obligatorio y admin identity.
5. El admin entra al tenant y crea **bodegas, productos, cuentas**, etc., según permisos.

## 6. Próximos refinos (si quieres alinear 100 % con el documento original)

- Exponer **alias** de ruta `GET/POST /api/superadmin/planes` que reenvíen a `saas-plans` (compatibilidad con clientes antiguos).
- Añadir **`planId`** (Guid) en el comando de alta de empresa resolviendo internamente a `planCode`.
- Columna **dirección** en `Tenant` + formulario.
- Al crear tenant, insertar **`TenantSaasSubscription`** para que `DELETE` de plan refleje “empresas asignadas” de forma consistente.
- Renombrar rol mostrado a “Administrador” en UI manteniendo claim `Admin` en backend.

## Prueba integral paso a paso

Ver **`docs/PRUEBA-CERO-DESDE-CERO.md`** y el script **`scripts/prueba-desde-cero.ps1`** (Development).
