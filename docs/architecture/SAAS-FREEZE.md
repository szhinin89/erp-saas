# SaaS Core — Architecture Freeze
## ZH Technologies · ERP SaaS Platform

> **Frozen on:** 2026-05-28  
> **Status:** ✅ FROZEN — No structural changes without ADR approval  
> **Scope:** SaaS Subscription & Billing Engine  

---

## ¿Qué significa "frozen"?

El SaaS Core está **production-ready en modo manual/simulado**. La arquitectura está
consolidada y NO debe modificarse estructuralmente sin:

1. Crear un ADR (Architecture Decision Record) en `/AI-RULES/`
2. Revisar con el equipo de Tech Lead
3. Garantizar backward compatibility
4. Garantizar que los tests existentes siguen pasando

---

## Componentes CONGELADOS (frozen)

| Componente | Ubicación | Descripción |
|---|---|---|
| `ISubscriptionLifecycleOrchestrator` | `ERP.Application.Common.Subscriptions` | State machine de lifecycle — no agregar transiciones sin ADR |
| `ISubscriptionAccessService` | `ERP.Application.Common.Subscriptions` | Access evaluation cache-first — no cambiar evaluation logic |
| `ISubscriptionAccessCache` | `ERP.Application.Common.Subscriptions` | Interfaz de cache L1+L2 — no cambiar contrato |
| `SubscriptionAccessMiddleware` | `ERP.API.Middleware` | Request guard — no cambiar bypass rules sin ADR |
| `ReadOnlyEnforcementMiddleware` | `ERP.API.Middleware` | Write blocker en ReadOnly — congelado |
| `SubscriptionBillingOptions` | `ERP.Application.Common.Subscriptions` | Config centralizada — solo agregar campos, no eliminar |
| `IBillingInvoiceService` | `ERP.Application.Billing.Invoices` | Invoice generation — no cambiar firma sin ADR |
| `IPaymentProvider` | `ERP.Application.Billing.PaymentProviders` | Provider interface — no cambiar sin ADR |
| `CheckSubscriptionExpiryJob` | `ERP.API.Hangfire` | Expiry job — lógica congelada |
| `SubscriptionRenewalService` | `ERP.Infrastructure.SaaS` | Renewal engine — lógica congelada |
| `WebhookEventProcessor` | `ERP.Infrastructure.SaaS` | Webhook routing — congelado |
| `ProcessedWebhookEvent` | `ERP.Domain.Billing.Entities` | Dedup table — no cambiar sin migración |
| Domain events en Subscribers | `ERP.Domain.Modules.Subscribers.Events` | Event model — no renombrar |

---

## ANTI-CORRUPTION RULES (obligatorias)

### Billing Layer
```
SaaS Billing NO depende de:
  ❌ ERP.Domain.Modules.Sales (ventas, facturas ERP)
  ❌ ERP.Domain.Modules.Fiscal (fiscal invoices)
  ❌ ERP.Domain.Modules.Accounting (contabilidad)
  ❌ ERP.Domain.Modules.Payroll (nómina)
  ❌ ERP.Domain.Modules.Inventory (inventario)

SaaS Billing SÍ puede depender de:
  ✅ ERP.Domain.Billing (entidades propias)
  ✅ ERP.Domain.Subscribers (suscriptores)
  ✅ ERP.Domain.Subscriptions (planes, entitlements)
  ✅ ERP.Infrastructure.Persistence (DbContext)
```

### ERP Modules
```
ERP Modules NO deben llamar directamente a:
  ❌ ISubscriptionLifecycleOrchestrator (solo Platform CP)
  ❌ SubscriberBillingAccount (no leer billing desde handlers ERP)
  ❌ SaasBillingInvoice (no crear facturas SaaS desde ventas ERP)

ERP Modules SÍ pueden usar:
  ✅ ISubscriptionAccessService (para verificar acceso)
  ✅ IFeatureAccessService (para verificar features/quotas)
  ✅ ISubscriberEntitlementsService (para módulos habilitados)
  ✅ CommercialPlanLimitService (para quotas MAX_*)
```

---

## Sources of Truth — Definitivo

| Aspecto | Source of Truth | NO usar |
|---|---|---|
| Identidad SaaS del tenant | `Subscriber.Name/Slug/PlanCode` | `Company.LegalName` para SaaS |
| Lifecycle del tenant | `Subscriber.LifecycleStatus` | `BillingAccount.Status` directamente |
| Estado de facturación | `SubscriberBillingAccount.Status` | `Subscriber.LifecycleStatus` para billing |
| Plan activo | `SubscriberSubscription.PlanId` | PlanCode hardcoded |
| Módulos habilitados | `SubscriberEntitlementsService` | Variables locales cacheadas |
| Quotas (MAX_*) | `CommercialPlanLimit` via `CommercialPlanLimitService` | Verificación manual |
| Decisión de acceso | `ISubscriptionAccessService.EvaluateAsync()` | `subscriber.IsActive` directo |
| Identidad fiscal ERP | `Company.Ruc/LegalName/MainAddress` | `Subscriber.*` (ya eliminado) |
| Identidad fiscal SaaS billing | `SubscriberBillingProfile` | - |
| Historial de cobros | `SaasBillingInvoice` | - |
| Auditoría billing | `BillingEvent` | - |

---

## Extension Points Aprobados

### Para agregar un Payment Provider (Stripe, Kushki, etc.)
1. Implementar `IPaymentProvider` (interfaz ya definida)
2. Registrar en DI como Singleton
3. Actualizar `PaymentProviderFactory.GetForSubscriber()` para leer config de DB
4. Agregar webhook signature validation en `ValidateWebhookAsync()`
5. **NO modificar** `WebhookEventProcessor`, `BillingController`, ni el lifecycle

### Para agregar Notificaciones (Email, SMS, WhatsApp)
1. Implementar `IBillingNotificationService` (interfaz ya definida)
2. Reemplazar `NullBillingNotificationService` en DI
3. **NO modificar** el orchestrator ni los handlers
4. Inyectar service donde se emiten eventos de billing

### Para agregar Metered Usage
1. Usar `SubscriptionUsage` entity (ya existe en DB)
2. Implementar `ISubscriptionUsageTracker` (nuevo servicio)
3. Llamar desde handlers de entidades (crear usuario, crear empresa, etc.)
4. **NO tocar** CommercialPlanLimitService ni el lifecycle

### Para agregar un Plan nuevo
1. Agregar en `CommercialPlansBootstrap` (idempotente)
2. Agregar módulos en `CommercialPlanFeaturesBootstrap`
3. Agregar límites en `CommercialPlanLimitsBootstrap`
4. **NO hardcodear** el plan code en ningún handler o servicio

---

## Hot Paths (NO degradar performance)

| Hot Path | Target | Cómo garantizarlo |
|---|---|---|
| `SubscriptionAccessMiddleware` cache hit | < 1ms | HybridSubscriptionAccessCache L1 |
| `SubscriptionAccessMiddleware` cache miss | < 20ms | Compiled EF query JOIN |
| Lifecycle transition | < 100ms p95 | RepeatableRead TX, minimal queries |
| Invoice generation | < 50ms | SaveChanges única |
| Renewal per subscriber | < 200ms | Advisory lock + compiled queries |

---

## Components Ready for Stripe Integration

```
LISTO (no cambios necesarios):
  ✅ IPaymentProvider interface + all method signatures
  ✅ IPaymentProviderFactory + routing infrastructure
  ✅ BillingWebhookController + signature validation pipeline
  ✅ WebhookEventProcessor + deduplication
  ✅ BillingCheckoutSession entity + EF config
  ✅ BillingPaymentAttempt entity + EF config
  ✅ IBillingInvoiceService + payment attempt persistence
  ✅ SubscriptionRenewalService + ChargeInvoiceAsync integration point
  ✅ simulate-payment endpoint (para testing sin Stripe)
  ✅ POST /api/billing/checkout → will create real checkout URL

NECESITA IMPLEMENTACIÓN (~400 líneas):
  ⏳ StripePaymentProvider : IPaymentProvider
     - EnsureCustomerAsync → Stripe.CustomerService
     - CreateCheckoutSessionAsync → Stripe.CheckoutSessionService
     - ChargeInvoiceAsync → Stripe.InvoiceService
     - ValidateWebhookAsync → StripeClient.ConstructEvent (signature)
     - ParseWebhookEventAsync → map Stripe event → ProviderWebhookEvent
  ⏳ PaymentProviderFactory.GetForSubscriber() → read DB config
  ⏳ Stripe API keys in appsettings (user secrets)
```

---

## Observabilidad Disponible

### Métricas (Prometheus / OTLP)
- `active_subscribers_total` — tenants activos
- `trial_subscribers_total` — en trial
- `suspended_subscribers_total` — suspendidos
- `subscription_access_cache_hits` — eficiencia cache
- `subscription_access_cache_misses` — rebuilds desde DB
- `subscription_access_denied_total{reason}` — accesos bloqueados
- `subscription_access_allowed_total` — accesos permitidos
- `subscription_suspend_total` — suspensiones
- `grace_period_entered_total` — grace periods iniciados
- `grace_period_recovered_total` — recuperaciones post-grace
- `subscription_trial_expired_total` — trials vencidos
- `renewal_success_total` — renovaciones exitosas
- `renewal_failure_total` — renovaciones fallidas
- `invoices_generated_total{plan}` — facturas creadas
- `invoices_paid_total` — facturas pagadas
- `payment_attempts_total{provider}` — intentos de cobro
- `payment_attempts_failed_total{code}` — intentos fallidos
- `lifecycle_transition_duration_ms{transition}` — latencia de transiciones
- `snapshot_rebuild_duration_ms` — latencia de rebuild de cache
- `renewal_processing_duration_ms` — latencia de renovación

### Health Checks
- `GET /health/live` — proceso activo
- `GET /health/ready` — BD + servicios listos
- `GET /health/saas` — SaaS subsystem específico (invoices, billing accounts, cache)

### Debug Panel
- `GET /api/platform/saas/debug/{subscriberId}` — snapshot completo (PlatformOperator only)

---

## Riesgos Restantes (aceptables para modo manual)

| Riesgo | Severidad | Descripción |
|---|---|---|
| Sin payment provider real | ACEPTABLE | NullPaymentProvider en modo manual. Extension points listos. |
| SubscriptionUsage sin incrementers | BAJO | Metered billing no implementado. Tabla lista. |
| Self-service signup no existe | BAJO | Requiere Stripe + UI de registro. |
| MRR/ARR sin dashboard | BAJO | Datos en SaasBillingInvoice. Dashboard pendiente. |
| MenuConfigJson hardcodeado en bootstrap | BAJO | Futuro: mover a DB para personalización por plan. |

---

## Comandos de Verificación

```bash
# Health SaaS
curl http://localhost:5001/health/saas | jq

# Prometheus metrics (incluye SaaS)
curl http://localhost:5001/metrics | grep -E "subscription|billing|renewal|invoice"

# Debug panel (requiere JWT PlatformOperator)
curl -H "Authorization: Bearer {jwt}" \
  http://localhost:5001/api/platform/saas/debug/{subscriberId} | jq
```

---

*Documento generado y aprobado por ZH Technologies Tech Lead — 2026-05-28*
