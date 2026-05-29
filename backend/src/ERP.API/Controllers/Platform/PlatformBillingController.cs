using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Billing.Invoices;
using ERP.Application.Common.Subscriptions;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Billing.Interfaces;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>Platform Layer — SaaS billing aggregates + simulated payment (control plane).</summary>
[ApiController]
[Route("api/platform/billing")]
[Authorize(Roles = PlatformAuthorizationRoles.BillingReaders)]
[Tags("Platform")]
public sealed class PlatformBillingController : ControllerBase
{
    private readonly ISubscriberRepository              _subscribers;
    private readonly ISubscriberBillingRepository       _billing;
    private readonly IBillingInvoiceService             _invoiceService;
    private readonly ISubscriptionLifecycleOrchestrator _lifecycle;

    public PlatformBillingController(
        ISubscriberRepository subscribers,
        ISubscriberBillingRepository billing,
        IBillingInvoiceService invoiceService,
        ISubscriptionLifecycleOrchestrator lifecycle)
    {
        _subscribers    = subscribers;
        _billing        = billing;
        _invoiceService = invoiceService;
        _lifecycle      = lifecycle;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var all       = await _subscribers.GetAllAsync(ct);
        var active    = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.Active);
        var suspended = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.Suspended);
        var grace     = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.GracePeriod);
        var trial     = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.Trial);
        var pastDue   = await _billing.GetPastDueAccountsAsync(ct);

        return this.ApiOk(new
        {
            totals = new { subscribers = all.Count, active, suspended, gracePeriod = grace, trial, overdueAccounts = pastDue.Count },
        });
    }

    [HttpGet("invoices")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Invoices([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var invoices      = await _billing.GetRecentInvoicesPlatformAsync(take, ct);
        var subscriberMap = (await _subscribers.GetAllAsync(ct)).ToDictionary(s => s.Id, s => s.Name);

        var rows = invoices.Select(i => new
        {
            i.Id,
            i.SubscriberId,
            subscriberName = subscriberMap.GetValueOrDefault(i.SubscriberId),
            i.InvoiceNumber,
            status  = i.Status.ToString(),
            i.Total,
            i.CurrencyCode,
            issuedAtUtc = i.IssuedAtUtc,
            dueAtUtc    = i.DueAtUtc,
            paidAtUtc   = i.PaidAtUtc,
        });

        return this.ApiOk(new { invoices = rows });
    }

    [HttpGet("overdue")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Overdue(CancellationToken ct)
    {
        var pastDue = await _billing.GetPastDueAccountsAsync(ct);
        var all     = await _subscribers.GetAllAsync(ct);
        var map     = all.ToDictionary(s => s.Id);

        var rows = pastDue.Select(a =>
        {
            map.TryGetValue(a.SubscriberId, out var sub);
            return new { a.SubscriberId, subscriberName = sub?.Name, lifecycle = sub?.LifecycleStatus.ToString(), a.Status, a.GracePeriodEndsAtUtc, a.CurrentPeriodEndUtc };
        });

        return this.ApiOk(new { accounts = rows });
    }

    // ── POST simulate-payment ─────────────────────────────────────────────────

    /// <summary>
    /// Simulates a successful payment: creates invoice → attempts → marks paid → activates lifecycle.
    /// No real provider. For admin testing and manual activation.
    /// </summary>
    [HttpPost("{subscriberId:guid}/simulate-payment")]
    [Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
    [ProducesResponseType(typeof(ApiResponse<SimulatePaymentResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SimulatePayment(
        Guid subscriberId,
        [FromBody] SimulatePaymentRequest body,
        CancellationToken ct)
    {
        var subscriber = await _subscribers.GetByIdAsync(subscriberId, ct);
        if (subscriber is null) return this.ApiNotFound("Suscriptor no encontrado.");

        var account = await _billing.GetAccountBySubscriberIdAsync(subscriberId, ct);
        if (account is null) return this.ApiBadRequest("BillingAccount no existe para este suscriptor.");

        var now       = DateTime.UtcNow;
        var amount    = (body.Amount ?? 0m) > 0 ? body.Amount!.Value : 49.00m;
        var currency  = string.IsNullOrWhiteSpace(body.Currency) ? account.CurrencyCode : body.Currency;
        var planCode  = body.PlanCode ?? subscriber.PlanCode ?? "starter";
        var periodEnd = body.PeriodEndUtc ?? now.AddMonths(1);
        var sysId     = ERP.Infrastructure.SaaS.SubscriptionLifecycleOrchestrator.SystemActorId;

        var invoiceId = await _invoiceService.EnsureRenewalInvoiceAsync(
            subscriberId, planCode, amount, currency, now, periodEnd, sysId, ct);

        // Activate lifecycle FIRST — if this fails, invoice is not yet marked paid → retryable safely
        await _lifecycle.ActivateAsync(subscriberId, sysId,
            $"Pago simulado por plataforma", ct);

        // Record attempt + mark invoice paid AFTER lifecycle is consistent
        var attemptId   = await _invoiceService.RecordPaymentAttemptAsync(
            invoiceId, subscriberId, PaymentProviderType.Manual, amount, currency, sysId, ct);

        var providerRef = $"sim-{Guid.NewGuid():N}"[..20];
        await _invoiceService.CompletePaymentAsync(attemptId, invoiceId, subscriberId, providerRef, sysId, ct);

        return this.ApiOk(new SimulatePaymentResultDto(subscriberId, invoiceId, attemptId, providerRef, "PaymentSimulated"));
    }

    // ── POST simulate-payment-failure ─────────────────────────────────────────

    /// <summary>Simulates a failed payment → enters grace period. For testing suspension flows.</summary>
    [HttpPost("{subscriberId:guid}/simulate-payment-failure")]
    [Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SimulatePaymentFailure(
        Guid subscriberId,
        [FromBody] SimulatePaymentFailureRequest? body,
        CancellationToken ct)
    {
        var subscriber = await _subscribers.GetByIdAsync(subscriberId, ct);
        if (subscriber is null) return this.ApiNotFound("Suscriptor no encontrado.");

        var graceDays   = body?.GraceDays > 0 ? body.GraceDays : 7;
        var graceEnd    = DateTime.UtcNow.AddDays(graceDays);

        await _lifecycle.EnterGracePeriodAsync(
            subscriberId,
            graceEnd,
            ERP.Infrastructure.SaaS.SubscriptionLifecycleOrchestrator.SystemActorId,
            ct);

        return this.ApiOk(new { subscriberId, status = "GracePeriodEntered", gracePeriodEndsAtUtc = graceEnd });
    }
}

public sealed record SimulatePaymentRequest(
    decimal? Amount = null, string? Currency = null, string? PlanCode = null, DateTime? PeriodEndUtc = null);

public sealed record SimulatePaymentFailureRequest(int GraceDays = 7);

public sealed record SimulatePaymentResultDto(
    Guid SubscriberId, Guid InvoiceId, Guid AttemptId, string ProviderReference, string Status);
