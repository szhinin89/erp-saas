using ERP.Domain.Modules.Purchases.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <see cref="PurchaseReturnAuthorizedEvent"/> (P0-02 Fase 6) al hecho compuesto
/// balanceado de diseño §19.1bis e invoca <see cref="IPostingEngine"/> — no crea
/// <c>JournalEntry</c>, no resuelve cuentas, no contiene lógica financiera (mismo criterio que
/// <c>PurchaseInvoiceConfirmedPostingTranslator</c>/<c>SalesReturnAuthorizedPostingTranslator</c>,
/// ADR-026 §8). Único hecho contable por autorización — nunca dos eventos paralelos para la misma
/// devolución (§19.1bis, "un único hecho compuesto, nunca doble contabilización").
///
/// Usa los 5 campos de <see cref="PostingFact"/> agregados en la Remediación 01 de esta fase
/// (<c>AppliedToPayableAmount</c>/<c>SupplierCreditAmount</c>/<c>CostVarianceDebitAmount</c>/
/// <c>CostVarianceCreditAmount</c>/<c>HistoricalCostTotal</c>) + los campos ya existentes
/// <c>TotalVat</c>/<c>TotalIce</c> para <c>ReturnedVatAmount</c>/<c>ReturnedIceAmount</c> — nunca
/// reutiliza <c>Subtotal</c>/<c>Discount</c>/<c>GrandTotal</c> para estos conceptos (evita
/// ambigüedad de trazabilidad contable).
/// </summary>
public sealed class PurchaseReturnAuthorizedPostingTranslator
    : INotificationHandler<PurchaseReturnAuthorizedEvent>
{
    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<PurchaseReturnAuthorizedPostingTranslator> _logger;

    public PurchaseReturnAuthorizedPostingTranslator(
        IPostingEngine postingEngine,
        ILogger<PurchaseReturnAuthorizedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(PurchaseReturnAuthorizedEvent e, CancellationToken ct)
    {
        var fact = PurchaseReturnPostingFactBuilder.Build(
            e.TenantId!.Value,
            e.CompanyId,
            e.PurchaseReturnId,
            DateOnly.FromDateTime(e.OccurredOn),
            e.AuthorizedVatTotal,
            e.AuthorizedIceTotal,
            e.AuthorizedIrbpnrTotal,
            e.AppliedToPayableAmount,
            e.SupplierCreditAmount,
            e.CostVarianceTotal,
            e.HistoricalCostTotal
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for PurchaseReturn {PurchaseReturnId} ({ReturnNumber}): {Code} — {Error}",
                e.PurchaseReturnId,
                e.ReturnNumber,
                result.Code,
                result.Error
            );
        }
    }
}
