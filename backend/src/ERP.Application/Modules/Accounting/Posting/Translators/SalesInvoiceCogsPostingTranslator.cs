using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ACCOUNTING-INVENTORY-COGS-07 / ACCOUNTING-CONTINUITY-05 — segundo handler de
/// <see cref="SalesInvoiceAuthorizedEvent"/>: registra la obligación durable del costo de ventas
/// (Sales/CostOfGoodsSold, identidad = factura), hecho distinto de InvoiceIssued. El costo sale de los
/// <c>StockMovement</c> que la autorización creó en la misma transacción (Kardex), nunca del precio.
/// No invoca el motor: el intento corre después del commit (AccountingPostingExecutor).
/// </summary>
public sealed class SalesInvoiceCogsPostingTranslator(IStockRepository stockRepository, SaleCostPostingRecorder costs)
    : INotificationHandler<SalesInvoiceAuthorizedEvent>
{
    private const string StockDocumentType = "SalesInvoice";

    public async Task Handle(SalesInvoiceAuthorizedEvent e, CancellationToken ct)
    {
        var movements = await stockRepository.GetMovementsByDocumentAsync(
            e.TenantId!.Value, e.InvoiceId, StockDocumentType, ct);
        var totalCost = movements.Sum(m => m.TotalCost ?? 0m);
        await costs.RecordAsync(e.TenantId.Value, e.CompanyId, e.InvoiceId, e.InvoiceNumber, e.IssueDate,
            e.InvoiceId, totalCost, e.IssueDate, ct);
    }
}
