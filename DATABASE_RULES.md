# Database Rules

> Canónico: [`docs/DATABASE.md`](docs/DATABASE.md)

## Obligatorio

1. **PostgreSQL** único motor relacional producto.
2. **Migraciones EF** en `ERP.Infrastructure/Migrations/` — no SQL manual salvo ops documentado.
3. **Configuración:** `IEntityTypeConfiguration` por entidad.
4. **Unicidad multi-tenant:** índices `(SubscriberId, …)`.
5. **Soft delete:** `IsActive = false`; no DELETE físico de negocio.
6. **RLS:** donde esté habilitado, respetar session vars vía interceptor.

## Arranque local

```powershell
docker compose up -d
cd backend/src
dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
```

Puerto Postgres local: **5435**.

## Ops SQL

- EF migrations: canónico
- Scripts ops: `infrastructure/postgres/`, `scripts/db/sql/` (excepcional)
