namespace ERP.Domain.Modules.SriCatalogs.Entities;

public class SriUom
{
    public string  Code     { get; set; } = null!;
    public string  Name     { get; set; } = null!;
    public string? Abbrev   { get; set; }
    public bool    IsActive { get; set; } = true;
}
