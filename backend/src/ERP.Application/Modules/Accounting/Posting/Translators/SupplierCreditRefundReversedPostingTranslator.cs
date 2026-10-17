using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Solicita el espejo exacto del asiento histórico del reembolso original
/// (Purchases/SupplierCreditRefunded del movimiento revertido, ACCOUNTING-CONTINUITY-04): mismas
/// cuentas e importes, nunca la cuenta vigente de la caja/banco. Pending/Failed sin asiento se
/// cancela, sin espejo.
/// </summary>
public sealed class SupplierCreditRefundReversedPostingTranslator(IAccountingReversalRecorder reversals)
    : INotificationHandler<SupplierCreditRefundReversedEvent>
{
    public async Task Handle(SupplierCreditRefundReversedEvent e, CancellationToken ct)
    {
        await reversals.RecordSourceAsync(e.TenantId!.Value, e.CompanyId,
            SupplierCreditRefundedPostingTranslator.SourceModuleName, SupplierCreditRefundedPostingTranslator.FactTypeName,
            e.ReversalOfMovementId, "Reversa de reembolso de saldo a favor de proveedor", e.OccurredOn, ct);
    }
}
