using ERP.Domain.Modules.Purchases.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <see cref="PurchaseReturnCancelledEvent"/> (P0-02 Fase 10) al hecho contable reverso
/// del de <see cref="PurchaseReturnAuthorizedPostingTranslator"/> (§19.1bis) — mismo criterio: no
/// crea <c>JournalEntry</c>, no resuelve cuentas, mismos montos snapshot exactos del evento
/// (nunca recalcula <c>CostVarianceTotal</c>). El <c>PostingRule</c> configurado para
/// <c>FactType="PurchaseReturnCancelled"</c> es responsabilidad de administración contable
/// (invierte débito/crédito respecto al de autorización) — no de este traductor. Si la
/// cancelación ocurrió desde <c>Draft</c> (nunca hubo hecho contable que reversar, todos los
/// snapshots del evento son <c>null</c>), no publica ningún <c>PostingFact</c>.
/// </summary>
public sealed class PurchaseReturnCancelledPostingTranslator
    : INotificationHandler<PurchaseReturnCancelledEvent>
{
    private const string SourceModuleName = "Purchases";
    private const string FactTypeName = "PurchaseReturnCancelled";

    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<PurchaseReturnCancelledPostingTranslator> _logger;

    public PurchaseReturnCancelledPostingTranslator(
        IPostingEngine postingEngine,
        ILogger<PurchaseReturnCancelledPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(PurchaseReturnCancelledEvent e, CancellationToken ct)
    {
        if (e.AppliedToPayableAmount is null)
            return;

        var costVarianceDebit = Math.Max(e.CostVarianceTotal!.Value, 0m);
        var costVarianceCredit = Math.Max(-e.CostVarianceTotal!.Value, 0m);

        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.PurchaseReturnId,
            DateOnly.FromDateTime(e.OccurredOn),
            Subtotal: 0m,
            TotalVat: e.AuthorizedVatTotal!.Value,
            TotalIce: e.AuthorizedIceTotal!.Value,
            TotalDiscount: 0m,
            GrandTotal: 0m,
            AppliedToPayableAmount: e.AppliedToPayableAmount,
            SupplierCreditAmount: e.SupplierCreditAmount,
            CostVarianceDebitAmount: costVarianceDebit,
            CostVarianceCreditAmount: costVarianceCredit,
            HistoricalCostTotal: e.HistoricalCostTotal,
            TotalIrbpnr: e.AuthorizedIrbpnrTotal
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for PurchaseReturn {PurchaseReturnId} cancellation ({ReturnNumber}): {Code} — {Error}",
                e.PurchaseReturnId,
                e.ReturnNumber,
                result.Code,
                result.Error
            );
        }
    }
}
