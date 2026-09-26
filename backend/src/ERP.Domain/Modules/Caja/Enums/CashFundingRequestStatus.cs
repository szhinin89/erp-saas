namespace ERP.Domain.Modules.Caja.Enums;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B — estado de una <see cref="Entities.CashFundingRequest"/>.
/// <see cref="Pending"/> es el único estado mutable; los demás son terminales y nunca se reutilizan.
/// <see cref="Fulfilled"/> = el cajero que controla la sesión entregó el efectivo y, en la MISMA
/// transacción, se ejecutó el SupplierPayment que lo consume (nunca una autorización previa).
/// </summary>
public enum CashFundingRequestStatus
{
    Pending = 1,
    Fulfilled = 2,
    Rejected = 3,
    Cancelled = 4,
}
