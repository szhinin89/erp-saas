namespace ERP.Domain.Modules.Inventory.Enums;

/// <summary>
/// WAREHOUSE-BRANCH-REASSIGNMENT-INTEGRITY-01 — primer motivo que impide cambiar la sucursal de una
/// bodega. <see cref="None"/> = la bodega no tiene existencias, historial ni referencias operativas.
/// </summary>
public enum WarehouseBranchChangeBlocker
{
    None = 0,
    MainWarehouse = 1,
    Stock = 2,
    Movements = 3,
    LotsSerialsOrReservations = 4,
    Documents = 5,
    DefaultSetting = 6,
    CashRegisterDefault = 7,
}
