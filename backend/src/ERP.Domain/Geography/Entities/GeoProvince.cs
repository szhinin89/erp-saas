namespace ERP.Domain.Geography.Entities;

public sealed class GeoProvince
{
    public string Id { get; private set; } = null!;
    public string CountryId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    /// <summary>Vigente en el Clasificador Geográfico Estadístico del INEC; los códigos retirados se conservan.</summary>
    public bool IsActive { get; private set; } = true;

    private GeoProvince() { }

    public GeoProvince(string id, string countryId, string name)
    {
        Id = id;
        CountryId = countryId;
        Name = name;
    }
}
