using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Sales.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

public sealed class SalesInvoiceCancelledPostingTranslator(
    IAccountingReversalRecorder reversals,
    ERP.Application.Modules.Inventory.Costing.InventoryCostAccounting? costAccounting = null)
    : INotificationHandler<SalesInvoiceCancelledEvent>
{
    public async Task Handle(SalesInvoiceCancelledEvent e, CancellationToken ct)
    {
        var reason = $"Factura {e.InvoiceNumber} anulada: {e.CancelReason}";
        if (costAccounting is not null)
            await costAccounting.CancelAsync(e.TenantId!.Value, e.CompanyId, e.InvoiceId, reason, ct);
        foreach (var fact in new[] { "InvoiceIssued", "CostOfGoodsSold" })
            await reversals.RecordSourceAsync(e.TenantId!.Value, e.CompanyId, "Sales", fact,
                e.InvoiceId, e.CancelReason, e.OccurredOn, ct);
    }
}
