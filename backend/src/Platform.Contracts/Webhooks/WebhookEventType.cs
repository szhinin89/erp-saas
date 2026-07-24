namespace Platform.Contracts.Webhooks;

/// <summary>
/// Identifies the kind of event delivered in a <see cref="WebhookEnvelope{TPayload}"/>.
/// </summary>
public enum WebhookEventType
{
    ProductCreated,
    InvoiceIssued,
    StockAdjusted,
}
