using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>Solicita el espejo del asiento histórico; no interpreta reglas ni cuentas actuales.</summary>
public sealed class PurchaseCreditNoteCancelledPostingTranslator(IAccountingReversalRecorder reversals)
    : INotificationHandler<PurchaseCreditNoteCancelledEvent>
{
    public async Task Handle(PurchaseCreditNoteCancelledEvent e, CancellationToken ct)
    {
        if (e.AppliedToPayableAmount is null) return;
        await reversals.RecordSourceAsync(e.TenantId!.Value, e.CompanyId, "Purchases",
            "PurchaseCreditNoteAuthorized", e.PurchaseCreditNoteId, e.Reason, e.OccurredOn, ct);
    }
}
