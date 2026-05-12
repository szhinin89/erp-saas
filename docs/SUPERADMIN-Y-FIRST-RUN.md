# SuperAdmin, first-run y contexto de empresa

Documento de referencia para **comportamiento real** del backend y scripts (no un diseño futuro).

## Resumen

| Concepto | Implementación actual |
|----------|------------------------|
| **Primer SuperAdmin** | Un solo usuario con `role = 'SuperAdmin'` en tabla `users` y `tenant_id` vacío. Se crea con token efímero (hash en tabla **`first_run_setup_state`**, texto plano solo en consola al arrancar la API). |
| **Token de instalación** | **No** se valida contra `Deployment:InitialSuperAdminSetupToken` en el flujo de claim. Esa clave de configuración existe en código (`AuthorizeInitialSuperAdminSetup`) pero **no** la usa `ClaimInitialSuperAdminHandler`. |
| **Endpoints de alta** | `POST /api/setup/superadmin` (principal). Alias: `POST /api/setup/claim-initial-superadmin` (mismo cuerpo; oculto en Swagger). |
| **Login global SuperAdmin** | `POST /api/auth/superadmin-login` → JWT con `tenant_id = 00000000-0000-0000-0000-000000000000`. |
| **“Entrar” a una empresa** | `POST /api/auth/switch-tenant` con `Authorization: Bearer` del SuperAdmin y body `{ "tenantId": "<guid>" }`. Emite **nuevo JWT** con ese `tenant_id`. **No** hay tabla de auditoría de impersonación, claim `is_impersonating` ni campo obligatorio de motivo. |
| **Volver al panel global** | Mismo endpoint: `{ "tenantId": "00000000-0000-0000-0000-000000000000" }` (Guid vacío). |
| **Admin multi-empresa (no SuperAdmin)** | Flujo distinto: `POST /api/access/bootstrap-login` → `POST /api/access/switch-tenant` con política **Bootstrap** (token de corta duración). Ver `AccessController`. |
| **Vida del JWT** | `Jwt:ExpirationMinutes` en configuración (por defecto **60** en `JwtService`). |
| **Token first-run: caducidad** | **15 minutos** desde emisión o rotación. |

## Secuencia recomendada (instalación)

1. Base de datos creada y migraciones aplicadas (`dotnet ef database update`).
2. Arrancar **ERP.API**. Si no existe SuperAdmin y first-run sigue activo, la consola del proceso muestra un bloque **FIRST-RUN DETECTADO** con un `curl` de ejemplo y el **token en claro** (copiarlo de la salida; no se vuelve a mostrar).
3. Llamar a setup con el JSON documentado abajo (o usar `scripts/create-superadmin.ps1 -SetupToken "<pegar-token-consola>"`).
4. Iniciar sesión: `POST /api/auth/superadmin-login`.
5. Para operar datos de una empresa: `POST /api/auth/switch-tenant` con el `tenantId` deseado.

## Cuerpo JSON del claim (alta SuperAdmin)

Propiedades en **camelCase** (ASP.NET Core):

```json
{
  "setupToken": "<token de la consola del servidor>",
  "firstName": "Super",
  "lastName": "Admin",
  "email": "superadmin@ejemplo.com",
  "password": "MínimoSegúnValidación"
}
```

- `firstName` y `lastName` son **obligatorios** (no un solo campo “nombre”).
- Tras éxito, la fila en `first_run_setup_state` queda con `is_first_run = false`, `completed_at` en UTC, y sin hash ni expiración de token.

## Tabla `first_run_setup_state` (PostgreSQL)

| Columna (BD) | Uso |
|----------------|-----|
| `id` | PK de la fila única de estado. |
| `is_first_run` | `true` mientras no exista SuperAdmin o se haya rehecho el reset. |
| `setup_token_hash` | SHA-256 en hexadecimal del token mostrado en consola; `NULL` si no hay token activo o ya se completó. |
| `setup_token_expiry_utc` | Caducidad del token (15 min al emitir). |
| `completed_at` | **UTC** en que se completó el alta del SuperAdmin; `NULL` si aún no. |
| `created_at`, `updated_at`, `created_by`, `updated_by` | Auditoría (`SystemAuditableEntity`). |

No existen columnas `token_hash` ni `token_expires_at` en esta tabla (esos nombres son de otras entidades, p. ej. `refresh_tokens.token_hash`).

### SQL manual de reset (solo si no usas `POST /api/dev/reset-first-run`)

Ver `scripts/sql/reset-first-run-state.sql`. Resumen: borrar refresh tokens y filas `users` con `role = 'SuperAdmin'`, luego poner `first_run_setup_state` en modo first-run y limpiar `setup_token_hash`, `setup_token_expiry_utc` y `completed_at`.

Migración que añade `completed_at`: `AddCompletedAtToFirstRunSetupState` (`dotnet ef database update`).


Solo con ambiente **Development**:

- `POST /api/dev/reset-first-run` (sin auth en el código actual) elimina SuperAdmins existentes, reabre first-run y devuelve un **nuevo** `setupToken` en el JSON de respuesta útil para pruebas automatizadas o scripts.

## Scripts PowerShell

- `scripts/create-superadmin.ps1` — POST a `/api/setup/superadmin`; el parámetro `-SetupToken` debe ser el token **mostrado por la API en consola** (o el devuelto por reset first-run), no un valor inventado en user-secrets salvo que coincida casualmente (no recomendado).
- `scripts/create-superadmin-interactive.ps1` — asistente que pide el mismo token pegado desde consola.
- `Crear-SuperAdmin.ps1` (raíz del repo `erp-saas`) — flujo interactivo en español; usa `claim-initial-superadmin` y comprueba el login leyendo `responseObject.token` del `ApiResponse`.
- `scripts/sql/reset-first-run-state.sql` — reset manual en PostgreSQL (`first_run_setup_state` + `users` / `refresh_tokens`), alineado con nombres reales de columnas.

## Panel frontend y UX

- Rutas bajo `/superadmin` (ver `FRONTEND-PANTALLAS.md`).
- Cualquier etiqueta de “impersonación” en UI que no llame a `POST /api/auth/switch-tenant` es solo presentación; la fuente de verdad es el **claim `tenant_id`** del JWT actual.

## Qué **no** está en el código (posible evolución)

- Registro dedicado `ImpersonacionLog` / motivo obligatorio.
- JWT de duración fija distinta de `Jwt:ExpirationMinutes` solo para “sesión impersonada”.
- Semilla automática de bodega o plan contable al crear tenant (revisar handler concreto de creación de empresa si se documenta ese alcance).

## Archivos útiles

| Área | Archivo |
|------|---------|
| Emisión/validación token first-run | `ERP.Infrastructure/Deployment/FirstRunSetupService.cs` |
| Claim SuperAdmin | `ERP.Application/.../ClaimInitialSuperAdmin/ClaimInitialSuperAdminHandler.cs` |
| API setup | `ERP.API/Controllers/SetupController.cs` |
| Switch tenant SuperAdmin | `ERP.API/Controllers/AuthController.cs` (`switch-tenant`), `ERP.Application/.../Auth/UseCases/SwitchTenant/SwitchTenantHandler.cs` |
| Switch tenant Admin (bootstrap) | `ERP.API/Controllers/AccessController.cs` |
| Mensaje consola first-run | `ERP.API/Program.cs` |
| Panel, planes, menú comercial | [`docs/GUIA-SUPERADMIN-PANEL-Y-PLANES.md`](GUIA-SUPERADMIN-PANEL-Y-PLANES.md), [`docs/COMPANIES-PLAN-MENU-ADMIN.md`](COMPANIES-PLAN-MENU-ADMIN.md) |
