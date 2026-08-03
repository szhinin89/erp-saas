namespace ERP.Domain.Modules.Purchases.Enums;

/// <summary>
/// Máquina de estados de <see cref="Entities.PurchaseReturn"/> — ver diseño P0-02 §9.1.
/// <c>Draft → Authorized</c> y <c>Draft → Cancelled</c> (sin reversas) y
/// <c>Authorized → Cancelled</c> (con reversas auditadas). <c>Cancelled</c> es terminal.
/// </summary>
public enum PurchaseReturnStatus
{
    Draft = 1,
    Authorized = 2,
    Cancelled = 3,
}
