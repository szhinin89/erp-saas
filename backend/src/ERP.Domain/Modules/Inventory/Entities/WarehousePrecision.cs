namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// ZH-DESIGN-SYSTEM-PRECISION-06 — escala CONTRACTUAL de los datos numéricos de bodega. Fija del
/// sistema (no configurable por empresa) y única fuente: la usa la columna física
/// (<c>WarehouseConfiguration</c>) y la expone <c>EffectivePrecisionPolicyDto</c> al frontend.
/// </summary>
public static class WarehousePrecision
{
    /// <summary>Capacidad total en m³ — <c>warehouses.capacity numeric(18,4)</c>.</summary>
    public const int Capacity = 4;
}
