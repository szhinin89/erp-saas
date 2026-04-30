namespace ERP.Domain.Geography.Entities;

/// <summary>Catálogo global de países (sin multi-tenant).</summary>
public sealed class GeoCountry
{
    public string Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private GeoCountry() { }

    public GeoCountry(string id, string name)
    {
        Id = id;
        Name = name;
    }
}
