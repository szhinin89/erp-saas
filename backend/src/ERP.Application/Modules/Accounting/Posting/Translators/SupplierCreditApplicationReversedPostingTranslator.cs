using ERP.Domain.Modules.Purchases.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <see cref="SupplierCreditApplicationReversedEvent"/> (P0-02 Fase 7) al hecho contable
/// reverso del de <see cref="SupplierCreditAppliedPostingTranslator"/> (§9.3) — mismo criterio: no
/// crea <c>JournalEntry</c>, no resuelve cuentas, un único monto en <c>GrandTotal</c>. El
/// <c>PostingRule</c> configurado para <c>FactType="SupplierCreditApplicationReversed"</c> es
/// responsabilidad de administración contable (invierte débito/crédito respecto al de
/// aplicación) — no de este traductor.
/// </summary>
public sealed class SupplierCreditApplicationReversedPostingTranslator
    : INotificationHandler<SupplierCreditApplicationReversedEvent>
{
    private const string SourceModuleName = "Purchases";
    private const string FactTypeName = "SupplierCreditApplicationReversed";

    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<SupplierCreditApplicationReversedPostingTranslator> _logger;

    public SupplierCreditApplicationReversedPostingTranslator(
        IPostingEngine postingEngine,
        ILogger<SupplierCreditApplicationReversedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(SupplierCreditApplicationReversedEvent e, CancellationToken ct)
    {
        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.SupplierCreditMovementId,
            DateOnly.FromDateTime(e.OccurredOn),
            Subtotal: 0m,
            TotalVat: 0m,
            TotalIce: 0m,
            TotalDiscount: 0m,
            GrandTotal: e.Amount
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for SupplierCredit {SupplierCreditId} reversal {MovementId}: {Code} — {Error}",
                e.SupplierCreditId,
                e.SupplierCreditMovementId,
                result.Code,
                result.Error
            );
        }
    }
}
