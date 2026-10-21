namespace ERP.Domain.Modules.SriCatalogs.Entities;

public class SriCountry
{
    public string Code { get; set; } = null!; // ISO-3166 alpha-3
    public string? Iso2 { get; set; }
    /// <summary>Código de país SRI: ficha 2.34 Tabla 25 / Catálogo ATS Tabla 16.</summary>
    public string? SriCode { get; set; }
    public string Name { get; set; } = null!;
    public string? PhoneCode { get; set; }
    public bool IsActive { get; set; } = true;
}
