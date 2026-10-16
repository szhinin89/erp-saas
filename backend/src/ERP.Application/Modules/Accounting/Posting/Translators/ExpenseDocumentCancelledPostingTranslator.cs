using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Expenses.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>Solicita el espejo del asiento histórico; no interpreta reglas ni cuentas actuales.</summary>
public sealed class ExpenseDocumentCancelledPostingTranslator(IAccountingReversalRecorder reversals)
    : INotificationHandler<ExpenseDocumentCancelledEvent>
{
    public async Task Handle(ExpenseDocumentCancelledEvent e, CancellationToken ct)
    {
        await reversals.RecordSourceAsync(e.TenantId!.Value, e.CompanyId, "Expenses",
            ExpenseDocumentConfirmedPostingTranslator.FactTypeName, e.ExpenseDocumentId, e.CancelReason, e.OccurredOn, ct);
    }
}
