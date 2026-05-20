# Enterprise Smoke Tests (Post-Baseline)

Manual/E2E checklist. Base URL dev: `https://localhost:5001`.

## AUTH

| # | Test | Expected |
|---|------|----------|
| A1 | `POST /api/auth/login` (identity user) | 200 + tokens |
| A2 | `POST /api/auth/refresh` | 200 + rotated refresh |
| A3 | `POST /api/auth/logout` | 204/200, refresh revoked |
| A4 | Bootstrap multi-subscriber → select → `POST /api/auth/switch-subscriber` | Session with `subscriber_id` |
| A5 | `POST /api/auth/switch-company` | JWT includes `company_id` |

## MULTIEMPRESA

| # | Test | Expected |
|---|------|----------|
| M1 | `GET /api/companies` | Lista empresas del suscriptor |
| M2 | `POST /api/companies` | Crea empresa; respeta `MAX_COMPANIES` |
| M3 | `PUT /api/companies/{id}` | Actualiza profile/branding |
| M4 | Membership activa en empresa B | Acceso OK solo con switch a B |
| M5 | JWT `company_id` ≠ empresa solicitada | 403 `company_access_denied` |

## BILLING

| # | Test | Expected |
|---|------|----------|
| B1 | `GET /api/saas/billing/account` | Cuenta billing del subscriber |
| B2 | `GET /api/saas/billing/invoices` | Lista (puede vacía) |
| B3 | `GET /api/saas/billing/events` | Auditoría governance |
| B4 | Suspend billing (admin/governance) | 403 en operaciones con subscriber activo |

## SECURITY

| # | Test | Expected |
|---|------|----------|
| S1 | Request sin JWT | 401 |
| S2 | JWT manipulado | 401 |
| S3 | `company_id` de otro subscriber | 403 |
| S4 | Endpoint `[Authorize]` sin permiso | 403 |
| S5 | SuperAdmin sin `subscriber_id` | Acceso panel; bypass company scope |

## ENTITLEMENTS

| # | Test | Expected |
|---|------|----------|
| E1 | `GET` entitlements / session modules | Features del plan |
| E2 | Exceder `MAX_COMPANIES` | 403 commercial limit |
| E3 | Cambio de plan → invalidar cache | Snapshot actualizado tras TTL/invalidación |

## OLEADA 1 ERP (company_id)

| # | Test | Expected |
|---|------|----------|
| W1 | Crear producto con company en JWT | `products.company_id` poblado |
| W2 | RLS: query sin `app.subscriber_id` en sesión DB | Sin filas (excepto platform admin) |

## Comandos útiles

```bash
curl -k https://localhost:5001/health/ready
dotnet ef migrations has-pending-model-changes --startup-project backend/src/ERP.API/ERP.API.csproj
```
