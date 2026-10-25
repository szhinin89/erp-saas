using ERP.Application.Common.Services;
using ERP.Application.Modules.Companies;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ACCOUNTING-INVENTORY-COGS-07 / ACCOUNTING-CONTINUITY-05 — costo revertido por una devolución de
/// venta de una factura sin valoración A2 (sin <c>SaleCostObligation</c>): obligación agregada
/// Sales/CostOfGoodsSoldReversed con identidad = devolución. Las facturas A2 publican su costo por
/// movimiento desde el Kardex (<see cref="SaleCostRecognizedPostingTranslator"/>).
/// </summary>
public sealed class SalesReturnCogsReversalPostingTranslator(
    IStockRepository stockRepository,
    IInventoryCostLedger ledger,
    ICompanyClock companyClock,
    SaleCostPostingRecorder costs,
    ICompanyPrecisionPolicyProvider precision)
    : INotificationHandler<SalesReturnAuthorizedEvent>
{
    private const string StockDocumentType = "SalesReturn";

    public async Task Handle(SalesReturnAuthorizedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var moneyDecimals = (await precision.GetEffectiveAsync(ct)).AccountingDecimals;
        if (await ledger.GetCurrentInvoiceCostAsync(tenantId, e.CompanyId, e.SalesInvoiceId, moneyDecimals, ct) is not null)
            return;

        var movements = await stockRepository.GetMovementsByDocumentAsync(tenantId, e.SalesReturnId, StockDocumentType, ct);
        var totalCost = movements.Sum(m => m.TotalCost ?? 0m);
        if (totalCost == 0m)
            return;
        var date = await companyClock.TodayAsync(e.CompanyId, tenantId, ct);
        await costs.RecordForInvoiceAsync(tenantId, e.CompanyId, e.SalesInvoiceId, e.SalesReturnId, -totalCost, date, ct);
    }
}
