# ADR-005: PostgreSQL como única base relacional

## Estado
Aceptado

## Contexto
Stack oficial: Docker Compose local, EF Core, RLS parcial en tablas operativas.

## Decisión
**PostgreSQL 16** + **Npgsql** + migraciones EF. Sin SQL Server en producto (patrón bloqueado en allowlist).

## Consecuencias
- ✅ `ExecuteUpdateAsync` para rotación atómica refresh
- ✅ RLS en inventario/ventas core
- ⚠️ Scripts ops SQL en `infrastructure/postgres/` y `scripts/db/`

## Referencias
- [`docs/DATABASE.md`](../DATABASE.md)
