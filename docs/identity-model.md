# Identity Model (IAM único)

## Tabla canónica: `identity_users`

Todos los actores autenticables viven aquí. No existe tabla `users` en el estado final.

### Campos clave

| Columna | Tipo | Descripción |
|---------|------|-------------|
| `user_type` | enum string | `Platform` \| `Company` \| `Subscriber` (future) |
| `platform_role` | enum nullable | `SuperAdmin` \| `Support` \| `BillingAdmin` |
| `subscriber_id` | uuid nullable | Opcional hint; **Platform debe ser NULL** |
| `email_normalized` | string | Índice único para lookup |
| `security_stamp` | string | Revocación de sesiones al rotar |
| `require_password_reset` | bool | Fuerza reset seguro post-migración |

### Invariantes

```sql
CHECK (user_type <> 'Platform' OR subscriber_id IS NULL)
```

- Platform users **no** requieren `company_id`.
- Acceso ERP vía `company_user_memberships` (solo `user_type=Company`).
- `CompanyScopeBehavior` hace bypass solo para SuperAdmin platform (`subscriber_id=Guid.Empty`).

### Factory methods (dominio)

- `IdentityUser.CreateCompanyUser(...)` — usuarios ERP
- `IdentityUser.CreatePlatformSuperAdmin(...)` — operadores globales
- `IdentityUser.Create(...)` — alias legacy → `CreateCompanyUser`

### JWT claims unificados

| Claim | Platform SuperAdmin | Company user |
|-------|---------------------|--------------|
| `sub` | `identity_user_id` | `identity_user_id` |
| `user_type` | `Platform` | `Company` |
| `platform_role` | `SuperAdmin` | — |
| `subscriber_id` | `00000000-...` o impersonation | subscriber activo |
| `company_id` | opcional (impersonation) | empresa operativa |

Ver [auth-unified.md](./auth-unified.md) y [identity-migration.md](./identity-migration.md).
