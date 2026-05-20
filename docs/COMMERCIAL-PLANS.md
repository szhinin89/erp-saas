# Commercial Plans & Limits

Single enforcement point: **`ICommercialPlanLimitService`**.

## Data model

| Table | Purpose |
|-------|---------|
| `commercial_plans` | Plan catalog (Starter, Business, …) |
| `commercial_plan_features` | Feature flags per plan |
| `commercial_plan_limits` | Numeric caps per plan |
| `subscriber_subscriptions` | Active subscription per subscriber |
| `subscriber_subscription_events` | Subscription audit |
| `subscription_feature_overrides` | Per-subscriber overrides |
| `subscription_usages` | Usage meters |
| `platform_features` | Global feature registry |

## Limit codes (seeded)

| Code | Enforced today | Provider |
|------|----------------|----------|
| `MAX_COMPANIES` | yes | `MaxCompaniesLimitUsageProvider` |
| `MAX_USERS` | yes | `MaxUsersLimitUsageProvider` |
| `MAX_BRANCHES` | yes | `MaxBranchesLimitUsageProvider` |
| `MAX_WAREHOUSES` | yes | `MaxWarehousesLimitUsageProvider` |
| `MAX_STORAGE_MB` | reserved | TBD |
| `MAX_AI_TOKENS` | reserved | TBD |
| `MAX_API_REQUESTS` | reserved | TBD |

Bootstrap: `CommercialPlanLimitsBootstrap` at startup (idempotent).

## Enforcement flow

```
Handler (create company / user / branch / warehouse)
  → ICommercialPlanLimitService.ExecuteWithLimitEnforcementAsync
    → Serializable transaction + FOR UPDATE on subscriber
    → Usage provider counts current usage
    → Compare to commercial_plan_limits
    → 403 CommercialPlanLimitExceededException if over cap
```

## Provisioning rules

- New **companies** only via `ICompanyProvisioningService` (never raw `ICompanyRepository.Add` from handlers)
- Plan resolution: `subscriber_subscriptions` → fallback `subscribers.plan_code`
- No row in `commercial_plan_limits` for a code → no cap (allow) until seeded

## Entitlements API

`GET /api/saas/entitlements` returns `SubscriberEntitlementsSnapshot` including `CommercialLimits` for UI and gates.

## Deployment quota (separate layer)

`DeploymentQuota` (instance-level file) caps subscribers per deployment — independent from commercial plan limits.
