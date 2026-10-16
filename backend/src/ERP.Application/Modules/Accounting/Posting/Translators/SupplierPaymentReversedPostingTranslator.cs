using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Payables.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>Solicita el espejo del asiento histórico; no interpreta reglas ni cuentas actuales.</summary>
public sealed class SupplierPaymentReversedPostingTranslator(IAccountingReversalRecorder reversals)
    : INotificationHandler<SupplierPaymentReversedEvent>
{
    public async Task Handle(SupplierPaymentReversedEvent e, CancellationToken ct)
    {
        await reversals.RecordSourceAsync(e.TenantId!.Value, e.CompanyId, "Payables",
            "SupplierPaymentConfirmed", e.SupplierPaymentId, e.ReverseReason, e.OccurredOn, ct);
    }
}
