namespace ERP.Domain.Modules.Purchases.Enums;

/// <summary>
/// Tipo de costo adicional de una <see cref="Entities.PurchaseCostDistribution"/> (persistido como
/// entero en <c>purchase_cost_distributions.cost_type</c>).
/// </summary>
public enum PurchaseCostType
{
    Freight = 1,
    OtherCost = 2,
}
