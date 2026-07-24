namespace ERP.Domain.Geography.Entities;

public sealed class GeoProvince
{
    public string Id { get; private set; } = null!;
    public string CountryId { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private GeoProvince() { }

    public GeoProvince(string id, string countryId, string name)
    {
        Id = id;
        CountryId = countryId;
        Name = name;
    }
}
