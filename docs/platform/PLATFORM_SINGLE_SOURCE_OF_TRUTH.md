# Platform Single Source of Truth

Documento normativo del contrato end-to-end del Platform Control Plane tras consolidación final.

## Principio

> Una sola superficie lógica: **`/api/platform/*`**.  
> `/api/subscribers/*` es **ERP Runtime**, no control plane (excepto endpoints cross-cutting documentados).

## Stack vertical (Subscriber)

```
┌─────────────────────────────────────────────────────────────┐
│  UI: /superadmin/subscribers/*                              │
│  platformService + subscriberService                        │
└───────────────────────────┬─────────────────────────────────┘
                            │ HTTPS
┌───────────────────────────▼─────────────────────────────────┐
│  API: /api/platform/subscribers/*                           │
│  PlatformSubscribersController                                │
└───────────────────────────┬─────────────────────────────────┘
                            │ MediatR / Application
┌───────────────────────────▼─────────────────────────────────┐
│  Domain: Subscriber, SubscriberSubscription, …                │
└───────────────────────────┬─────────────────────────────────┘
                            │ EF Core
┌───────────────────────────▼─────────────────────────────────┐
│  DB: subscribers, subscriber_subscriptions, …               │
└─────────────────────────────────────────────────────────────┘
```

## Constantes frontend (SoT paths)

```typescript
// frontend/src/modules/platform/api/platformApiPaths.ts
export const PLATFORM_API = {
  subscribers: '/api/platform/subscribers',
  config: '/api/platform/config',
  // …
} as const;
```

## Runtime boundary (fuera del control plane)

```
┌──────────────────────────────────────┐
│  ERP Session / Tenant Admin UI       │
│  entitlementsService                 │
│  tenantSubscriberService             │
└──────────────┬───────────────────────┘
               │
    /api/subscribers/entitlements/me
    /api/subscribers/{id}/company
    /api/auth/switch-subscriber
    /api/public/plans
```

**Regla:** ningún código bajo `modules/platform/` debe importar `tenantSubscriberService` ni llamar `/api/subscribers` excepto vía auth helpers whitelisted.

## Backend SoT

| Concern | Canónico | Legacy (deprecado) |
|---------|----------|-------------------|
| Subscriber CRUD platform | `Controllers/Platform/PlatformSubscribersController.cs` | `SubscribersController` POST + PATCH global/subscription |
| Subscriber runtime | `SubscribersController` GET/PATCH company, operational | — |
| Session entitlements | `SaasEntitlementsController` | — |

## Naming alignment

| Concepto | Nombre canónico | Evitar |
|----------|-----------------|--------|
| Entidad | Subscriber | Tenant (solo en JWT claim legacy) |
| API prefix | `/api/platform` | `/api/superadmin`, `/api/subscribers` (control plane) |
| Frontend module | `modules/platform` | `modules/superadmin`, `companyService` |
| Service | `platformService`, `subscriberService` | `platformService`, `companyService` |

## CI como guardián del contrato

| Guard | Qué protege |
|-------|-------------|
| `run-platform-guard.mjs` | Patrones legacy SuperAdmin + companyService |
| `validate-subscriber-api-surface.mjs` | `/api/subscribers` solo runtime whitelist |
| `PlatformControlPlaneGuardTests.cs` | Rutas backend `/api/superadmin`, deprecaciones |

Config: `tools/ci/platform-guard-config.json`

## Telemetría migración

- `DeprecatedApiAttribute` → hits en observability platform (solo si reaparecen rutas legacy)
- Dashboard: `GET /api/platform/observability/legacy-endpoints`

## Checklist para nuevas features platform

1. ¿Es gestión SaaS global? → `/api/platform/*` + `Platform*Controller`
2. ¿Es operación tenant en sesión? → runtime `/api/subscribers` o dominio ERP
3. ¿Frontend platform? → `platformService` / `subscriberService` únicamente
4. ¿Añadir CI? → actualizar `platform-guard-config.json` si nuevo patrón legacy

## Estado final

**Contrato único end-to-end:** DB `subscribers` ↔ Domain `Subscriber` ↔ API `/api/platform/subscribers` ↔ Frontend `subscriberService`.

Duplicidad funcional activa en frontend: **0**.  
Legacy backend control plane en `/api/subscribers`: **eliminado** (0 endpoints duplicados activos).
