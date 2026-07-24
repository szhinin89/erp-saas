namespace ERP.Domain.Geography.Entities;

public sealed class GeoParish
{
    public string Id { get; private set; } = null!;
    public string CantonId { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private GeoParish() { }

    public GeoParish(string id, string cantonId, string name)
    {
        Id = id;
        CantonId = cantonId;
        Name = name;
    }
}
