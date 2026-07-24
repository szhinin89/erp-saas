# ADR-015: PostgreSQL como única base relacional

## Estado
Aceptado

## Contexto
Stack oficial: Docker Compose local, EF Core. RLS no implementado — ver [DATABASE.md#rls](../DATABASE.md#rls).

## Decisión
**PostgreSQL 16** + **Npgsql** + migraciones EF. Sin SQL Server en producto (patrón bloqueado en allowlist).

## Consecuencias
- ✅ `ExecuteUpdateAsync` para rotación atómica refresh
- ❌ RLS no implementado (ver [DATABASE.md#rls](../DATABASE.md#rls)) — aislamiento real vigente vía filtros EF + `CompanyScopeBehavior`
- ⚠️ Scripts ops SQL en `infrastructure/postgres/` y `scripts/db/`

## Referencias
- [`docs/DATABASE.md`](../DATABASE.md)
