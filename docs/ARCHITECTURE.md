# Arquitectura

Monolito modular: **Clean Architecture + CQRS (MediatR)**.

Documentos relacionados: [IDENTITY.md](./IDENTITY.md), [SAAS-COMMERCIAL.md](./SAAS-COMMERCIAL.md), [DATABASE.md](./DATABASE.md), [STATUS.md](./STATUS.md), [ROADMAP.md](./ROADMAP.md).

---

## Capas

| Capa | Proyecto | Responsabilidad |
|------|----------|-----------------|
| Domain | `ERP.Domain` | Entidades, enums, excepciones, interfaces |
| Application | `ERP.Application` | Casos de uso, behaviors, DTOs, ports |
| Infrastructure | `ERP.Infrastructure` | EF Core, Redis, guards, billing, limits |
| API | `ERP.API` | HTTP, JWT, middleware, policies |

Dependencias: API → Application → Domain; Infrastructure implementa ports.

---

## Jerarquía multi-tenant

```
GlobalSuperAdmin (platform)
  └── Subscriber (contrato SaaS: plan, billing, límites)
        └── Company (entidad fiscal / operativa)
              └── CompanyUserMembership
                    └── Módulos ERP (Ventas, Inventario, Contabilidad, …)
```

| Actor | Clave scope | Paga / gobernado | Opera ERP |
|-------|-------------|------------------|-----------|
| GlobalSuperAdmin | — (platform) | — | bypass RLS vía `app.is_platform_admin` |
| Subscriber | `subscriber_id` | sí | vía companies |
| Company | `company_id` | no | sí (JWT) |

**Regla:** Subscriber paga y se gobierna. Company opera. Billing SaaS nunca usa `company_id`.

---

## Capas platform vs ERP runtime

| Capa | Clave | Responsabilidad | API (canónica) |
|------|-------|-----------------|----------------|
| **Platform** | `subscriber_id` | Onboarding, planes, límites, menús | `/api/platform/*` |
| **IAM** | `identity_user_id` | Auth, switch, perfiles, permisos | `/api/auth/*`, `/api/platform/auth/*` |
| **Billing SaaS** | `subscriber_id` | Cuenta billing plataforma | `/api/saas/billing/*` |
| **Company** | `company_id` | Empresa fiscal (RUC) | `/api/companies/*` |
| **ERP Runtime** | `company_id` | Ventas, inventario, compras, SRI | `/api/sales/*`, `/api/inventory/*`, … |

Separación:

- Platform **no** ejecuta lógica ERP operativa.
- IAM **no** provisiona billing ni companies (orquestador Platform).
- ERP Runtime **siempre** filtra por `company_id`.

### Ownership matrix

| Concepto | Capa | Clave |
|--------|------|-------|
| Suscriptor SaaS | Platform | `subscriber_id` |
| Empresa fiscal | Company / ERP | `company_id` |
| Usuario login | IAM | `identity_users.id` |
| Membership | IAM | `(company_id, identity_user_id)` |
| Billing account | Billing | `subscriber_id` |
| Límites plan | Platform | `subscriber_id` |

---

## Scopes

Toda entidad nueva declara **un** scope primario.

### SaaS (`subscriber_id`)

`subscribers`, `commercial_plans*`, `subscriber_subscriptions`, `saas_billing_*`, `subscriber_custom_menus`, `config_*`.

JWT: `subscriber_id` en operaciones platform del tenant.

### ERP operativo (`company_id` target)

Ventas, compras, contabilidad, caja — hoy filtro `subscriber_id`; migración a `company_id`. Wave 1: inventario core con `company_id` nullable + RLS.

JWT: `company_id` obligatorio (`CompanyScopeBehavior`).

### Billing SaaS

Solo `subscriber_id`. Ver [SAAS-COMMERCIAL.md](./SAAS-COMMERCIAL.md).

### IAM

`company_user_memberships` solo `company_id` + `identity_user_id`. Permisos por `(companyId, userId)`.

### Platform (sin tenant)

SuperAdmin global: `subscriber_id` vacío, `app.is_platform_admin=true`. `PlatformQueryReason` si `IgnoreQueryFilters()`.

---

## Multi-tenant — aislamiento

| Capa | Mecanismo |
|------|-----------|
| JWT | `subscriber_id`, `company_id` |
| MediatR | `BillingGateBehavior`, `SubscriptionGateBehavior`, `CompanyScopeBehavior` |
| Application | `ICompanyAccessGuard`, `ICurrentSubscriber`, `ICurrentCompany` |
| EF Core | Filtro global `ISubscriberScopedEntity` |
| PostgreSQL | RLS Wave 1 — [DATABASE.md](./DATABASE.md#rls) |

### Cambio de contexto

1. Login → `subscriber_id`
2. Una company → auto `company_id`
3. Varias → `/select-company` → `POST /api/auth/switch-company`
4. Handlers leen `ICurrentCompany` — **nunca** `company_id` del body como autoridad

### Background jobs

Antes de BD: `JobSubscriberContext`, `JobCompanyContext` para interceptor PostgreSQL.

Cuotas: solo `ICommercialPlanLimitService` — no `COUNT(*)` manual en handlers.

Terminología retirada: `Tenant`, `tenant_id`.

---

## Bounded contexts

| Contexto | Carpeta / namespace | Scope |
|----------|---------------------|-------|
| Platform / IAM | `Modules/Platform`, `Modules/Access` | subscriber, company |
| Subscriptions | `Subscriptions`, `CommercialPlanLimits` | `subscriber_id` |
| Billing | `Billing` | `subscriber_id` only |
| Sales / Purchasing / Inventory / Accounting / Cash | `Modules/*` | → `company_id` |
| SRI | `Configuration`, `Sales` | por company settings |

Markers: `PlatformLayerBoundary`, `IamLayerBoundary`, `ErpRuntimeLayerBoundary`.

---

## Pipeline MediatR

1. `ValidationBehavior`
2. `BillingGateBehavior`
3. `SubscriptionGateBehavior`
4. `CompanyScopeBehavior`
5. `CachingBehavior`

---

## Servicios core

| Concern | Interface |
|---------|-----------|
| Contexto SaaS | `ICurrentSubscriber` |
| Contexto ERP | `ICurrentCompany` |
| Acceso company | `ICompanyAccessGuard` |
| Límites plan | `ICommercialPlanLimitService` |
| Entitlements | `ISubscriberEntitlementsSnapshotCache` |
| Billing | `IBillingGovernanceService` |
| Pagos (futuro) | `IPaymentProviderAdapter` |

---

## CQRS

- Commands/queries en `ERP.Application/Modules/{Module}/UseCases/`
- ERP: `ICompanyScopedRequest` o namespaces cubiertos por `CompanyScopeBehavior`
- Platform: `ISubscriberOnlyRequest` cuando no hay company

---

## API (rutas estables)

| Área | Base |
|------|------|
| Auth | `/api/auth/*` |
| Platform auth | `/api/platform/auth/*` |
| Platform admin | `/api/platform/subscribers/*` |
| Companies | `/api/companies/*` |
| Billing SaaS | `/api/saas/billing/*` |
| Entitlements | `/api/saas/entitlements` |
| ERP | `/api/{module}/*` |
| SuperAdmin legacy | `/api/superadmin/*`, `/api/admin/*` (alias en deprecación) |

Aliases legacy documentados en código `[Obsolete]`; nuevas integraciones usan rutas `/api/platform/*`.

---

## Frontend

- Claims JWT: `subscriber_id`, `company_id`
- Flujo: login → subscriber → switch-company si N empresas
- UI SaaS: `/saas/companies`, `/select-company`, `CompanySwitcher`
- Detalle auth UI: [IDENTITY.md](./IDENTITY.md#frontend)

---

## Caching

| Cache | Patrón clave | Invalidación |
|-------|--------------|--------------|
| Entitlements | `entitlements:snapshot:{subscriberId}:v{N}` | plan/billing |
| Permisos | por `(companyId, userId)` | perfil/membership |

Redis opcional; fallback in-memory si deshabilitado.
