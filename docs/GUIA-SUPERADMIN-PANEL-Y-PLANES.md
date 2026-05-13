# Guía: panel global SuperAdmin, empresas, planes y menú dinámico

Documento de **mejora de flujo** anclado al repositorio actual. Corrige suposiciones habituales y propone pasos concretos sin reescribir el modelo SaaS desde cero.

## Aclaraciones respecto a suposiciones genéricas

| Suposición frecuente | Realidad en este repo |
|----------------------|------------------------|
| `IsGlobalSuperAdmin` / usuario `Usuario` legacy | Autenticación moderna: **`IdentityUser` + `Membership`**, usuarios legacy `users` solo para **SuperAdmin** global (`role = 'SuperAdmin'`, `tenant_id` vacío). JWT con `tenant_id` y `role`. |
| Impersonación auditada con tablas dedicadas | Hoy el acceso “como empresa” del SuperAdmin es **`POST /api/auth/switch-tenant`** (nuevo JWT con `tenant_id`). **No** hay log de impersonación ni motivo obligatorio; ver [`docs/SUPERADMIN-Y-FIRST-RUN.md`](SUPERADMIN-Y-FIRST-RUN.md). |
| `GET /api/superadmin/empresas` | El listado global es **`GET /api/superadmin/tenants`**. “Empresas” en UI suele mapear a tenants + datos legales en otras rutas (`/api/tenants`, `/companies`). |
| `PlanId` UUID FK a tabla `planes` | El agregado `Tenant` usa **`PlanCode` (string)** opcional + **`EnabledModulesJson`** (lista de claves de módulo). Los planes comerciales viven en catálogo **SaaS** (`saas_plans`, etc.) y se enlazan por **código** alineado con `PlanCode`. |
| `MenuItems` JSON en tabla Planes | El menú navegable sale de **`ui_nav_groups` / `ui_nav_items`** y del snapshot de sesión; la visibilidad por contrato se cruza con **features SaaS** y `enabledModules` del tenant. Ver [`docs/COMPANIES-PLAN-MENU-ADMIN.md`](COMPANIES-PLAN-MENU-ADMIN.md). |

---

## 1. Lo que ya existe (inventario)

### Backend – SuperAdmin y tenants

| Área | Rutas / piezas |
|------|------------------|
| Listado tenants + conteos usuarios + `planCode` + módulos efectivos | `GET /api/superadmin/tenants` → `SuperAdminController`, `ITenantRepository.GetAllAsync` + `TenantSubscriptionCatalog.GetEffectiveEnabledModules` |
| Métricas agregadas | `GET /api/superadmin/metrics`, `GET /api/superadmin/growth-analytics`, monetario |
| Catálogo planes (lectura) | `GET /api/superadmin/plans` |
| CRUD planes SaaS, features, orden | `SaasPlansAdminController`, `SaasFeaturesAdminController` (rutas bajo `/api/superadmin/saas-*`) |
| Menú global (árbol BD) | `GET /api/superadmin/navigation-menu` y PUT/POST de reordenado / ítems |
| **Cambiar plan / módulos de una empresa** | **`PATCH /api/tenants/{id}/subscription`** (body `planCode`, `enabledModules`) — rol **SuperAdmin**; no está duplicado bajo `/api/superadmin/...` |
| Alta empresa + admin | **`POST /api/access/superadmin/tenants`** (`SuperAdminCreateTenantWithAdminCommand`) — crea `Tenant` + `IdentityUser` + `Membership` Admin; body opcional **`planCode`**, **`enabledModules`** |

### Dominio – suscripción tenant

- `Tenant.Create(..., planCode, enabledModuleKeys)` y `SetSubscription` ya soportan plan y módulos.
- El handler **`SuperAdminCreateTenantWithAdminHandler`** recibe **`PlanCode`** y **`EnabledModules`** opcionales en **`SuperAdminCreateTenantWithAdminCommand`**: valida el plan contra el catálogo activo (`ISaasCatalogQuery`) y las claves de módulo con **`TenantSubscriptionCatalog.ValidateModuleKeysOrThrow`**; normaliza lista vacía / ausente como **sin restricción JSON** (equivalente a “todos los módulos” respecto a `EnabledModulesJson`). Pasa `planCode` y `enabledModuleKeys` a **`Tenant.Create`** en el flujo de admin nuevo y al vincular admin existente.

### Frontend

- **`SuperAdminPanelPage`** (`/superadmin`): pestañas overview, companies, features, plans; métricas, lista tenants, modal **crear empresa** (plan + opción **restringir módulos** con checkboxes), botón por fila **Plan y módulos** (modal → `PATCH` de suscripción), planes SaaS, growth.
- **`superAdminService`**: `getTenants`, `getPlansCatalog`, `getMetrics`, **`createTenantWithAdmin`** → `POST /api/access/superadmin/tenants` (cuerpo con `planCode`, `enabledModules`); **`updateTenantSubscription`** → **`PATCH /api/tenants/{id}/subscription`**.
- **`frontend/src/constants/subscriptionModules.ts`**: `TENANT_MODULE_KEYS` alineado con **`TenantSubscriptionCatalog.AllModuleKeys`** en backend.
- Menú de sesión en app: **`GET /api/access/me/menu`** (`accessService.getSessionMenu`), filtrado con **`enabledModules`** y permisos en `AppLayout` / `permissionsStore`.

### Documentación relacionada

- Plan ↔ menú ↔ features: [`docs/COMPANIES-PLAN-MENU-ADMIN.md`](COMPANIES-PLAN-MENU-ADMIN.md)
- Política de acceso SuperAdmin vs plan: [`docs/POLITICA-FORMULARIOS-Y-ACCESO.md`](POLITICA-FORMULARIOS-Y-ACCESO.md)
- Pantallas: [`docs/FRONTEND-PANTALLAS.md`](FRONTEND-PANTALLAS.md)

---

## 2. Brechas prioritarias (respecto al flujo deseado)

1. ~~**Crear empresa con plan en el mismo flujo**~~ **(hecho)**  
   Comando + handler + modal en **`SuperAdminPanelPage`** + **`createTenantWithAdmin`** con `planCode` / `enabledModules`. No se deriva automáticamente la lista de módulos desde las *features* del plan; la restricción explícita sigue siendo opcional por UI.

2. ~~**Cambiar plan desde el panel sin ir a otra pantalla**~~ **(hecho)**  
   Botón **Plan y módulos** en la fila del tenant → modal → **`PATCH /api/tenants/{id}/subscription`**. Sigue siendo opcional añadir un alias bajo `/api/superadmin/tenants/...` solo por ergonomía.

3. **Métricas por empresa en el listado**  
   Hoy `GET /api/superadmin/tenants` incluye usuarios; **no** incluye ventas del mes. Añadir consulta agregada (ventas por `tenant_id` y rango de fechas) o endpoint dedicado `GET /api/superadmin/tenants/{id}/usage` para no inflar el listado.

4. **Menú “solo por plan” para usuarios de empresa**  
   Ya se combina **plan + `enabledModules` + permisos** en cliente y políticas de API. No hace falta duplicar un `GET /api/superadmin/menu-config/{planId}` para el admin de tenant salvo que quieras **previsualizar** menú antes de guardar plan; en runtime basta con **`/api/access/me/menu`** tras login o `switch-tenant`.

5. **Unificación UX**  
   Tabs actuales están bien; mejorar copy y accesos directos a **Companies** (`/companies`) para edición legal RUC/dirección. La acción de plan/módulos ya está en el panel.

---

## 3. Plan de implementación recomendado (orden)

### Fase A – Crear tenant con plan (backend + frontend) — **implementada**

| Paso | Estado |
|------|--------|
| `SuperAdminCreateTenantWithAdminCommand` con `PlanCode` y `EnabledModules` | Hecho |
| Validación plan activo + módulos (`ISaasCatalogQuery`, `TenantSubscriptionCatalog`) | Hecho |
| `Tenant.Create(..., planCode, enabledModuleKeys)` en ambos flujos (admin nuevo / admin existente) | Hecho |
| Body JSON camelCase en `POST /api/access/superadmin/tenants` | Hecho |
| Modal crear empresa: selector de plan (planes activos de `getPlansCatalog`), restricción opcional de módulos | Hecho |
| Tests automatizados dedicados (crear tenant con plan y assert en BD) | Pendiente (recomendado) |

### Fase B – Cambiar plan desde panel — **implementada**

| Paso | Estado |
|------|--------|
| Modal en fila de tenant + planes + restricción de módulos (misma lógica que alta: sin restricción o subconjunto de `TENANT_MODULE_KEYS`) | Hecho |
| `PATCH /api/tenants/{tenantId}/subscription` desde `superAdminService.updateTenantSubscription` | Hecho |
| `TenantsController.UpdateSubscription`: `[Authorize(Roles = "SuperAdmin")]` (no exige `tenant_id` de empresa en el JWT para esta ruta) | Ya existía |

### Fase C – Métricas

1. Query agregada ventas últimos 30 días por `tenant_id` (repositorio ventas o SQL en infra).  
2. Ampliar DTO de `GET /api/superadmin/tenants` o endpoint paralelo para no romper clientes existentes.

### Fase D (opcional) – FK `PlanId` a `saas_plans`

Solo si se requiere integridad referencial estricta: migración `tenants.plan_id` nullable FK + rellenado desde `PlanCode` + transición gradual. Hoy el producto está pensado en **código de plan** string; cambiar es coste de migración y de todos los handlers.

---

## 4. Flujo end-to-end (Fase A/B en producción en código)

1. SuperAdmin entra a `/superadmin` → pestaña **Empresas**.  
2. **Nueva empresa**: nombre, slug, **plan opcional**, **restricción opcional de módulos**, admin Identity, contraseña → `POST /api/access/superadmin/tenants` con `planCode` y `enabledModules` (lista vacía o “todos los módulos” marcados se normaliza a sin JSON de restricción). Los datos fiscales extendidos (RUC, etc.) siguen pudiendo editarse desde **Companies** si el flujo lo requiere.  
3. **Plan y módulos**: fila → **Plan y módulos** → modal → `PATCH /api/tenants/{id}/subscription`.  
4. Admin de la empresa hace login → JWT con `tenant_id` + `enabledModules` → `GET /api/access/me/menu` → drawer filtrado.  
5. SuperAdmin “ver como empresa”: `POST /api/auth/switch-tenant` (sin auditoría extendida hasta que se implemente).

---

## 5. Referencias de código (arranque rápido)

- Panel, modal creación y modal plan/módulos: `frontend/src/pages/SuperAdminPanelPage.tsx`  
- Claves de módulo UI: `frontend/src/constants/subscriptionModules.ts`  
- Cliente API: `frontend/src/services/superAdminService.ts` (`createTenantWithAdmin`, `updateTenantSubscription`)  
- Alta tenant + admin: `backend/.../SuperAdminTenants/SuperAdminTenantHandlers.cs`, `SuperAdminTenantCommands.cs`  
- Suscripción: `backend/.../Tenants/Entities/Tenant.cs`, `ERP.Application.Tenants.UseCases.UpdateTenantSubscription.UpdateTenantSubscriptionHandler`  
- Listado SuperAdmin: `SuperAdminController.GetTenants`  
- Menú sesión: `AccessController` + `GetSessionMenu` / `INavigationMenuAdminService` vs menú usuario

---

## 6. Anexo: checklist “clásico” (PlanId, `/empresas`, menú por planId) vs este repositorio

Muchas plantillas de ERP SaaS asumen **FK `PlanId` → tabla `Planes`**, rutas **`/api/superadmin/empresas`** y un JSON de menú por **`planId`**. Este monolito **no sigue ese esquema**: el contrato vigente está en las secciones 1–4. La tabla siguiente sirve para **auditar expectativas** sin confundir diseños.

| Checklist típico | En este repo |
|------------------|--------------|
| Tabla `tenants` con **`plan_id`** FK a **`planes`** | **`plan_code`** (string, hasta 64) + **`enabled_modules`** (JSON texto). Sin FK a `planes` legacy. Configuración: `TenantConfiguration` (`plan_code`, `enabled_modules`). |
| **`POST /api/superadmin/empresas`** con **`planId`** | **`POST /api/access/superadmin/tenants`** — cuerpo `SuperAdminCreateTenantWithAdminCommand`: **`planCode`**, **`enabledModules`**, datos opcionales de empresa, admin o vínculo a admin existente. |
| Al crear empresa: “guardar menú del plan” | Se persiste **plan + módulos contratados** en el tenant. El **árbol de menú** vive en **`ui_nav_*`** y la sesión usa **`GET /api/access/me/menu`**, filtrado por **permisos + módulos (+ features SaaS del plan)**. Ver `COMPANIES-PLAN-MENU-ADMIN.md`. |
| **`GET /api/superadmin/empresas`** → **`planNombre`** + métricas | **`GET /api/superadmin/tenants`** (y **`GET /api/access/superadmin/tenants`**). DTO **`SuperAdminTenantItemDto`**: `planCode`, `enabledModules`, `hasModuleRestrictions` — **no** incluye `planNombre`; el nombre se puede resolver en UI con **`GET /api/superadmin/plans`** (`getPlansCatalog`). |
| **`PUT .../empresas/{id}/plan`** | **`PATCH /api/tenants/{id}/subscription`** (`planCode`, `enabledModules`), rol SuperAdmin. |
| **`GET /api/superadmin/menu-config/{planId}`** | **No existe.** Menú de usuario: **`GET /api/access/me/menu`**. Árbol global editable: **`GET /api/superadmin/navigation-menu`**. |
| Tras impersonar, menú según plan de la empresa | Tras **`POST /api/auth/switch-tenant`**, el JWT y el `user` del cliente cambian. En **`AppLayout`**, si el usuario **no** es SuperAdmin en contexto global, se vuelve a cargar **`getSessionMenu()`** al cambiar `user`; el lateral filtra por **`enabledModules`** y permisos. |
| Tabs SuperAdmin: Empresas, Planes, Métricas | Pestañas actuales: **Resumen**, **Empresas**, **Features**, **Planes**; métricas en resumen y endpoints dedicados. No es el mismo nombre de tabs que el checklist genérico. |
| Formulario: **RUC único**, email válido | **Email** y **slug** sí se validan en el flujo de alta; **unicidad de RUC** no está implementada como regla dura en el handler SuperAdmin (índice único en BD hoy: **`slug`**). Si el producto lo exige, añadir validación + índice único condicional sobre `ruc`. |

Para ejemplos **`curl`** concretos (login, crear plan SaaS, crear tenant, `switch-tenant`, `me/menu`), ver **§7**.

### 6.1. Si vienes del checklist clásico: qué tocar (sin rehacer todo)

1. **No sustituir `plan_code` por `plan_id`** salvo decisión explícita de migración (ver **Fase D** en §3): implica migración de datos, handlers y UI.  
2. **Ampliar listado con nombre de plan**: opción A) enriquecer **`SuperAdminTenantItemDto`** en el backend desde catálogo SaaS; opción B) mantener resolución en frontend (patrón actual).  
3. **Unificar creación**: usar solo **`POST /api/access/superadmin/tenants`**; evitar duplicar un `POST .../empresas` salvo que sea **alias** que reenvíe al mismo comando.  
4. **Cambio de plan en panel**: ya cubierto por **`PATCH .../subscription`** + modal en **`SuperAdminPanelPage`**; un `PUT .../superadmin/tenants/{id}/subscription` sería opcional.  
5. **Menú “rol + plan”**: la pila real es **`/api/access/me/menu`** + snapshot de permisos + **`enabledModules`**; en cliente, orden típico: **módulos contratados / plan** → **permisos de rol** (ver `AppLayout`, `permissionsStore`).  
6. **RUC único**: implementar aparte (validación aplicación + restricción BD) si es requisito legal/operativo.

---

## 7. Equivalencia `curl`: plantilla genérica (`/planes`, `/empresas`, `impersonar`, `menu-config`) → este API

Las rutas del ejemplo genérico **no existen** en este monolito con esos paths ni ese cuerpo. Abajo, el flujo equivalente usando **`ApiResponse<T>`** (`success`, `message`, `responseObject`). Ajusta el host y el **puerto** (p. ej. `5003`) según `launchSettings` / despliegue.

**Cabeceras habituales**

```http
Content-Type: application/json
Authorization: Bearer <access_token>
```

**Extraer el token con `jq`** (el access token suele estar en `responseObject.token`):

```bash
TOKEN=$(curl -s ... | jq -r '.responseObject.token')
```

### 0. Login SuperAdmin global (token con `tenant_id` vacío)

No hay un único “superadmin_token” sin login: primero autenticar.

```bash
curl -s -X POST "http://localhost:5003/api/auth/superadmin-login" \
  -H "Content-Type: application/json" \
  -d '{"email":"<superadmin_email>","password":"<password>"}'
```

Respuesta: `responseObject` es **`AuthResponseDto`** (`token`, `tenantId` = `00000000-0000-0000-0000-000000000000`, `role` = `SuperAdmin`, etc.).

### 1. Crear un plan comercial (si no existe)

**No** uses `POST /api/superadmin/planes`. Aquí el CRUD es **`POST /api/superadmin/saas-plans`** y el cuerpo es **`CreateSaasPlanRequest`** (metadatos del plan; **no** incluye un array `modulos` — los módulos del tenant van en `enabledModules` al crear la empresa, y la composición “plan ↔ features de catálogo” se gestiona con **`PUT /api/superadmin/saas-plans/{planId}/features`**).

Requiere política **`GlobalSuperAdmin`**: JWT **SuperAdmin** y contexto de tenant **global** (Guid vacío), típico del token del paso 0.

```bash
curl -s -X POST "http://localhost:5003/api/superadmin/saas-plans" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "basico-test",
    "name": "Plan Básico",
    "shortLabel": "Básico",
    "isActive": true,
    "priceAmount": 50,
    "currency": "USD",
    "billingCycle": "monthly",
    "isPubliclyVisible": true,
    "isRecommended": false,
    "sortOrder": 10,
    "externalBillingRef": null
  }'
```

La respuesta devuelve `responseObject.id` (GUID del plan en `saas_plans`). Para asignar la empresa **no uses ese UUID como `planId` en el tenant**: el agregado usa **`planCode`** string; en el paso 2 envía **`"planCode": "basico-test"`** (el mismo `code`).

### 2. Crear una empresa con ese plan

**No** uses `POST /api/superadmin/empresas`. Ruta oficial: **`POST /api/access/superadmin/tenants`** con **`SuperAdminCreateTenantWithAdminCommand`** (nombres JSON en **camelCase**).

No existe `direccion` en este comando; datos legales opcionales incluyen `ruc`, `shortName`, `tradeName`, etc. El **`planCode`** debe existir y estar **activo** en catálogo. **`enabledModules`** es opcional: lista de claves (`inventario`, `ventas`, `accounting`, …) o `null`/omitido para sin restricción JSON.

```bash
curl -s -X POST "http://localhost:5003/api/access/superadmin/tenants" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantName": "Empresa Test",
    "tenantSlug": "empresa-test",
    "adminFirstName": "Admin",
    "adminLastName": "Local",
    "adminEmail": "admin@test.com",
    "adminPassword": "Test123!",
    "passwordResetMode": 0,
    "linkExistingAdmin": false,
    "ruc": "9999999999001",
    "planCode": "basico-test",
    "enabledModules": ["inventario", "ventas"]
  }'
```

`responseObject` es **`SessionResponseDto`**: guarda **`tenantId`** y **`token`** del admin recién creado si quieres probar como ese usuario; para impersonación SuperAdmin sigue el paso 3 con el **mismo** `$TOKEN` global del paso 0.

### 3. “Impersonar” (cambiar contexto de empresa)

**No** existe `POST /api/superadmin/impersonar` ni body `motivo`. Usa **`POST /api/auth/switch-tenant`** con el **JWT de sesión SuperAdmin** emitido tras **`superadmin-login`** (no el flujo bootstrap de usuario normal).

```bash
curl -s -X POST "http://localhost:5003/api/auth/switch-tenant" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"tenantId\":\"<guid_de_la_empresa>\"}"
```

`responseObject` es **`AuthResponseDto`** (nuevo `token` con `tenantId` de la empresa, `planCode` y `enabledModules` efectivos del tenant). Para volver al panel global, el mismo endpoint con **`tenantId`: `00000000-0000-0000-0000-000000000000`** (ver comentarios en `AuthController`).

### 4. Obtener el menú (no hay `menu-config/{planId}`)

**No** uses `GET /api/superadmin/menu-config/...`. Con el token ya “en contexto” de la empresa (paso 3):

```bash
curl -s "http://localhost:5003/api/access/me/menu" \
  -H "Authorization: Bearer $TOKEN_NUEVO"
```

La respuesta es una lista de grupos/ítems de menú para la sesión actual (rol + permisos + **`enabledModules`** / plan). No está parametrizada por un `planId` en la URL.

### Tabla rápida plantilla → repo

| Acción (plantilla) | Equivalente en este repo |
|--------------------|---------------------------|
| `POST .../superadmin/planes` | `POST .../superadmin/saas-plans` + cuerpo `CreateSaasPlanRequest` |
| `planId` en empresa | **`planCode`** (string) alineado con `saas_plans.code` |
| `POST .../superadmin/empresas` | `POST .../access/superadmin/tenants` |
| `POST .../superadmin/impersonar` + motivo | `POST .../auth/switch-tenant` + `{ "tenantId": "..." }` |
| `GET .../menu-config/{planId}` | `GET .../access/me/menu` (sesión actual) |

---

*Última revisión: mayo 2026 — Fases A y B implementadas en código; Fases C–D pendientes según secciones anteriores; anexo §6 añadido para alinear checklists externos; §7 equivalencias `curl`.*
