# ADR-005: Multi-tenant query filters

## Status

Accepted (2026-05)

## Context

Fuga de datos entre suscriptores es riesgo crítico. `TenantId`/`SubscriberId` debe venir del JWT, no del body. `IgnoreQueryFilters()` sin control permite lecturas cross-tenant.

## Decision

- Entidades operativas: `ISubscriberScopedEntity` + filtros globales EF
- Índices únicos compuestos con tenant/subscriber
- `IgnoreQueryFilters()` solo vía `IPlatformQueryAccessor` / allowlist documentada
- Frontend: sin UUID tenant en URL (`sessionStorage` `erp.saas.*`)

Enforcement: `check-backend-tenant-rules.mjs`, identity guardrails PS, tests.

## Consequences

- ✅ Aislamiento por capa app (query filters EF + `CompanyScopeBehavior`) — RLS a nivel PostgreSQL no implementado, ver [DATABASE.md#rls](../DATABASE.md#rls)
- ⚠️ Catálogos globales (SRI, geografía, SaaS plans) exentos explícitos en config

## Alternatives Considered

- **Tenant solo en middleware manual:** rechazado (error humano)
- **DB por tenant:** rechazado (costo operativo SaaS)

## Contexto histórico

Decisión original (ex `docs/decisions/ADR-004`, superseded): el modelo multi-tenant nació con `SubscriberId` en entidades de negocio, filtros globales en `ErpDbContext`, índices únicos compuestos `(SubscriberId, Code)` y JWT como fuente de tenant en la API. La consolidación `SubscriberId → TenantId` (FASE 4, ver [`STATUS.md`](../../STATUS.md)) y la eliminación de SuperAdmin/Platform (ver [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md)) actualizaron la nomenclatura; los principios de aislamiento por filtros globales y JWT-as-source-of-tenant siguen vigentes en esta ADR-005.
