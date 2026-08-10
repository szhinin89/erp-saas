using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Domain.Modules.SriCatalogs.Entities;

/// <summary>
/// FLOW-READY-02F.1 — catálogo global del Impuesto Redimible a las Botellas Plásticas No
/// Retornables (IRBPNR, SRI <c>impuesto/codigo = "5"</c>). Mismo shape que <see cref="SriIceRate"/> —
/// entidad separada (nunca se reutiliza <c>SriIceRate</c> para IRBPNR: son impuestos SRI distintos
/// con su propio código, catálogo y resolución).
/// </summary>
public class SriIrbpnrRate
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal? Percentage { get; set; }
    public decimal? UnitValue { get; set; }
    public SriTaxCalculationType CalculationType { get; set; } = SriTaxCalculationType.Specific;
    public bool IsActive { get; set; } = true;
}
