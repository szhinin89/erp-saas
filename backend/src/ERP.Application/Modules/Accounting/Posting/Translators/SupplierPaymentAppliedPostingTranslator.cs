using ERP.Domain.Modules.Finance.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce SupplierPaymentAppliedEvent (Finance/Payment) a PostingFact e invoca IPostingEngine —
/// no crea JournalEntry, no resuelve cuentas, no contiene lógica financiera (ADR-026 §8, Fase
/// 5.6.1). Mismo criterio que <see cref="CollectionAppliedPostingTranslator"/>: sin desglose de
/// impuestos (Subtotal/TotalVat/TotalIce/TotalDiscount en cero, no inventados), GrandTotal
/// transporta el monto pagado.
/// </summary>
public sealed class SupplierPaymentAppliedPostingTranslator
    : INotificationHandler<SupplierPaymentAppliedEvent>
{
    private const string SourceModuleName = "Finance";
    private const string FactTypeName = "SupplierPaymentApplied";

    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<SupplierPaymentAppliedPostingTranslator> _logger;

    public SupplierPaymentAppliedPostingTranslator(
        IPostingEngine postingEngine,
        ILogger<SupplierPaymentAppliedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(SupplierPaymentAppliedEvent e, CancellationToken ct)
    {
        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.PaymentId,
            e.PaymentDate,
            0m,
            0m,
            0m,
            0m,
            e.Amount
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for SupplierPayment {PaymentId}: {Code} — {Error}",
                e.PaymentId,
                result.Code,
                result.Error
            );
        }
    }
}
