using ERP.Application.Billing.PaymentProviders;
using ERP.Domain.Billing.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Receives webhooks from payment providers (Stripe, Kushki, PayPal, etc.).
///
/// Security: raw body is required for signature validation — do NOT parse before validation.
/// Each provider has its own signature header (e.g., Stripe-Signature, X-Kushki-Signature).
///
/// Flow:
///   Provider → POST /billing/webhooks/{provider}
///   → ValidateWebhookAsync() (signature check)
///   → ParseWebhookEventAsync() (map to ProviderWebhookEvent)
///   → IPaymentWebhookProcessor.ProcessAsync() (lifecycle/billing actions)
///   → 200 OK (provider retries on non-2xx)
/// </summary>
[ApiController]
[Route("billing/webhooks")]
public sealed class BillingWebhookController : ControllerBase
{
    private readonly IPaymentProviderFactory  _providerFactory;
    private readonly IPaymentWebhookProcessor _processor;
    private readonly ILogger<BillingWebhookController> _logger;

    public BillingWebhookController(
        IPaymentProviderFactory providerFactory,
        IPaymentWebhookProcessor processor,
        ILogger<BillingWebhookController> logger)
    {
        _providerFactory = providerFactory;
        _processor       = processor;
        _logger          = logger;
    }

    /// <summary>
    /// Generic webhook endpoint — routes by provider code in the URL.
    /// e.g., POST /billing/webhooks/stripe, /billing/webhooks/kushki
    /// </summary>
    [HttpPost("{providerCode}")]
    public async Task<IActionResult> ReceiveWebhook(
        [FromRoute] string providerCode,
        CancellationToken ct)
    {
        var providerType = ResolveProviderType(providerCode);
        var provider     = _providerFactory.GetByType(providerType);

        // Read raw body — required for signature validation
        using var reader  = new StreamReader(Request.Body);
        var rawBodyString = await reader.ReadToEndAsync(ct);
        var rawBodyBytes  = System.Text.Encoding.UTF8.GetBytes(rawBodyString);

        // Collect all headers as string dictionary for signature validation
        var headers = Request.Headers
            .ToDictionary(h => h.Key, h => h.Value.ToString(),
                StringComparer.OrdinalIgnoreCase);

        // Step 1: Validate signature
        var validated = await provider.ValidateWebhookAsync(rawBodyBytes, headers, ct);
        if (validated is null)
        {
            _logger.LogWarning(
                "BillingWebhookController: invalid signature from provider={Provider}", providerCode);
            return BadRequest(new { error = "invalid_signature" });
        }

        // Step 2: Parse to domain event
        var evt = await provider.ParseWebhookEventAsync(validated, ct);
        if (evt is null)
        {
            _logger.LogDebug(
                "BillingWebhookController: unrecognized event type={RawType} from {Provider}",
                validated.RawEventType, providerCode);
            return Ok(new { status = "ignored" }); // return 200 to prevent retries
        }

        // Step 3: Process
        try
        {
            await _processor.ProcessAsync(evt, ct);
            return Ok(new { status = "processed", eventId = validated.EventId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "BillingWebhookController: processing failed for event={EventId} from {Provider}",
                validated.EventId, providerCode);
            // Return 500 so provider retries
            return StatusCode(500, new { error = "processing_failed" });
        }
    }

    private static PaymentProviderType ResolveProviderType(string providerCode) =>
        providerCode.ToLowerInvariant() switch
        {
            PaymentProviderCodes.Stripe     => PaymentProviderType.Stripe,
            PaymentProviderCodes.Paddle     => PaymentProviderType.Paddle,
            PaymentProviderCodes.PayPal     => PaymentProviderType.PayPal,
            PaymentProviderCodes.Kushki     => PaymentProviderType.Stripe, // mapped until Kushki enum added
            _                               => PaymentProviderType.None,
        };
}
