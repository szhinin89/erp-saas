using ERP.Domain.Modules.Sales.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce SalesInvoiceAuthorizedEvent (Sales) a PostingFact e invoca IPostingEngine — no crea
/// JournalEntry, no resuelve cuentas, no contiene lógica financiera (ADR-026 §8, Fase 3.2).
/// </summary>
public sealed class SalesInvoiceAuthorizedPostingTranslator
    : INotificationHandler<SalesInvoiceAuthorizedEvent>
{
    private const string SourceModuleName = "Sales";
    private const string FactTypeName = "InvoiceIssued";

    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<SalesInvoiceAuthorizedPostingTranslator> _logger;

    public SalesInvoiceAuthorizedPostingTranslator(
        IPostingEngine postingEngine,
        ILogger<SalesInvoiceAuthorizedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(SalesInvoiceAuthorizedEvent e, CancellationToken ct)
    {
        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.InvoiceId,
            e.IssueDate,
            e.Subtotal,
            e.TotalVat,
            e.TotalIce,
            e.TotalDiscount,
            e.GrandTotal
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for SalesInvoice {InvoiceId} ({InvoiceNumber}): {Code} — {Error}",
                e.InvoiceId,
                e.InvoiceNumber,
                result.Code,
                result.Error
            );
        }
    }
}
