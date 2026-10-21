using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Domain.Modules.SriCatalogs.Entities;

public class SriIceRate
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal? Percentage { get; set; }
    public decimal? UnitValue { get; set; }
    /// <summary>Unidad física y moneda publicadas para la tarifa específica, sin inferir conversiones.</summary>
    public string? SpecificUnit { get; set; }
    public Guid? NormativeSourceId { get; set; }
    /// <summary>Vigencia comprobada de los componentes registrados (inclusiva); null = no comprobada o sin límite.</summary>
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }

    /// <summary>Representación de los componentes publicados; Mixed permanece no seleccionable.</summary>
    public SriTaxCalculationType CalculationType { get; set; } = SriTaxCalculationType.Percentage;
    public bool IsActive { get; set; } = true;
}
