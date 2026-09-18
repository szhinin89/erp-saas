using ERP.Application.Common.Services;
using ERP.Domain.Modules.Finance.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce CollectionReversedEvent (Finance/Payment) a PostingFact e invoca IPostingEngine — no
/// crea JournalEntry, no resuelve cuentas, no contiene lógica financiera (ADR-026 §8, Fase 5.6.5).
/// No reversa automáticamente ningún JournalEntry existente: publica un nuevo hecho contable
/// ("CollectionReversed") con el monto reversado, igual que el resto de traductores — la
/// PostingRule que lo mapea (fuera de alcance de esta fase) decide el efecto contable. Mismo
/// criterio que CollectionAppliedPostingTranslator: sin desglose de impuestos
/// (Subtotal/TotalVat/TotalIce/TotalDiscount en cero, no inventados), GrandTotal transporta el
/// monto reversado. Sin PaymentDate en el evento — se usa la fecha operativa de la empresa
/// (DATETIME-COMPANY-CLOCK-GLOBAL-FIX-01: nunca BaseDomainEvent.OccurredOn crudo en UTC, que
/// desplaza el día calendario en Ecuador entre las 19:00 y 23:59) como fecha del hecho contable.
/// </summary>
public sealed class CollectionReversedPostingTranslator
    : INotificationHandler<CollectionReversedEvent>
{
    private const string SourceModuleName = "Finance";
    private const string FactTypeName = "CollectionReversed";

    private readonly IPostingEngine _postingEngine;
    private readonly ICompanyClock _companyClock;
    private readonly ILogger<CollectionReversedPostingTranslator> _logger;

    public CollectionReversedPostingTranslator(
        IPostingEngine postingEngine,
        ICompanyClock companyClock,
        ILogger<CollectionReversedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _companyClock = companyClock;
        _logger = logger;
    }

    public async Task Handle(CollectionReversedEvent e, CancellationToken ct)
    {
        var entryDate = await _companyClock.TodayAsync(e.CompanyId, e.TenantId!.Value, ct);
        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.PaymentId,
            entryDate,
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
                "Posting failed for CollectionReversed {PaymentId}: {Code} — {Error}",
                e.PaymentId,
                result.Code,
                result.Error
            );
        }
    }
}
