namespace ERP.Domain.Modules.SriCatalogs.Entities;

public class SriIceRate
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal? Percentage { get; set; }
    public decimal? UnitValue { get; set; }
    public bool IsActive { get; set; } = true;
}
