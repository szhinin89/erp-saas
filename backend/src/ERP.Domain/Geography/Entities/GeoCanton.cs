namespace ERP.Domain.Geography.Entities;

public sealed class GeoCanton
{
    public string Id { get; private set; } = null!;
    public string ProvinceId { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private GeoCanton() { }

    public GeoCanton(string id, string provinceId, string name)
    {
        Id = id;
        ProvinceId = provinceId;
        Name = name;
    }
}
