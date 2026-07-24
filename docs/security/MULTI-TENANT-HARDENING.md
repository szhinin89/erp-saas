# Multi-tenant hardening (enterprise)

**Updated:** 2026-05-23 (revisado 2026-07-23 — naming Subscriber→Tenant corregido tras verificación contra código real)

## Fail-closed query filters

- Entidades `ITenantScopedEntity` / `ICompanyScopedEntity` filtradas en `ErpDbContext`.
- Sin `tenant_id` o `company_id` válidos en JWT → 0 filas (no error explícito en lectura).
- Bypass solo vía `IPlatformQueryAccessor.Unfiltered` con `PlatformQueryReason` documentado.

## Tenant vs Company

| Scope | JWT | Ejemplos |
|-------|-----|----------|
| Tenant | `tenant_id` | BusinessPartner, CustomerProfile, SupplierProfile |
| Company | `tenant_id` + `company_id` | Customer, SalesBill, CompanyBpSettings |

## Explicit request markers

| Interface | Uso |
|-----------|-----|
| `ICompanyScopedRequest` | Operaciones ERP con empresa activa |
| `IRequiresCompanyContext` | Extiende `ICompanyScopedRequest`; exige membresía activa validada (`RequireCurrentCompanyAsync`) — ej. `GetSessionMenuQuery` (`/api/me/menu`) |
| `ITenantScopedRequest` | MasterData BP global al tenant |
| `IPlatformScopedRequest` | Jobs cross-tenant / plataforma |

`CompanyScopeBehavior`: interfaces explícitas primero; **namespace-prefix** solo fallback legacy (warning + métrica `security.namespace_fallback_used`).

## Pendientes detectados (no implementar sin ticket propio)

- **SRI Configuration use cases** (`GetSriConfigurationQuery` / `UpsertSriConfigurationCommand`, namespace `ERP.Application.Configuration.UseCases.{GetSriSettings,UpsertSriSettings}`): namespace no coincide con el prefijo `ERP.Application.Modules.Configuration` de `AR_SEC_4` (`SecurityArchitectureTests`), por lo que escapan la verificación de scope marker. Ninguno implementa `ICompanyScopedRequest`; los handlers leen `ICurrentCompany.CompanyId` (header sin validar) directamente. Actualmente sin endpoint en `ERP.API` (dead code). Si se reactivan, deben: (1) corregir el namespace para entrar en `AR_SEC_4`, y (2) implementar `ICompanyScopedRequest`/`IRequiresCompanyContext`.

## Concurrency (PostgreSQL)

Handlers con patrón check-then-insert capturan `DbUpdateException` → `PostgresException` SqlState `23505` → `Result.Conflict` / `Result.UniqueViolation` (HTTP **409**).

Implementación: `IDatabaseExceptionTranslator` (Infrastructure).

## Reconciliation (READ-ONLY)

- `IMasterDataReconciliationService` detecta drift legacy ↔ BP.
- Health: `/health/masterdata-reconciliation`
- Hangfire: `masterdata-reconciliation` (diario 03:00) — **no autocorrige**.

## Dual-write (BP-3 / BP-4)

Best-effort tras persistir legacy. Fallos incrementan `masterdata.dualwrite_failed`; races UNIQUE incrementan `masterdata.sync_inconsistency`.
