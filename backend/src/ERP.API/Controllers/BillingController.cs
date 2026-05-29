using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Billing.DTOs;
using ERP.Application.Billing.Invoices;
using ERP.Application.Billing.PaymentProviders;
using ERP.Application.Billing.UseCases.GetBillingAccount;
using ERP.Application.Billing.UseCases.ListBillingInvoices;
using ERP.Application.Common;
using ERP.Application.Common.Subscriptions;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Billing.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Billing portal for authenticated subscribers.
/// Uses NullPaymentProvider for simulated checkout (no Stripe yet).
/// </summary>
[ApiController]
[Route("api/billing")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
public sealed class BillingController : ControllerBase
{
    private readonly IMediator                    _mediator;
    private readonly ICurrentSubscriber           _currentSubscriber;
    private readonly ICurrentUser                 _currentUser;
    private readonly IBillingInvoiceService       _invoiceService;
    private readonly IPaymentProviderFactory      _providerFactory;
    private readonly ISubscriberBillingRepository _billing;
    private readonly ISubscriberRepository        _subscribers;

    public BillingController(
        IMediator mediator,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        IBillingInvoiceService invoiceService,
        IPaymentProviderFactory providerFactory,
        ISubscriberBillingRepository billing,
        ISubscriberRepository subscribers)
    {
        _mediator          = mediator;
        _currentSubscriber = currentSubscriber;
        _currentUser       = currentUser;
        _invoiceService    = invoiceService;
        _providerFactory   = providerFactory;
        _billing           = billing;
        _subscribers       = subscribers;
    }

    // ── GET /api/billing/subscription ────────────────────────────────────────

    [HttpGet("subscription")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberBillingAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscription(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBillingAccountQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ── GET /api/billing/invoices ─────────────────────────────────────────────

    [HttpGet("invoices")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SaasBillingInvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoices([FromQuery] int take = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListBillingInvoicesQuery(Math.Clamp(take, 1, 100)), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ── POST /api/billing/checkout (simulated) ────────────────────────────────

    /// <summary>
    /// Creates a simulated checkout session using NullPaymentProvider.
    /// With a real provider: returns a hosted checkout URL to redirect to.
    /// Without provider: simulates checkout and returns a local success URL.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(ApiResponse<CheckoutSessionResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutRequest body, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        if (subscriberId == Guid.Empty)
            return Forbid();

        var subscriber = await _subscribers.GetByIdAsync(subscriberId, ct);
        if (subscriber is null)
            return this.ApiNotFound("Suscriptor no encontrado.");

        var account = await _billing.GetAccountBySubscriberIdAsync(subscriberId, ct);
        if (account is null)
            return this.ApiBadRequest("Cuenta de facturación no configurada.");

        var provider = _providerFactory.GetForSubscriber(subscriberId);

        var session = BillingCheckoutSession.Create(
            subscriberId:    subscriberId,
            providerType:    provider.ProviderType,
            planCode:        body.PlanCode,
            commercialPlanId: null,
            billingCycle:    body.BillingCycle.ToLowerInvariant() == "yearly"
                ? BillingPeriodType.Yearly : BillingPeriodType.Monthly,
            amount:          0m,   // resolved by provider or admin
            currencyCode:    account.CurrencyCode,
            expiresAtUtc:    DateTime.UtcNow.AddHours(1),
            createdBy:       _currentUser.UserId);

        var checkoutRequest = new CreateCheckoutSessionRequest(
            SubscriberId:    subscriberId,
            ExternalCustomerId: string.Empty,
            PriceId:         body.PlanCode,
            CurrencyCode:    account.CurrencyCode,
            Amount:          0m,
            BillingCycle:    body.BillingCycle.ToLowerInvariant() == "yearly"
                ? Application.Billing.PaymentProviders.BillingCycleType.Yearly
                : Application.Billing.PaymentProviders.BillingCycleType.Monthly,
            SuccessUrl:      $"/billing/checkout/success?session={session.Id}",
            CancelUrl:       "/billing/checkout/cancel");

        var result = await provider.CreateCheckoutSessionAsync(checkoutRequest, ct);
        if (!result.Succeeded || result.Data is null)
            return this.ApiBadRequest(result.ErrorMessage ?? "Error al crear sesión de checkout.");

        session.SetProviderSession(result.Data.SessionId, result.Data.CheckoutUrl, _currentUser.UserId);
        await _billing.AddCheckoutSessionAsync(session, ct);
        await _billing.SaveChangesAsync(ct);

        return this.ApiOk(new CheckoutSessionResponseDto(
            session.Id,
            result.Data.CheckoutUrl,
            result.Data.ExpiresAtUtc,
            session.Status.ToString()));
    }

    // ── POST /api/billing/cancel ──────────────────────────────────────────────

    [HttpPost("cancel")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelSubscription([FromBody] CancelBillingRequest? body, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        if (subscriberId == Guid.Empty) return Forbid();

        var account = await _billing.GetAccountTrackedBySubscriberIdAsync(subscriberId, ct);
        if (account is null) return this.ApiNotFound("Cuenta de facturación no encontrada.");
        if (account.Status == BillingAccountStatus.Cancelled)
            return this.ApiBadRequest("La suscripción ya está cancelada.");

        account.SetRenewalState(BillingRenewalState.PendingCancellation, _currentUser.UserId);
        await _billing.AddBillingEventAsync(
            BillingEvent.Record(subscriberId, "billing.cancellation.scheduled",
                BillingEventSource.Admin, _currentUser.UserId,
                $"{{\"reason\":\"{body?.Reason ?? "self-service"}\"}}"),
            ct);
        await _billing.SaveChangesAsync(ct);

        return this.ApiOk(true);
    }

    // ── POST /api/billing/reactivate ──────────────────────────────────────────

    [HttpPost("reactivate")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reactivate(CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        if (subscriberId == Guid.Empty) return Forbid();

        var account = await _billing.GetAccountTrackedBySubscriberIdAsync(subscriberId, ct);
        if (account is null) return this.ApiNotFound("Cuenta de facturación no encontrada.");
        if (account.RenewalState != BillingRenewalState.PendingCancellation)
            return this.ApiBadRequest("No hay cancelación pendiente que revertir.");

        account.SetRenewalState(BillingRenewalState.Active, _currentUser.UserId);
        await _billing.AddBillingEventAsync(
            BillingEvent.Record(subscriberId, "billing.cancellation.reverted",
                BillingEventSource.Admin, _currentUser.UserId, null),
            ct);
        await _billing.SaveChangesAsync(ct);

        return this.ApiOk(true);
    }
}

// ── Request/Response DTOs ─────────────────────────────────────────────────────

public sealed record CreateCheckoutRequest(string PlanCode, string BillingCycle = "monthly");
public sealed record CancelBillingRequest(string? Reason = null);
public sealed record CheckoutSessionResponseDto(
    Guid     SessionId,
    string   CheckoutUrl,
    DateTime ExpiresAtUtc,
    string   Status);
