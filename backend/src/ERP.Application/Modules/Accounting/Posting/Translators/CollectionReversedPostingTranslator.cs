using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Finance.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>Solicita el espejo del asiento histórico; no interpreta reglas ni cuentas actuales.</summary>
public sealed class CollectionReversedPostingTranslator(IAccountingReversalRecorder reversals)
    : INotificationHandler<CollectionReversedEvent>
{
    public async Task Handle(CollectionReversedEvent e, CancellationToken ct)
    {
        await reversals.RecordSourceAsync(e.TenantId!.Value, e.CompanyId, "Finance",
            "CollectionApplied", e.PaymentId, e.Reason, e.OccurredOn, ct);
    }
}
