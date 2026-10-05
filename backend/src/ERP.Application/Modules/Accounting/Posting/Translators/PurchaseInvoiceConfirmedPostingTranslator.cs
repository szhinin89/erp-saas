using ERP.Application.Modules.Purchases.Exceptions;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce PurchaseInvoiceConfirmedEvent (Purchases) a PostingFact e invoca IPostingEngine — no crea
/// JournalEntry, no resuelve cuentas, no contiene lógica financiera (ADR-026 §8, Fase 3.4).
/// </summary>
/// <remarks>
/// COMPRAS-METODO-ZH-01A — el asiento InvoiceReceived es obligatorio: un fallo lanza
/// <see cref="PurchasePostingFailedException"/> para revertir Compra/Kardex/CxP juntos (mismo criterio
/// que SalesInvoiceAuthorizedPostingTranslator). Subtotal = <c>CostSubtotal</c>: base neta de
/// descuentos + flete/otros costos ya congelados por Compras, para que Debe (Subtotal+impuestos)
/// cuadre con Haber (GrandTotal) sin una segunda fórmula de costos.
/// </remarks>
public sealed class PurchaseInvoiceConfirmedPostingTranslator
    : INotificationHandler<PurchaseInvoiceConfirmedEvent>
{
    private const string SourceModuleName = "Purchases";
    private const string FactTypeName = "InvoiceReceived";

    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<PurchaseInvoiceConfirmedPostingTranslator> _logger;

    public PurchaseInvoiceConfirmedPostingTranslator(
        IPostingEngine postingEngine,
        ILogger<PurchaseInvoiceConfirmedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(PurchaseInvoiceConfirmedEvent e, CancellationToken ct)
    {
        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.InvoiceId,
            e.IssueDate,
            e.CostSubtotal,
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
                "Posting failed for PurchaseInvoice {InvoiceId} ({InvoiceNumber}): {Code} — {Error}",
                e.InvoiceId,
                e.InvoiceNumber,
                result.Code,
                result.Error
            );
            throw new PurchasePostingFailedException(
                result.Error ?? "No se pudo contabilizar la compra.",
                result.Code
            );
        }
    }
}
