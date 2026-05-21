# ADR-004: Multi-tenant con SubscriberId y filtros EF

## Estado
Aceptado

## Contexto
Datos aislados por suscriptor (tenant SaaS). SuperAdmin con scope platform.

## Decisión
- `TenantId`/`SubscriberId` en entidades de negocio
- Query filters globales en `ErpDbContext`
- Índices únicos compuestos `(SubscriberId, Code)`
- JWT como fuente de tenant en API

## Consecuencias
- ✅ Aislamiento por convención
- ⚠️ Entidades platform (SuperAdmin) excluidas explícitamente del filtro tenant

## Referencias
- [`docs/DATABASE.md`](../DATABASE.md)
- [`DATABASE_RULES.md`](../../DATABASE_RULES.md)
