using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>Solicita el espejo del asiento histórico; no interpreta reglas ni cuentas actuales.</summary>
public sealed class PurchaseInvoiceCancelledPostingTranslator(IAccountingReversalRecorder reversals, ICurrentCompany company)
    : INotificationHandler<PurchaseInvoiceCancelledEvent>
{
    public async Task Handle(PurchaseInvoiceCancelledEvent e, CancellationToken ct)
    {
        await reversals.RecordSourceAsync(e.TenantId!.Value, company.CompanyId, "Purchases",
            PurchaseInvoiceConfirmedPostingTranslator.FactTypeName, e.InvoiceId, e.CancelReason, e.OccurredOn, ct);
    }
}
