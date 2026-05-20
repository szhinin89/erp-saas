# Company Management

Fiscal companies under a subscriber. Distinct from SuperAdmin **subscriber** administration (`/companies` = subscribers).

## Domain

`Company`: `SubscriberId`, RUC, legal name, `Timezone`, `CurrencyCode`, `LogoUrl`, `BrandingJson`.

`CompanyUserMembership`: `company_id` + `identity_user_id` (unique per pair).

## Application (`ERP.Application.Modules.Platform.Companies`)

| Use case | Permission |
|----------|------------|
| List accessible companies | membership ∩ `subscriber_id` |
| Get by id | membership on target |
| Get current | JWT `company_id` + re-validation |
| Create | `perm:saas.companies.create` + `ICommercialPlanLimitService` |
| Update profile | `perm:saas.companies.update` + membership |

## Dependencies

```
CompaniesController
  → ICompanyAccessGuard
  → ICompanyProvisioningService (create + MAX_COMPANIES)
  → ICompanyRepository
  → ICommercialPlanLimitService
```

## API

| Route | Notes |
|-------|-------|
| `GET /api/companies` | Companies user can access |
| `GET /api/companies/current` | Active company from JWT |
| `GET /api/companies/{id}` | Detail with guard |
| `POST /api/companies` | Provisioning + limit |
| `PUT /api/companies/{id}` | Profile update |

Auth: `POST /api/auth/switch-company`, `GET /api/auth/my-companies`.

## Frontend

| Route | Purpose |
|-------|---------|
| `/saas/companies` | Hub |
| `/saas/companies/new` | Create |
| `/saas/companies/:id` | Edit |
| `/select-company` | Multi-company picker |
| `CompanySwitcher` | Header context switch |

## Security

- Global unique RUC check before create
- Never trust body `company_id` without `ICompanyAccessGuard`
- SuperAdmin `/companies` manages **subscribers**, not fiscal `company` rows

## Login flow

1. Login → `subscriber_id` in token
2. One company → auto `company_id`
3. Many → `/select-company` → switch-company
4. ERP modules require `company_id` in JWT
