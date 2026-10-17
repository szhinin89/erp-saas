using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Solicita el espejo exacto del asiento histórico Purchases/PurchaseReturn
/// (ACCOUNTING-CONTINUITY-04); no interpreta reglas ni cuentas actuales. Cancelar desde Draft no
/// tiene hecho contable (snapshots null); Pending/Failed sin asiento se cancela, sin espejo.
/// </summary>
public sealed class PurchaseReturnCancelledPostingTranslator(IAccountingReversalRecorder reversals)
    : INotificationHandler<PurchaseReturnCancelledEvent>
{
    public async Task Handle(PurchaseReturnCancelledEvent e, CancellationToken ct)
    {
        if (e.AppliedToPayableAmount is null)
            return;
        await reversals.RecordSourceAsync(e.TenantId!.Value, e.CompanyId, PurchaseReturnPosting.SourceModuleName,
            PurchaseReturnPosting.FactTypeName, e.PurchaseReturnId,
            e.Reason, e.OccurredOn, ct);
    }
}
