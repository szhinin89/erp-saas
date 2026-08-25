using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ACCOUNTING-INVENTORY-COGS-07 — segundo handler de <see cref="SalesReturnAuthorizedEvent"/>
/// (junto a <see cref="SalesReturnAuthorizedPostingTranslator"/>, que solo cubre el lado ingreso/
/// IVA revertido) — reversa el costo de ventas reconocido por
/// <see cref="SalesInvoiceCogsPostingTranslator"/>: Debe Inventario / Haber Costo de Ventas (item
/// 11 del ticket), simétrico e inverso al asiento original (Debe Costo de Ventas / Haber
/// Inventario). No usa <c>JournalEntry.Reverse()</c> — mismo criterio arquitectónico ya fijado en
/// ACCOUNTING-REVERSALS-05: una devolución es un documento nuevo y distinto de la factura
/// original, nunca una anulación de ese mismo asiento (a diferencia de
/// <c>PurchaseInvoiceCancelledPostingTranslator</c>, que sí reversa porque anular una factura SÍ
/// deshace el mismo hecho).
/// </summary>
/// <remarks>
/// El costo se resuelve consultando <see cref="IStockRepository.GetMovementsByDocumentAsync"/> por
/// (SalesReturnId, "SalesReturn") — exactamente los <c>StockMovement</c> (<c>SaleReturn</c>) que
/// <c>AuthorizeSalesReturnHandler</c> ya creó en la misma transacción de la devolución — nunca se
/// recalcula desde <c>GrandTotal</c>/precio de venta. Log-and-continue.
/// </remarks>
public sealed class SalesReturnCogsReversalPostingTranslator
    : INotificationHandler<SalesReturnAuthorizedEvent>
{
    private const string SourceModuleName = "Sales";
    private const string FactTypeName = "CostOfGoodsSoldReversed";
    private const string StockDocumentType = "SalesReturn";

    private readonly IStockRepository _stockRepository;
    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<SalesReturnCogsReversalPostingTranslator> _logger;

    public SalesReturnCogsReversalPostingTranslator(
        IStockRepository stockRepository,
        IPostingEngine postingEngine,
        ILogger<SalesReturnCogsReversalPostingTranslator> logger
    )
    {
        _stockRepository = stockRepository;
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(SalesReturnAuthorizedEvent e, CancellationToken ct)
    {
        var movements = await _stockRepository.GetMovementsByDocumentAsync(
            e.TenantId!.Value,
            e.SalesReturnId,
            StockDocumentType,
            ct
        );

        var totalCost = movements.Sum(m => m.TotalCost ?? 0m);
        if (totalCost <= 0m)
        {
            _logger.LogInformation(
                "Sin costo de inventario que revertir para SalesReturn {SalesReturnId} ({ReturnNumber}) — "
                    + "devolución sin líneas de inventario o costo resuelto en cero.",
                e.SalesReturnId,
                e.ReturnNumber
            );
            return;
        }

        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.SalesReturnId,
            DateOnly.FromDateTime(e.OccurredOn),
            Subtotal: 0m,
            TotalVat: 0m,
            TotalIce: 0m,
            TotalDiscount: 0m,
            GrandTotal: 0m,
            HistoricalCostTotal: totalCost
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting de reverso de costo de ventas falló para SalesReturn {SalesReturnId} ({ReturnNumber}): {Code} — {Error}",
                e.SalesReturnId,
                e.ReturnNumber,
                result.Code,
                result.Error
            );
        }
    }
}
