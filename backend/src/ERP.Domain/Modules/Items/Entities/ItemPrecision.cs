namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// ZH-DESIGN-SYSTEM-PRECISION-06 — escala CONTRACTUAL de datos numéricos del ítem sin policy
/// configurable. Fija del sistema y única fuente: la usa la columna física
/// (<c>ItemChildEntitiesConfiguration</c>) y la expone <c>EffectivePrecisionPolicyDto</c> al frontend.
/// </summary>
public static class ItemPrecision
{
    /// <summary>Peso de un nivel de empaque — <c>item_packaging_levels.weight numeric(10,3)</c>.</summary>
    public const int PackagingWeight = 3;
}
