using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Retentions.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>Solicita el espejo del asiento histórico; no interpreta reglas ni cuentas actuales.</summary>
public sealed class RetentionDocumentCancelledPostingTranslator(IAccountingReversalRecorder reversals)
    : INotificationHandler<RetentionDocumentCancelledEvent>
{
    public async Task Handle(RetentionDocumentCancelledEvent e, CancellationToken ct)
    {
        await reversals.RecordSourceAsync(e.TenantId!.Value, e.CompanyId, "Retentions",
            RetentionDocumentIssuedPostingTranslator.FactTypeName, e.RetentionDocumentId, e.CancelReason, e.OccurredOn, ct);
    }
}
