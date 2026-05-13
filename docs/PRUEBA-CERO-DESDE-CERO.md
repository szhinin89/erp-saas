# Prueba completa desde cero (BD vacía + flujo SuperAdmin → empresa → admin local)

Esta guía describe el flujo **manual** y el **mapeo real** del producto (claves de módulo, menú estático vs BD, scripts).

## Prerrequisitos

- PostgreSQL accesible; cadena en `appsettings.Development.json` → `ConnectionStrings:DefaultConnection`.
- `Development:SeedDemoTenant` en **`false`** (por defecto) para no crear `tenant-demo`.
- Esquema aplicado: desde `ERP.API`:

  `dotnet ef database update --project ..\ERP.Infrastructure\ERP.Infrastructure.csproj`

- API en **Development** para usar `POST /api/dev/reset-first-run` (obtiene token sin leer consola).

## 1. Arrancar la API y token first-run

1. `dotnet run` en `backend/src/ERP.API`.
2. Si la BD no tiene SuperAdmin y first-run está pendiente, en consola del servidor aparece el bloque **FIRST-RUN** con `setupToken`.
3. **Alternativa (solo Development):**

   `POST http://localhost:5003/api/dev/reset-first-run`  
   Cuerpo vacío. La respuesta incluye `setupToken` y `expiresAtUtc` (reinicia first-run y borra SuperAdmins previos en modo dev).

## 2. Crear SuperAdmin

**Opción A — script interactivo:** `.\Crear-SuperAdmin.ps1` en la raíz `erp-saas`.

**Opción B — HTTP:**

`POST /api/setup/superadmin` (o alias `POST /api/setup/claim-initial-superadmin`)

```json
{
  "setupToken": "<token>",
  "firstName": "Super",
  "lastName": "Admin",
  "email": "superadmin@test.local",
  "password": "ClaveSegura123!"
}
```

Contraseña: misma regla que la API (mínimo 10 caracteres en scripts actuales).

Tras un claim exitoso, en BD debe existir el usuario SuperAdmin en **`users`** (rol SuperAdmin, sin empresa operativa en JWT global).

## 3. Login SuperAdmin

`POST /api/auth/superadmin-login`

```json
{ "email": "superadmin@test.local", "password": "ClaveSegura123!" }
```

Respuesta: `responseObject.token` (JWT). Úsalo como `Authorization: Bearer …` en los pasos siguientes.

## 4. Panel SuperAdmin: sin empresas ni planes

- UI: `https://localhost:5173/superadmin` (o tu origen SPA).
- **Empresas:** `GET /api/superadmin/tenants` → lista vacía.
- **Planes (CRUD administrable):** `GET /api/superadmin/saas-plans` → lista vacía al inicio.  
  (El catálogo de lectura `GET /api/superadmin/planes` también refleja planes creados.)

## 5. Crear plan comercial «Básico»

`POST /api/superadmin/saas-plans` (requiere JWT SuperAdmin global)

Ejemplo:

```json
{
  "code": "basico",
  "name": "Básico",
  "shortLabel": "BAS",
  "isActive": true,
  "priceAmount": 29.99,
  "currency": "USD",
  "billingCycle": "monthly",
  "isPubliclyVisible": true,
  "isRecommended": false,
  "sortOrder": 0,
  "externalBillingRef": null
}
```

Las **features por plan** (`PUT /api/superadmin/saas-plans/{id}/features`) operan sobre filas de `saas_feature_definitions`. En BD totalmente vacía **no hay definiciones**; para una primera prueba basta el plan con `code` **basico** y la **restricción de módulos en el tenant** (paso 6). Más adelante se pueden definir features y enlazarlas al plan desde la pestaña **Planes** / **Características**.

## 6. Crear empresa + admin local (solo tenant + identity + membership)

`POST /api/access/superadmin/tenants` con cabecera `Authorization: Bearer <jwt superadmin>`.

Mapeo respecto a “Bodegas + Productos” en el código:

| Concepto negocio | Clave módulo suscripción |
|------------------|---------------------------|
| Catálogo / productos / bodegas de inventario | `inventario` |
| Sucursales (pantalla **Sucursales** en Configuración) | `saas` |

Ejemplo con plan obligatorio y solo esos módulos:

```json
{
  "tenantName": "Empresa Demo",
  "tenantSlug": "empresa-demo",
  "adminFirstName": "Local",
  "adminLastName": "Admin",
  "adminEmail": "admin@empresa-demo.local",
  "adminPassword": "OtraClaveSegura1!",
  "passwordResetMode": 2,
  "linkExistingAdmin": false,
  "planCode": "basico",
  "enabledModules": ["inventario", "saas"]
}
```

- `planCode` debe coincidir con un plan **activo** del catálogo.
- `enabledModules` no vacío → el tenant queda restringido a esas claves (no se crean bodegas, cuentas ni catálogos por defecto).

Verificar listado: `GET /api/superadmin/tenants`.

## 7. Logout SuperAdmin y login como administrador local

- Cerrar sesión en la UI o dejar de enviar el JWT de SuperAdmin.
- `POST /api/auth/login`:

```json
{ "email": "admin@empresa-demo.local", "password": "OtraClaveSegura1!" }
```

El backend resuelve tenant y devuelve JWT con `enabledModules` acotados.

## 8. Menú esperado (fallback estático sin filas en `ui_nav_*`)

Si la BD **no** tiene menú persistido, el SPA usa `buildNavGroups` y filtra por `enabledModules`:

- **Inicio** (siempre).
- **Inventario** (módulo `inventario`): ítems de catálogo; listas vacías hasta que el admin cree datos.
- **Configuración** → **Sucursales** (`moduleKey` **saas**, permiso `saas.branches.view`): sin sucursales hasta que las cree.
- No deberían mostrarse grupos **Contabilidad**, **Ventas**, **Compras**, **RRHH** si no están en `enabledModules`.
- **Accesos** / **Perfiles** (`access`) **no** aparecen si `access` no está contratado.

> Nota: antes del ajuste de suscripción estricta, tener solo `inventario` implicaba también `ventas`, `compras` y `gastos` por compatibilidad; eso ya **no** aplica: solo los módulos listados en JSON.

## 9. SuperAdmin puede seguir creando planes o empresas

Misma sesión SuperAdmin o login de nuevo: CRUD planes (`/api/superadmin/saas-plans`) y alta de empresas (`POST /api/access/superadmin/tenants`).

## Automatización opcional

Script de ejemplo (PowerShell) que encadena reset first-run (dev), claim, login, crear plan y crear empresa:

**`scripts/prueba-desde-cero.ps1`**

```powershell
pwsh -File scripts/prueba-desde-cero.ps1 -ApiBaseUrl "http://localhost:5003"
```

Parámetros adicionales: `-SuperAdminEmail`, `-SuperAdminPassword`, `-TenantAdminEmail`, `-TenantAdminPassword`. Revisar TLS si usas `https://localhost:5001`.
