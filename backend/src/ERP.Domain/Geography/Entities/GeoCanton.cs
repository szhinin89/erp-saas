namespace ERP.Domain.Geography.Entities;

public sealed class GeoCanton
{
    public string Id { get; private set; } = null!;
    public string ProvinceId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    /// <summary>Vigente en el Clasificador Geográfico Estadístico del INEC; los códigos retirados se conservan.</summary>
    public bool IsActive { get; private set; } = true;

    private GeoCanton() { }

    public GeoCanton(string id, string provinceId, string name)
    {
        Id = id;
        ProvinceId = provinceId;
        Name = name;
    }
}
