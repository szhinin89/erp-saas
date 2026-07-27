using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Domain.Modules.Finance.Events;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce CollectionReversedEvent (Finance/Payment) a PostingFact e invoca IPostingEngine — no
/// crea JournalEntry, no resuelve cuentas, no contiene lógica financiera (ADR-026 §8, Fase 5.6.5).
/// No reversa automáticamente ningún JournalEntry existente: publica un nuevo hecho contable
/// ("CollectionReversed") con el monto reversado, igual que el resto de traductores — la
/// PostingRule que lo mapea (fuera de alcance de esta fase) decide el efecto contable. Mismo
/// criterio que CollectionAppliedPostingTranslator: sin desglose de impuestos
/// (Subtotal/TotalVat/TotalIce/TotalDiscount en cero, no inventados), GrandTotal transporta el
/// monto reversado. Sin PaymentDate en el evento — se usa la fecha real del reverso
/// (BaseDomainEvent.OccurredOn) como fecha del hecho contable.
/// </summary>
public sealed class CollectionReversedPostingTranslator : INotificationHandler<CollectionReversedEvent>
{
    private const string SourceModuleName = "Finance";
    private const string FactTypeName = "CollectionReversed";

    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<CollectionReversedPostingTranslator> _logger;

    public CollectionReversedPostingTranslator(
        IPostingEngine postingEngine, ILogger<CollectionReversedPostingTranslator> logger)
    {
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(CollectionReversedEvent e, CancellationToken ct)
    {
        var fact = new PostingFact(
            e.TenantId!.Value, e.CompanyId, SourceModuleName, FactTypeName, e.PaymentId,
            DateOnly.FromDateTime(e.OccurredOn), 0m, 0m, 0m, 0m, e.Amount);

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for CollectionReversed {PaymentId}: {Code} — {Error}",
                e.PaymentId, result.Code, result.Error);
        }
    }
}
