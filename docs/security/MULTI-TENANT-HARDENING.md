# Multi-tenant hardening (enterprise)

**Updated:** 2026-05-23

## Fail-closed query filters

- Entidades `ISubscriberScopedEntity` / `ICompanyScopedEntity` filtradas en `ErpDbContext`.
- Sin `subscriber_id` o `company_id` válidos en JWT → 0 filas (no error explícito en lectura).
- Bypass solo vía `IPlatformQueryAccessor.Unfiltered` con `PlatformQueryReason` documentado.

## Subscriber vs Company

| Scope | JWT | Ejemplos |
|-------|-----|----------|
| Subscriber | `subscriber_id` | BusinessPartner, CustomerProfile, SupplierProfile |
| Company | `subscriber_id` + `company_id` | Customer, SalesBill, CompanyBpSettings |

## Explicit request markers

| Interface | Uso |
|-----------|-----|
| `ICompanyScopedRequest` | Operaciones ERP con empresa activa |
| `ISubscriberScopedRequest` | MasterData BP global al subscriber |
| `IPlatformScopedRequest` | Jobs cross-tenant / plataforma |

`CompanyScopeBehavior`: interfaces explícitas primero; **namespace-prefix** solo fallback legacy (warning + métrica `security.namespace_fallback_used`).

## Concurrency (PostgreSQL)

Handlers con patrón check-then-insert capturan `DbUpdateException` → `PostgresException` SqlState `23505` → `Result.Conflict` / `Result.UniqueViolation` (HTTP **409**).

Implementación: `IDatabaseExceptionTranslator` (Infrastructure).

## Reconciliation (READ-ONLY)

- `IMasterDataReconciliationService` detecta drift legacy ↔ BP.
- Health: `/health/masterdata-reconciliation`
- Hangfire: `masterdata-reconciliation` (diario 03:00) — **no autocorrige**.

## Dual-write (BP-3 / BP-4)

Best-effort tras persistir legacy. Fallos incrementan `masterdata.dualwrite_failed`; races UNIQUE incrementan `masterdata.sync_inconsistency`.
