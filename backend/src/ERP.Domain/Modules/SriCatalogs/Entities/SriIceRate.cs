using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Domain.Modules.SriCatalogs.Entities;

public class SriIceRate
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal? Percentage { get; set; }
    public decimal? UnitValue { get; set; }

    /// <summary>FLOW-READY-02F.1 — tipo de cálculo de la tarifa (percent vs. monto fijo).</summary>
    public SriTaxCalculationType CalculationType { get; set; } = SriTaxCalculationType.Percentage;
    public bool IsActive { get; set; } = true;
}
