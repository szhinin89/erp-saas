using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Solicita el espejo exacto del asiento histórico de la aplicación original
/// (Purchases/SupplierCreditApplied del movimiento revertido, ACCOUNTING-CONTINUITY-04); no
/// interpreta reglas ni cuentas actuales. Pending/Failed sin asiento se cancela, sin espejo.
/// </summary>
public sealed class SupplierCreditApplicationReversedPostingTranslator(IAccountingReversalRecorder reversals)
    : INotificationHandler<SupplierCreditApplicationReversedEvent>
{
    public async Task Handle(SupplierCreditApplicationReversedEvent e, CancellationToken ct)
    {
        await reversals.RecordSourceAsync(e.TenantId!.Value, e.CompanyId,
            SupplierCreditAppliedPostingTranslator.SourceModuleName, SupplierCreditAppliedPostingTranslator.FactTypeName,
            e.ReversalOfMovementId, "Reversa de aplicación de saldo a favor de proveedor", e.OccurredOn, ct);
    }
}
