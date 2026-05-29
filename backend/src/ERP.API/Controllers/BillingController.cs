using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Billing.DTOs;
using ERP.Application.Billing.UseCases.GetBillingAccount;
using ERP.Application.Billing.UseCases.ListBillingInvoices;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Billing portal for authenticated subscribers.
/// Exposes read-only billing info and self-service actions (cancel, reactivate).
///
/// FUTURE PHASES:
///   POST /billing/checkout     → CreateCheckoutSession → ProviderCheckoutUrl
///   POST /billing/cancel       → CancelSubscription → SubscriptionLifecycleOrchestrator
///   POST /billing/reactivate   → ReactivateSubscription
///   GET  /billing/payment-methods → list saved payment methods
///
/// This controller only exposes SAFE, read-only endpoints for now.
/// Write operations are added in future phases when providers are connected.
/// </summary>
[ApiController]
[Route("api/billing")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
public sealed class BillingController : ControllerBase
{
    private readonly IMediator _mediator;

    public BillingController(IMediator mediator) => _mediator = mediator;

    // ── GET /api/billing/subscription ────────────────────────────────────────

    /// <summary>Returns the billing account + subscription status for the authenticated subscriber.</summary>
    [HttpGet("subscription")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberBillingAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscription(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBillingAccountQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ── GET /api/billing/invoices ─────────────────────────────────────────────

    /// <summary>Returns the last 20 SaaS invoices for the authenticated subscriber.</summary>
    [HttpGet("invoices")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SaasBillingInvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoices([FromQuery] int take = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListBillingInvoicesQuery(Math.Clamp(take, 1, 100)), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ── POST /api/billing/checkout (FUTURE — stub for architecture) ───────────

    /// <summary>
    /// Creates a checkout session to subscribe/upgrade a plan.
    /// STUB — returns 501 until IPaymentProvider is connected.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult CreateCheckout([FromBody] CreateCheckoutRequest body) =>
        StatusCode(501, new { error = "payment_providers_not_configured",
            message = "Checkout will be available once a payment provider is connected." });

    // ── POST /api/billing/cancel (FUTURE — stub) ──────────────────────────────

    /// <summary>
    /// Cancels the subscription at end of current period.
    /// STUB — returns 501 until IPaymentProvider is connected.
    /// </summary>
    [HttpPost("cancel")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult CancelSubscription([FromBody] CancelSubscriptionRequest? body) =>
        StatusCode(501, new { error = "payment_providers_not_configured",
            message = "Cancellation will be available once a payment provider is connected." });

    // ── POST /api/billing/reactivate (FUTURE — stub) ─────────────────────────

    /// <summary>
    /// Reactivates a subscription that was set to cancel at period end.
    /// STUB — returns 501 until IPaymentProvider is connected.
    /// </summary>
    [HttpPost("reactivate")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Reactivate() =>
        StatusCode(501, new { error = "payment_providers_not_configured" });
}

// ── Request DTOs (stubs for future) ──────────────────────────────────────────

public sealed record CreateCheckoutRequest(string PlanCode, string BillingCycle = "monthly");
public sealed record CancelSubscriptionRequest(string? Reason = null);
