# Observability — métricas de seguridad

**Endpoint Prometheus:** `/metrics` (si `Observability:EnablePrometheus=true`)

## Meter `ERP.Security`

| Métrica | Evento |
|---------|--------|
| `security.cross_company_denied` | Empresa fuera del tenant |
| `security.membership_validation_failed` | Membership inválida |
| `security.invalid_company_context` | Sin empresa operativa en JWT |
| `security.jwt_refresh_revoked` | Refresh inválido / reuse / revocado |
| `security.permission_denied` | HTTP 401/403 |
| `security.namespace_fallback_used` | Handler usa solo namespace-prefix |
| `masterdata.dualwrite_failed` | BP-3/BP-4 dual-write error |
| `masterdata.sync_inconsistency` | Race UNIQUE en dual-write |
| `background.context_leak_detected` | AsyncLocal sucio fuera de job |

## Tags permitidos

`tenant_id`, `company_id`, `endpoint`, `request_type`, `correlation_id`

**Prohibido en métricas/logs:** JWT, emails, passwords, secrets.

## Log scope (HTTP)

`SecurityCorrelationMiddleware`: TenantId, CompanyId, UserId, SessionId, RequestId.

## Meter `ERP.Jobs` (ADR-043)

| Metric | Instrument | Meaning |
|---|---|---|
| `erp.jobs.duration` | Histogram, s | Whole execution duration |
| `erp.jobs.processed` | Counter | Completed candidates/attempts |
| `erp.jobs.failed` | Counter | Candidate failures or execution errors |
| `erp.jobs.lock_skipped` | Counter | Nonblocking job lock already held |
| `erp.jobs.backlog` | Gauge | Pending candidate count, SQL aggregate |
| `erp.jobs.oldest_pending_age` | Gauge, s | Age of oldest pending candidate; zero if empty |

Tags: `job`; backlog/age also `stream` (three streams for retention recovery).
No tenant, email, document ID or secret labels. Candidate failure and infrastructure
failure may both count if an execution cannot continue. SMTP processed means a claim
was handled; delivery failure increments failed separately. SQL snapshots are taken
at discovery, so gauges describe that instant, before processing the batch.

API Prometheus subscribes to ERP.Jobs. A separate worker emits the Meter without
opening an HTTP endpoint; exporting it requires an in-process collector configured
for that deployment. Never query all candidate entities to compute backlog.
