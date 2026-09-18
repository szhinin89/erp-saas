using ERP.Application.Common.Services;
using ERP.Domain.Modules.Sales.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce SalesReturnAuthorizedEvent (Sales, P0-01 Fase 7) a PostingFact e invoca
/// IPostingEngine — no crea JournalEntry, no resuelve cuentas, no contiene lógica financiera
/// (mismo criterio que SalesInvoiceAuthorizedPostingTranslator, ADR-026 §8). No revierte
/// automáticamente el JournalEntry de la factura original: publica un nuevo hecho contable
/// ("SalesReturn") con los montos de la devolución — la PostingRule que lo mapea (dato de
/// configuración, fuera de alcance de esta fase) decide el efecto contable (débito/crédito).
/// SalesReturn no tiene un campo de fecha propio (a diferencia de SalesInvoice.IssueDate) — se usa
/// la fecha operativa de la empresa (DATETIME-COMPANY-CLOCK-GLOBAL-FIX-01: nunca
/// BaseDomainEvent.OccurredOn crudo en UTC) como fecha del hecho contable, mismo criterio que
/// CollectionReversedPostingTranslator.
/// </summary>
public sealed class SalesReturnAuthorizedPostingTranslator
    : INotificationHandler<SalesReturnAuthorizedEvent>
{
    private const string SourceModuleName = "Sales";
    private const string FactTypeName = "SalesReturn";

    private readonly IPostingEngine _postingEngine;
    private readonly ICompanyClock _companyClock;
    private readonly ILogger<SalesReturnAuthorizedPostingTranslator> _logger;

    public SalesReturnAuthorizedPostingTranslator(
        IPostingEngine postingEngine,
        ICompanyClock companyClock,
        ILogger<SalesReturnAuthorizedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _companyClock = companyClock;
        _logger = logger;
    }

    public async Task Handle(SalesReturnAuthorizedEvent e, CancellationToken ct)
    {
        var entryDate = await _companyClock.TodayAsync(e.CompanyId, e.TenantId!.Value, ct);
        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.SalesReturnId,
            entryDate,
            e.Subtotal,
            e.TotalVat,
            e.TotalIce,
            e.TotalDiscount,
            e.GrandTotal,
            TotalIrbpnr: e.TotalIrbpnr
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for SalesReturn {SalesReturnId} ({ReturnNumber}): {Code} — {Error}",
                e.SalesReturnId,
                e.ReturnNumber,
                result.Code,
                result.Error
            );
        }
    }
}
