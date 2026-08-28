namespace ERP.Domain.Modules.Payables.Enums;

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — sin <c>Draft</c> deliberadamente (SUPPLIER-PAYMENTS-AUDIT-15A:
/// "no Draft visible", confirmación directa desde el caso de uso). <c>Reversed</c> se declara ya
/// para no requerir una migración de esquema cuando el flujo de reverso se implemente en una fase
/// posterior — <see cref="Entities.SupplierPayment"/> todavía no expone un método para llegar a él.
/// </summary>
public enum SupplierPaymentStatus
{
    Confirmed = 1,
    Reversed = 2,
}
