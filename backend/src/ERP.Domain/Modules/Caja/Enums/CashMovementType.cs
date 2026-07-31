namespace ERP.Domain.Modules.Caja.Enums;

public enum CashMovementType
{
    Opening = 1,
    SaleIncome = 2,
    ManualIncome = 3,
    ManualExpense = 4,
    Withdrawal = 5,

    /// <summary>Reembolso en efectivo de una devolución de venta (P0-01, Fase 6).</summary>
    SaleRefund = 6,
}
