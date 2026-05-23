# SaaS comercial — planes y billing

Capa **Subscriber** (`subscriber_id`). Nunca usar `company_id` aquí.

Distinto de facturación ERP (`sales_invoice`, `billing_settings` SRI/tirilla).

Relacionado: [ARCHITECTURE.md](./ARCHITECTURE.md), [IDENTITY.md](./IDENTITY.md), [ROADMAP.md](./ROADMAP.md).

---

## Planes y límites

Punto único de enforcement: **`ICommercialPlanLimitService`**.

### Modelo

| Tabla | Propósito |
|-------|-----------|
| `commercial_plans` | Catálogo (Starter, Business, …) |
| `commercial_plan_features` | Features por plan |
| `commercial_plan_limits` | Topes numéricos |
| `subscriber_subscriptions` | Suscripción activa |
| `subscriber_subscription_events` | Auditoría |
| `subscription_feature_overrides` | Overrides por subscriber |
| `subscription_usages` | Medidores de uso |
| `platform_features` | Registro global features |

### Códigos de límite

| Code | Enforced | Provider |
|------|----------|----------|
| `MAX_COMPANIES` | sí | `MaxCompaniesLimitUsageProvider` |
| `MAX_USERS` | sí | `MaxUsersLimitUsageProvider` |
| `MAX_BRANCHES` | sí | `MaxBranchesLimitUsageProvider` |
| `MAX_WAREHOUSES` | sí | `MaxWarehousesLimitUsageProvider` |
| `MAX_STORAGE_MB` | reservado | TBD |
| `MAX_AI_TOKENS` | reservado | TBD |
| `MAX_API_REQUESTS` | reservado | TBD |

Bootstrap: `CommercialPlanLimitsBootstrap` (idempotente al arranque).

### Flujo enforcement

```
Handler (company / user / branch / warehouse)
  → ICommercialPlanLimitService.ExecuteWithLimitEnforcementAsync
    → transacción Serializable + FOR UPDATE subscriber
    → usage provider vs commercial_plan_limits
    → 403 si excede
```

Reglas:

- Companies nuevas solo vía `ICompanyProvisioningService`
- Plan: `subscriber_subscriptions` → fallback `subscribers.plan_code`
- Sin fila en `commercial_plan_limits` → sin tope hasta seed

### Entitlements API

`GET /api/saas/entitlements` → `SubscriberEntitlementsSnapshot` + `CommercialLimits`.

### Cuota de despliegue

`DeploymentQuota` (archivo instancia) limita subscribers por deployment — independiente del plan comercial.

---

## Billing SaaS

Aislado de documentos financieros ERP.

### Dominio (`ERP.Domain.Billing`)

| Entidad | Tabla | Propósito |
|---------|-------|-----------|
| `SubscriberBillingAccount` | `subscriber_billing_accounts` | Estado, grace, trial |
| `SaasBillingInvoice` | `saas_billing_invoices` | Facturas plataforma |
| `SaasBillingInvoiceLine` | `saas_billing_invoice_lines` | Líneas |
| `BillingEvent` | `saas_billing_events` | Audit append-only |
| `PaymentProviderCustomer` | `payment_provider_customers` | Id externo |
| `PaymentProviderSubscription` | `payment_provider_subscriptions` | Sub externa |

Scope: **`subscriber_id` only**.

### Application

| Componente | Rol |
|------------|-----|
| `IBillingGovernanceService` | Grace, suspend, reactivate |
| `BillingGateBehavior` | Bloqueo si suspendido |
| `IPaymentProviderAdapter` | Abstracción; `NullPaymentProviderAdapter` hoy |
| `ISubscriberBillingRepository` | Persistencia |

### API

| Método | Ruta | Permiso |
|--------|------|---------|
| GET | `/api/saas/billing/account` | `perm:saas.billing.view` |
| GET | `/api/saas/billing/invoices` | `perm:saas.billing.view` |
| GET | `/api/saas/billing/events` | `perm:saas.billing.view` |

Sin Stripe SDK en handlers. Webhooks: [ROADMAP.md](./ROADMAP.md).

### Entitlements + cache

`SubscriberEntitlementsSnapshot` incluye `BillingAccountStatus`.

Claves: `entitlements:version:{subscriberId}`, `entitlements:snapshot:{subscriberId}:v{N}`.

### Seguridad billing

- Filtro global `ISubscriberScopedEntity`
- Fail-closed en suspensión (403)
- Sin PAN — solo refs de provider

---

## Distinción ERP vs SaaS

| Tabla | Contexto |
|-------|----------|
| `billing_settings` | SRI / RIDE / tirilla **por company** |
| `sales_invoice` | Ventas ERP |
| `saas_billing_invoices` | Facturación plataforma SaaS |

---

## Gestión de empresas fiscales (`company`)

Empresas bajo un subscriber (RUC, branding). Distinto del panel platform que administra **subscribers** (`/companies` en UI legacy).

| Use case | Permiso |
|----------|---------|
| Listar accesibles | membership ∩ `subscriber_id` |
| Crear | `perm:saas.companies.create` + límite plan |
| Actualizar perfil | `perm:saas.companies.update` |

API: `GET/POST/PUT /api/companies/*`, `POST /api/auth/switch-company`.

Frontend: `/saas/companies`, `/select-company`, `CompanySwitcher`.

Provisioning operador platform: `SubscriberProvisioningOrchestrator` (transacción Serializable: subscriber → billing → company → user → membership → onboarding).
