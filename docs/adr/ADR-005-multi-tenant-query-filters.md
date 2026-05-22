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

- ✅ Aislamiento por capa app + RLS donde aplica
- ⚠️ Catálogos globales (SRI, geografía, SaaS plans) exentos explícitos en config

## Alternatives Considered

- **Tenant solo en middleware manual:** rechazado (error humano)
- **DB por tenant:** rechazado (costo operativo SaaS)
