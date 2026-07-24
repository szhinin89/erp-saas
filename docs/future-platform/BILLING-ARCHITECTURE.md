# Billing Architecture — ERP SaaS ZH Technologies

> ## 🚧 FUTURO / NO IMPLEMENTADO
>
> Este documento describe una posible plataforma externa futura.
>
> **NO forma parte del ERP actual.**
>
> No debe utilizarse como guía para desarrollar código dentro de ERP Core.

---

> **Estado:** Arquitectura lista. Payment providers NO conectados todavía.
> El sistema acepta cualquier provider futuro (Stripe, Kushki, PayPal, DataFast) sin cambios estructurales.

---

## 1. Flujo de Checkout (futuro — provider no conectado)

```mermaid
sequenceDiagram
    participant FE as Frontend
    participant API as BillingController
    participant CS as BillingCheckoutSession
    participant PP as IPaymentProviderFactory
    participant Prov as IPaymentProvider (Stripe/Kushki)
    participant DB as PostgreSQL

    FE->>API: POST /api/billing/checkout { planCode, billingCycle }
    API->>PP: GetForSubscriber(subscriberId)
    PP-->>API: IPaymentProvider instance
    API->>CS: BillingCheckoutSession.Create(...)
    API->>DB: INSERT billing_checkout_sessions
    API->>Prov: CreateCheckoutSessionAsync(request)
    Prov-->>API: { SessionId, CheckoutUrl, ExpiresAtUtc }
    API->>CS: SetProviderSession(sessionId, checkoutUrl)
    API->>DB: UPDATE billing_checkout_sessions
    API-->>FE: { checkoutUrl }
    FE->>Prov: redirect to checkoutUrl
    Prov->>FE: redirect to successUrl
    Note over Prov,API: Provider sends webhook asynchronously
```

---

## 2. Flujo de Webhooks

```mermaid
sequenceDiagram
    participant Prov as Payment Provider
    participant WH as BillingWebhookController
    participant Val as IPaymentProvider.ValidateWebhookAsync
    participant Parse as IPaymentProvider.ParseWebhookEventAsync
    participant Proc as IPaymentWebhookProcessor
    participant Orch as ISubscriptionLifecycleOrchestrator
    participant DB as PostgreSQL

    Prov->>WH: POST /billing/webhooks/{provider}
    WH->>Val: ValidateWebhookAsync(rawBody, headers)
    alt Invalid signature
        Val-->>WH: null
        WH-->>Prov: 400 Bad Request
    else Valid
        Val-->>WH: WebhookValidationResult
        WH->>Parse: ParseWebhookEventAsync(validated)
        Parse-->>WH: ProviderWebhookEvent
        WH->>Proc: ProcessAsync(evt)
        Proc->>Orch: ActivateAsync / EnterGracePeriodAsync / CancelAsync
        Orch->>DB: UPDATE subscribers + billing accounts (atomic)
        WH-->>Prov: 200 OK
    end
```

---

## 3. Lifecycle de Suscripción (State Machine)

```mermaid
stateDiagram-v2
    [*] --> TRIALING : Provisioning (CreateBillingAccountOrThrow)

    TRIALING --> ACTIVE : Trial expires + payment succeeds\nOR manual activation
    TRIALING --> GRACE_PERIOD : Trial expires, no payment\n(CheckSubscriptionExpiryJob)
    TRIALING --> CANCELLED : Admin cancel

    ACTIVE --> PAST_DUE : Auto-renewal payment fails\n(webhook: payment_failed)
    ACTIVE --> GRACE_PERIOD : Admin action / payment overdue
    ACTIVE --> CANCELLED : Admin cancel / subscriber cancel

    PAST_DUE --> ACTIVE : Payment succeeds\n(webhook: payment_succeeded)
    PAST_DUE --> GRACE_PERIOD : Grace period initiated

    GRACE_PERIOD --> SUSPENDED : Grace period expires\n(CheckSubscriptionExpiryJob)
    GRACE_PERIOD --> ACTIVE : Payment received\n(webhook: invoice_paid)
    GRACE_PERIOD --> CANCELLED : Admin cancel

    SUSPENDED --> ACTIVE : Admin reactivation / payment
    SUSPENDED --> CANCELLED : Admin cancel

    CANCELLED --> [*] : Terminal — no transitions

    note right of TRIALING : BillingAccount.TrialEndsAtUtc\ncontrols expiry
    note right of GRACE_PERIOD : BillingAccount.GracePeriodEndsAtUtc\ncontrols expiry
```

---

## 4. Flujo de Renovación Automática

```mermaid
sequenceDiagram
    participant Hang as Hangfire (hourly)
    participant Job as SubscriptionRenewalJob
    participant Svc as SubscriptionRenewalService
    participant PP as IPaymentProviderFactory
    participant Prov as IPaymentProvider
    participant Orch as ISubscriptionLifecycleOrchestrator
    participant Notif as IBillingNotificationService
    participant DB as PostgreSQL

    Hang->>Job: ExecuteAsync()
    Job->>Svc: ProcessUpcomingRenewalsAsync(24h)
    Svc->>DB: SELECT billing accounts due in 24h
    loop Per subscriber
        Svc->>PP: GetForSubscriber(subscriberId)
        alt Manual provider (PaymentProviderType.None)
            Svc->>DB: Advance period, mark Active
        else Auto provider (Stripe/Kushki)
            Svc->>Prov: ChargeInvoiceAsync(request)
            alt Payment succeeded
                Prov-->>Svc: PaymentAttemptData(Succeeded)
                Svc->>DB: Advance period
                Svc->>Notif: NotifySubscriptionRenewedAsync(...)
            else Payment failed
                Prov-->>Svc: PaymentAttemptData(Failed)
                Svc->>Orch: EnterGracePeriodAsync(7 days)
                Svc->>Notif: NotifyPaymentFailedAsync(...)
            end
        end
    end
```

---

## 5. Flujo de Invoice

```mermaid
stateDiagram-v2
    [*] --> DRAFT : SaasBillingInvoice.CreateDraft()

    DRAFT --> OPEN : Issue(dueAtUtc)
    DRAFT --> VOID : Void() — never sent

    OPEN --> PAID : MarkPaid()\nOR webhook: invoice_paid
    OPEN --> VOID : Admin void
    OPEN --> UNCOLLECTIBLE : Non-recoverable

    PAID --> [*] : Terminal
    VOID --> [*] : Terminal
    UNCOLLECTIBLE --> [*] : Terminal

    note right of PAID : MUST have ErpInvoiceId\nbefore marking Paid\n(ZH Technologies fiscal invoice)
```

---

## 6. Flujo de Grace Period

```mermaid
sequenceDiagram
    participant Job as CheckSubscriptionExpiryJob
    participant Orch as SubscriptionLifecycleOrchestrator
    participant Cache as ISubscriptionAccessCache
    participant DB as PostgreSQL
    participant MW as SubscriptionAccessMiddleware

    Note over Job: Runs every minute (Hangfire)
    Job->>DB: Load all subscribers
    loop Per subscriber in Trial
        Job->>DB: Load BillingAccount
        alt TrialEndsAtUtc < now AND Status == Trialing
            Job->>Orch: EnterGracePeriodAsync(7 days)
            Orch->>DB: UPDATE subscriber (GracePeriod)\nUPDATE billing_account (GracePeriod)
            Orch->>Cache: InvalidateAsync(subscriberId)
        end
    end

    Note over MW: Next request from subscriber
    MW->>Cache: TryGetAsync(subscriberId)
    Cache-->>MW: Snapshot(ReadOnly, GracePeriodExpired)
    MW->>MW: X-Subscription-Access-Mode: ReadOnly header
    MW->>MW: Allow request (ReadOnly)
```

---

## 7. Flujo de Suspensión Automática

```mermaid
sequenceDiagram
    participant Job as CheckSubscriptionExpiryJob
    participant Orch as SubscriptionLifecycleOrchestrator
    participant Cache as ISubscriptionAccessCache
    participant Notif as IBillingNotificationService
    participant DB as PostgreSQL
    participant MW as SubscriptionAccessMiddleware

    Note over Job: GracePeriod expired
    Job->>DB: Load subscriber (GracePeriod)
    Job->>DB: Load BillingAccount
    alt GracePeriodEndsAtUtc < now AND Status == GracePeriod
        Job->>Orch: SuspendAsync("Período de gracia vencido")
        Orch->>DB: UPDATE subscriber (Suspended, IsActive=false)\nUPDATE billing_account (Suspended)
        Orch->>Cache: InvalidateAsync(subscriberId)
        Orch->>Notif: NotifySubscriptionSuspendedAsync(...)
    end

    Note over MW: Next request from suspended subscriber
    MW->>Cache: TryGetAsync(subscriberId)
    Cache-->>MW: Snapshot(Blocked, Suspended)
    MW-->>Client: 403 { code: subscription_suspended }
```

---

## 8. Flujo de Reactivación

```mermaid
sequenceDiagram
    participant Admin as PlatformOperator
    participant Ctrl as PlatformSubscriberLifecycleController
    participant Handler as ActivateSubscriberHandler
    participant Orch as SubscriptionLifecycleOrchestrator
    participant Cache as ISubscriptionAccessCache
    participant DB as PostgreSQL

    Admin->>Ctrl: PATCH /api/platform/subscribers/{id}/activate
    Ctrl->>Handler: ActivateSubscriberCommand
    Handler->>Orch: ActivateAsync(subscriberId, actorId)
    Orch->>DB: BEGIN TRANSACTION (RepeatableRead)
    Orch->>DB: SELECT subscriber (with xmin lock)
    Orch->>DB: SELECT billing_account (with xmin lock)
    Orch->>DB: UPDATE subscriber (Active)\nUPDATE billing_account (Active)
    Orch->>DB: COMMIT
    Orch->>Cache: InvalidateAsync(subscriberId)
    Orch->>Orch: AuditLog(SubscriberActivated, source=PlatformOperator)
    Handler-->>Admin: 200 OK
```

---

## Ownership de Aggregates

| Aggregate | Source of Truth | Responsabilidad |
|---|---|---|
| `Subscriber` | Lifecycle (Active/Trial/GracePeriod/Suspended/Inactive) | Identidad SaaS, acceso a plataforma |
| `SubscriberBillingAccount` | Estado de cuenta (Trialing/Active/PastDue/GracePeriod/Suspended/Cancelled) | Fechas de trial/grace/período, estado de pago |
| `SubscriberSubscription` | Plan asignado (Active/Cancelled/etc.) | Entitlements de plan |
| `SaasBillingInvoice` | Invoice de cobro SaaS | Historial de cobros, link a factura ERP |
| `BillingPaymentAttempt` | Intentos de cobro | Reintentos, trazabilidad |
| `BillingCheckoutSession` | Sesión de checkout | URL de pago, expiración |
| `BillingEvent` | Audit inmutable | Log de todo evento de billing |

---

## Separación de Concerns

```
SubscriptionLifecycleOrchestrator
  → controla platform access (Active/Suspended/etc.)
  → NO conoce features ni quotas

ISubscriptionAccessService (cache-first)
  → evalúa gate de acceso (Blocked/ReadOnly/FullAccess)
  → NO modifica estado

IFeatureAccessService
  → evalúa features y quotas de plan
  → completamente independiente del lifecycle

IPaymentProvider (+ factory)
  → abstrae Stripe/Kushki/PayPal
  → NO conoce lifecycle ni features
```

---

## Providers Soportados (Futuro)

| Provider | Código | Estado |
|---|---|---|
| Manual (sin proveedor) | `none` | ✅ Activo (NullPaymentProvider) |
| Stripe | `stripe` | 🔜 Próxima fase |
| Kushki | `kushki` | 🔜 Próxima fase |
| PayPal | `paypal` | 🔜 Próxima fase |
| MercadoPago | `mercadopago` | 🔜 Futuro |
| DataFast | `datafast` | 🔜 Futuro |

Para agregar un nuevo provider:
1. Implementar `IPaymentProvider`
2. Registrar en `PaymentProviderFactory.GetByType()`
3. Configurar registro DI
4. NO cambiar ninguna otra clase

---

## Endpoints de Billing

| Endpoint | Estado | Descripción |
|---|---|---|
| `GET /api/billing/subscription` | ✅ Activo | Estado de cuenta + billing |
| `GET /api/billing/invoices` | ✅ Activo | Historial de facturas SaaS |
| `POST /api/billing/checkout` | 🔜 Stub 501 | Crear sesión de pago |
| `POST /api/billing/cancel` | 🔜 Stub 501 | Cancelar suscripción |
| `POST /api/billing/reactivate` | 🔜 Stub 501 | Reactivar suscripción |
| `POST /billing/webhooks/{provider}` | ✅ Pipeline listo | Recibir webhooks del provider |
