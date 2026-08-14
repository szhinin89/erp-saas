namespace ERP.Domain.Modules.Purchases.Enums;

/// <summary>
/// PURCHASE-FREIGHT-DISTRIBUTION-MODAL-01 — tipo de costo adicional que el modal "Distribuir
/// flete/gasto" reparte entre las líneas incluidas por el usuario, vía
/// <see cref="Entities.PurchaseInvoice.DistributeAdditionalCost"/>.
/// </summary>
public enum PurchaseCostType
{
    Freight = 1,
    OtherCost = 2,
}
