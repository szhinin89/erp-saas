using ERP.Domain.Modules.Finance.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce SupplierPaymentReversedEvent (Finance/Payment) a PostingFact e invoca IPostingEngine —
/// no crea JournalEntry, no resuelve cuentas, no contiene lógica financiera (ADR-026 §8, Fase
/// 5.6.5). Mismo criterio que <see cref="CollectionReversedPostingTranslator"/>: sin reverso
/// automático de ningún JournalEntry existente, sin desglose de impuestos, GrandTotal transporta
/// el monto reversado, fecha del hecho contable tomada de BaseDomainEvent.OccurredOn.
/// </summary>
public sealed class SupplierPaymentReversedPostingTranslator : INotificationHandler<SupplierPaymentReversedEvent>
{
    private const string SourceModuleName = "Finance";
    private const string FactTypeName = "SupplierPaymentReversed";

    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<SupplierPaymentReversedPostingTranslator> _logger;

    public SupplierPaymentReversedPostingTranslator(
        IPostingEngine postingEngine, ILogger<SupplierPaymentReversedPostingTranslator> logger)
    {
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(SupplierPaymentReversedEvent e, CancellationToken ct)
    {
        var fact = new PostingFact(
            e.TenantId!.Value, e.CompanyId, SourceModuleName, FactTypeName, e.PaymentId,
            DateOnly.FromDateTime(e.OccurredOn), 0m, 0m, 0m, 0m, e.Amount);

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for SupplierPaymentReversed {PaymentId}: {Code} — {Error}",
                e.PaymentId, result.Code, result.Error);
        }
    }
}
