# Phase — Company Management Module

## Impact analysis

| Area | Impact |
|------|--------|
| SaaS Platform | New `/api/companies` CQRS surface; `Company` profile fields; centralized access guard |
| ERP Runtime | Unchanged — branches, SRI, sales remain `company_id`-scoped in Fase 6 |
| Auth | Reuses `switch-company`, JWT `company_id`; no JWT-only trust for mutations |
| Commercial limits | `POST /api/companies` only via `ICommercialPlanLimitService` + provisioning |
| SuperAdmin `/companies` | Unchanged — still manages **Subscribers**, not fiscal `company` rows |

## Dependency analysis

```
CompaniesController
  → MediatR handlers (Application/Modules/Platform/Companies)
    → ICompanyAccessGuard (membership + subscriber + active checks)
    → ICompanyRepository
    → ICompanyProvisioningService (create + MAX_COMPANIES)
    → ICommercialPlanLimitService (enforcement only on create)
    → IAccessRepository (memberships)
    → ICurrentSubscriber / ICurrentCompany / ICurrentUser
```

## Refactor plan (executed)

1. Extend `Company` with `Timezone`, `CurrencyCode`, `LogoUrl`, `BrandingJson`; domain `UpdateProfile` / `CreateManaged`.
2. EF migration `20260520230000_CompanyManagementProfileFields`.
3. `ICompanyAccessGuard` — single place for membership validation (handlers call this, not inline checks).
4. `ICompanyProvisioningService.CreateManagedCompanyAsync` — limit gate + company + creator membership.
5. CQRS under `ERP.Application.Modules.Platform.Companies` (SaaS boundary).
6. Frontend `/saas/companies/*` — separate from SuperAdmin `/companies`.

## Risks

| Risk | Mitigation |
|------|------------|
| Global unique `ruc` on `company` | Check `GetByRucAsync` before create |
| `GetByIdsAsync` filtered active only | New `GetByIdsForManagementAsync` for list/edit |
| Permission not in plan | Seed `saas.companies.*` in install bootstrap; Admin bypass via `PermissionHandler` |
| Confusion Subscriber vs Company | Routes: `/companies` = SuperAdmin subscribers; `/saas/companies` = tenant fiscal companies |

## Security

- List: membership ∩ current `subscriber_id`
- Get current: JWT `company_id` + membership re-validated
- Create: `perm:saas.companies.create` + active subscriber + limit service
- Update: `perm:saas.companies.update` + membership on target `company_id`
- Never accept arbitrary `company_id` without `ICompanyAccessGuard`

## Login → subscriber → company flow

1. Login → subscriber context + token (maybe `requiresCompanySelection`)
2. If 1 company in subscriber → auto JWT with `company_id`
3. If N → `/select-company` → `POST /api/auth/switch-company`
4. Management UI at `/saas/companies` lists accessible companies; switch via existing auth API
