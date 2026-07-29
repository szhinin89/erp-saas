using ERP.Domain.Modules.Finance.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce CollectionAppliedEvent (Finance/Payment) a PostingFact e invoca IPostingEngine — no
/// crea JournalEntry, no resuelve cuentas, no contiene lógica financiera (ADR-026 §8, Fase 5.6.1).
/// Un cobro no tiene desglose de impuestos: Subtotal/TotalVat/TotalIce/TotalDiscount van en cero
/// deliberadamente (no son "montos inventados" — un cobro real no tiene esos componentes) y
/// GrandTotal transporta el monto cobrado, mismo campo genérico que ya usan los traductores de
/// Ventas/Compras para "el monto total del hecho".
/// </summary>
public sealed class CollectionAppliedPostingTranslator : INotificationHandler<CollectionAppliedEvent>
{
    private const string SourceModuleName = "Finance";
    private const string FactTypeName = "CollectionApplied";

    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<CollectionAppliedPostingTranslator> _logger;

    public CollectionAppliedPostingTranslator(
        IPostingEngine postingEngine, ILogger<CollectionAppliedPostingTranslator> logger)
    {
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(CollectionAppliedEvent e, CancellationToken ct)
    {
        var fact = new PostingFact(
            e.TenantId!.Value, e.CompanyId, SourceModuleName, FactTypeName, e.PaymentId, e.PaymentDate,
            0m, 0m, 0m, 0m, e.Amount);

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for Collection {PaymentId}: {Code} — {Error}",
                e.PaymentId, result.Code, result.Error);
        }
    }
}
