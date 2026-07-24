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
