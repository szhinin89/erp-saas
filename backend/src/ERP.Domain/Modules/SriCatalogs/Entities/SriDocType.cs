namespace ERP.Domain.Modules.SriCatalogs.Entities;

public class SriDocType
{
    public string Code         { get; set; } = null!;
    public string Name         { get; set; } = null!;
    public string ShortName    { get; set; } = null!;
    public bool   IsElectronic { get; set; } = true;
    public bool   IsActive     { get; set; } = true;
}
