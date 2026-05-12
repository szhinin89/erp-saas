# Recuperación de contraseña (forgot + token)

## Resumen

- **Público recomendado:** `POST /api/auth/forgot-password` con `{ "email" }` → correo con enlace.
- **Enlace:** `{PublicBaseUrl}/reset-password?token=...` (SuperAdmin) o con `&tenantId={guid}` (usuario de empresa).
- **Completar:** `POST /api/auth/reset-password` con `{ "token", "newPassword", "tenantId"? }`. Para cuentas de empresa, `tenantId` debe coincidir con el del token; SuperAdmin no lo envía.
- **Compatibilidad:** `POST /api/auth/password-reset` sigue siendo el restablecimiento **directo** (tenant + email + nueva contraseña) cuando el tenant tiene el modo adecuado.

## Configuración

`appsettings.json` → sección `PasswordReset`:

- `PublicBaseUrl`: origen del SPA (ej. `https://tudominio.com` o `http://localhost:5173`).
- `TokenLifetimeMinutes`: por defecto 60; el token se invalida al usarse.

## Migración EF

`ERP.Infrastructure/Migrations/20260512161332_AddPasswordResetTokens.cs` crea la tabla `password_reset_tokens`.

## Frontend

| Ruta | Pantalla |
|------|----------|
| `/forgot-password` | Solo email → forgot-password |
| `/reset-password?token=...&tenantId=...` | Nueva contraseña → reset-password |
| `/password-reset` | Modo directo (sigue pidiendo ID de empresa) |

El interceptor HTTP trata `forgot-password` y `reset-password` como rutas de auth anónimas (sin refresh en 401).

## Pruebas manuales sugeridas

1. **SuperAdmin global:** forgot con su email → enlace sin `tenantId` → reset solo con token + contraseña → login.
2. **Usuario único por email (una empresa):** forgot → enlace con `tenantId` → reset con body que incluye el mismo `tenantId` → login.
3. **Mismo email en dos empresas (identity con dos membresías o dos legacy):** forgot → respuesta de error indicando contacto con soporte (no envía enlace).
