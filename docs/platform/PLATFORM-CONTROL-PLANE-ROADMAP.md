# Platform SaaS Control Plane — Roadmap y Estado

**Fecha:** 2026-05-23  
**Versión:** 1.0  
**Basado en:** auditoría completa del repositorio en esta fecha.

---

## Estado actual (post-implementación Phase 1)

| Componente | Estado | Ubicación |
|-----------|--------|-----------|
| Subscriber CRUD | ✅ Implementado | `PlatformSubscribersController` |
| Subscriber Lifecycle (Activate/Suspend/Trial/GracePeriod) | ✅ Nuevo | `Subscriber.Activate/Suspend/SetTrial/EnterGracePeriod` |
| SubscriptionStatus (Suspended/Trial/GracePeriod) | ✅ Nuevo | `SubscriptionStatus` enum |
| PlatformAuditLog entity | ✅ Nuevo | `ERP.Domain.Platform.Audit.Entities` |
| IPlatformAuditLogger service | ✅ Nuevo | `ERP.Application.Platform.Audit` |
| Plans & Entitlements catalog | ✅ Implementado | `CommercialPlan`, `PlatformFeature`, `CommercialPlanLimit` |
| Entitlement enforcement (MediatR) | ✅ Implementado | `RequireFeatureAttribute` pipeline |
| Entitlement enforcement (HTTP) | ✅ Nuevo | `[RequireEntitlement]` MVC filter |
| Dynamic Menu `/api/me/menu` | ✅ Implementado | `MeController.GetMenu` |
| Billing schema | ✅ Implementado | `SaasBillingInvoice`, `SubscriberBillingAccount` |
| Billing grace period logic | ✅ Nuevo | `CheckSubscriptionExpiryJob` (Hangfire, cada hora) |
| Platform Metrics | ✅ Nuevo | `PlatformMetricsController` + `GetPlatformMetricsHandler` |
| Rate Limiting | ✅ Implementado | `per-subscriber` (600/min), `auth-refresh-ip` |
| Health Checks | ✅ Implementado | 8 health checks (DB, Security, MasterData, QueryFilters...) |
| OpenTelemetry | ✅ Implementado | Prometheus scrape en `/metrics` |
| Platform Auth | ✅ Implementado | `PlatformAuthController` + token type `platform` |
| RBAC Platform | ✅ Implementado | `GlobalSuperAdmin` policy, `Roles = "SuperAdmin"` |
| EF Migration | ✅ Nuevo | `20260523131502_AddPlatformControlPlane` |
| Tests Domain (lifecycle) | ✅ Nuevo | 29 nuevos tests en `ERP.Domain.Tests.Platform` |
| Tests Application (handlers) | ✅ Nuevo | 11 nuevos tests en `ERP.Application.Tests.Platform` |

---

## Separación ERP.Platform.* vs ERP.Runtime.*

### Estado actual: Monolito modular

El proyecto usa un **monolito modular** con un único `ErpDbContext`. La separación conceptual ya existe:

| Capa conceptual | Código actual | Separación formal |
|-----------------|---------------|-------------------|
| Platform (SuperAdmin) | `ERP.Application.Platform.*`, `ERP.API.Controllers.Platform.*` | No (mismo proyecto) |
| Runtime (ERP operativo) | Todo lo demás | No (mismo proyecto) |
| DbContext Platform | `PlatformAuditLogs` DbSet (nuevo) | Compartido en `ErpDbContext` |
| DbContext Runtime | Todos los demás DbSets | Compartido en `ErpDbContext` |

### ¿Cuándo separar en proyectos distintos?

**NO separar todavía si:**
- Equipo < 5 devs
- < 500 subscribers
- Sin requisito de compliance que exija aislamiento de datos

**SÍ separar cuando (criterio de decisión):**
- Compliance o auditoría exige que logs de platform no estén en el mismo DB schema que datos de tenant
- Performance: Platform metrics / audit queries impactan queries de ERP en prod
- Equipo crece y las fronteras de ownership se vuelven confusas
- > 1000 subscribers con SLA diferenciado

### Paso a paso si se decide separar:

1. Crear `ERP.Platform.Domain` → mover `PlatformAuditLog`, `CommercialPlan`, `PlatformFeature`
2. Crear `ERP.Platform.Application` → mover `Platform/Audit`, `Platform/Subscribers`, `Platform/Metrics`
3. Crear `PlatformDbContext` (nuevo proyecto `ERP.Platform.Infrastructure`)
4. Migrar `platform_audit_logs`, `commercial_plans`, `platform_features` a nueva conexión DB
5. Actualizar DI registrations y `Program.cs`
6. Mantener `ErpDbContext` solo para entidades operativas de tenant

---

## Phase 1.5 — Consolidation & Canonicalization (2026-05-23)

| Item | Estado |
|------|--------|
| UI canónica `/superadmin/*` (8 rutas) | ✅ Shell + redirects legacy |
| API canónica `/api/platform/*` (8 prefijos) | ✅ auth, subscribers, plans, config, metrics, audit, users (summary) |
| `[DeprecatedApi]` legacy controllers | ✅ superadmin, IAM, planes/empresas ES, subscription patch |
| Subscriber detail page wired | ✅ `/superadmin/subscribers/:id` |
| Menu builder fusionado en Plans | ✅ `?tab=menu` |
| Docs CANONICAL-ROUTES + ROUTE-MIGRATION | ✅ |

**Pendiente Phase 3:** eliminación física de controllers legacy (gated por métricas), billing automation, enabledModules platform PATCH.

---

## Phase 2 — Canonicalization + Runtime Separation (2026-05-23)

| Item | Estado |
|------|--------|
| Subscriber detail 9 tabs (absorbe CompaniesPage) | ✅ |
| Frontend platform → `/api/platform/*` only | ✅ |
| Deprecation instrumentation + legacy dashboard | ✅ |
| Platform Users / Billing / Observability UI | ✅ |
| Growth analytics → `/api/platform/metrics/growth-*` | ✅ |
| Navigation / features → platform controllers | ✅ |
| Cleanup audit doc | ✅ [PHASE2-CLEANUP-AUDIT.md](./PHASE2-CLEANUP-AUDIT.md) |
| Build + API/Architecture tests | ✅ |

**Pendiente Phase 3:** ver deuda en PHASE2-CLEANUP-AUDIT.md (roles extendidos, impersonation logs UI, billing detalle, tracker persistente).

---

## Roadmap de migración futura

### Phase 2 — Per-Plan Rate Limits (próximo sprint)

**Objetivo:** Rate limit configurable por plan (starter: 200/min, business: 600/min, enterprise: ilimitado)

**Cambios necesarios:**
- Añadir `RateLimitPerMinute` a `CommercialPlanLimit`
- `Program.cs` rate limiter lee límite de `ISubscriberEntitlementsService`
- Migration: nueva fila en `commercial_plan_limits` por plan

### Phase 3 — Billing Automation

**Objetivo:** Generación automática de facturas SaaS y notificaciones de vencimiento

**Cambios necesarios:**
- `BillingCycleJob` (Hangfire mensual): genera `SaasBillingInvoice` por subscriber activo
- `PaymentWebhookController`: recibe confirmaciones de Stripe/Paymentez
- Notificación por email 7/3/1 días antes del vencimiento
- `SaasBillingInvoiceStatus.Overdue` si no se paga en `DueAtUtc`

### Phase 4 — Usage Metering API

**Objetivo:** Tracking real de consumo por feature (API requests, AI tokens, invoices, storage)

**Cambios necesarios:**
- Actualizar `SubscriptionUsage` con contadores acumulativos
- Meter middleware que intercepta requests y llama `ISubscriptionUsageService.RecordAsync`
- Dashboard `/api/platform/subscribers/{id}/usage` con datos de consumo

### Phase 5 — Multi-region / Data Residency

**Objetivo:** Subscribers en regiones distintas (US, EU, LA) con datos en DB regional

**Precondición:** Separación de DbContexts (ver sección anterior)

**Cambios necesarios:**
- Routing por `subscriber.region` en API Gateway
- Conexiones DB por región en `appsettings.{region}.json`
- `PlatformAuditLog` en DB global (cross-region)

### Phase 6 — Separación de Microservicios

**Cuándo:** > 10K subscribers O requisitos de scaling independiente

**Orden recomendado de extracción:**
1. `ERP.Platform.API` → Control plane separado (auth, metrics, billing)
2. `ERP.Auth.API` → Autenticación standalone
3. `ERP.MasterData.API` → BusinessPartner standalone
4. `ERP.Accounting.API` → Módulo contable standalone

---

## Cuándo eliminar tablas legacy

**Criterio:** Una tabla legacy puede eliminarse cuando:
1. Drop-readiness score ≥ 90/100 (ver `LEGACY-DROP-READINESS.md`)
2. 0 referencias directas en código (verificar con grep)
3. Shadow FK migradas en todos los documentos históricos
4. Plan de rollback documentado y probado

**Orden de eliminación recomendado (NO antes de 2027-Q1):**
1. Columnas FIXME en `subscribers` → mover a `companies` (phase5-db)
2. `customers.identity_number` → FK a `business_partners`
3. `suppliers.tax_id` → FK a `business_partners`
4. Drop de `customers` standalone → todos usan `business_partner_id`
5. Drop de `suppliers` standalone → todos usan `business_partner_id`

---

## Restricciones absolutas (invariantes del sistema)

1. **NO DELETE físico** — siempre soft-delete o suspend
2. **NO DROP TABLE sin drop-readiness ≥ 90**
3. **NO romper multiempresa** — `SubscriberId` filter en todas las entidades operativas
4. **NO platform data en tenant scope** — `PlatformAuditLog` NO tiene `SubscriberId` filter
5. **NO mezclar tokens** — platform token no puede acceder a endpoints de runtime ERP

---

## Validación arquitectónica

Para verificar que no hay leakage entre Platform y Runtime, ejecutar:

```bash
# Verificar que PlatformAuditLog no tiene query filter de subscriber
dotnet test --filter "FullyQualifiedName~Architecture"

# Verificar que controllers Platform no usan entidades operativas de tenant
grep -r "Customer\|Supplier\|SalesInvoice" backend/src/ERP.API/Controllers/Platform/
```

Los Architecture Tests en `ERP.Architecture.Tests` deben agregar un test que verifique que `Platform.*` no depende de entidades operativas.
