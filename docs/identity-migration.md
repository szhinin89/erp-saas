# Identity migration (`users` → `identity_users`)

Estado **final**: tabla `users` eliminada; autenticación única en `identity_users`. Ver también [identity-model.md](./identity-model.md), [auth-unified.md](./auth-unified.md), [frontend-identity.md](./frontend-identity.md).

## Cadena EF (orden obligatorio)

| Migración | Propósito |
|-----------|-----------|
| `20260520215307_InitialEnterpriseBaseline` | Schema enterprise + RLS |
| `20260520223659_Wave1InventoryCompanyScope` | `company_id` inventario |
| `20260520230443_Wave2SalesCompanyScope` | `company_id` ventas |
| `20260521004512_EnterpriseOnboardingCompanyTaxId` | RUC provisional / tax id status |
| `20260521020928_IdentityUnificationPlatformUsers` | IAM en `identity_users` + backfill desde `users` |
| `20260521021016_DropLegacyUsersAuth` | `DROP TABLE users` |

Política general: [DATABASE/MIGRATIONS.md](./DATABASE/MIGRATIONS.md).

```bash
cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
```

## `IdentityUnificationPlatformUsers`

1. Agrega columnas IAM a `identity_users` (`email_normalized`, `platform_role`, `security_stamp`, `require_password_reset`, …).
2. Backfill filas existentes (`user_type=Company`).
3. Inserta SuperAdmins desde `users` (`Platform` / `SuperAdmin`, `subscriber_id=NULL`).
4. Inserta usuarios legacy no-SuperAdmin + memberships en primera company del subscriber.
5. Invalida refresh tokens `SuperAdmin` y `Legacy` (`reason_revoked=IdentityMigration`).
6. Marca usados password reset tokens `SuperAdmin` / `Legacy`.
7. Aplica `CHECK`: platform users sin `subscriber_id`.

### Mapping `users` → `identity_users`

| Campo legacy | Campo target | Notas |
|--------------|--------------|-------|
| `id` | `id` | Mismo UUID si no hay colisión de email |
| `email` | `email` + `email_normalized` | `LOWER(TRIM(email))` |
| `password_hash` | `password_hash` | BCrypt — reutilizable |
| `role = SuperAdmin` | `user_type=Platform`, `platform_role=SuperAdmin` | `subscriber_id=NULL` |
| `role != SuperAdmin` | `user_type=Company` | Membership en `company_user_memberships` |

### Contraseñas

- Hashes BCrypt existentes se **copian** tal cual.
- Hash no verificable → `require_password_reset=true` + `POST /api/auth/forgot-password`.

### Pre-requisitos antes de `DropLegacyUsersAuth`

- Forzar re-login platform (refresh legacy revocado).
- `SELECT COUNT(*) FROM users` debe ser coherente con backfill.
- Aplicación sin `DbSet<User>`, `IUserRepository`, `IJwtService`.

## `DropLegacyUsersAuth`

- `DROP TABLE users`
- Código: sin entidad `User`, repositorio ni JWT legacy.

## Endpoints post-migración

| Canónico | Alias legacy |
|----------|--------------|
| `POST /api/platform/auth/login` | `POST /api/auth/superadmin-login` |
| `POST /api/auth/refresh` | único refresh (platform + company) |
| `POST /api/setup/superadmin` | crea SuperAdmin en `identity_users` |

Setup token: efímero en `first_run_setup_state` (~15 min), **no** `Deployment:InitialSuperAdminSetupToken`.

## CI guardrails

Script: `scripts/check-identity-guardrails.ps1` (job `identity-guardrails` en CI).

Patrones prohibidos en código productivo:

- `ERP.Domain.Auth.Entities.User`
- `IUserRepository`, `IJwtService`
- `ToTable("users")`, `_context.Users`, `DbSet<User>`

## Scripts SQL manuales

- Repunte auth `users` → `identity_users`: cubierto por migración EF (no script manual).
- Documentos: `scripts/sql/002_unified_documents_schema_and_migration.sql` (dominio documentos, no auth).
