namespace ERP.Domain.Geography.Entities;

public sealed class GeoParish
{
    public string Id { get; private set; } = null!;
    public string CantonId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    /// <summary>Vigente en el Clasificador Geográfico Estadístico del INEC; los códigos retirados se conservan.</summary>
    public bool IsActive { get; private set; } = true;

    private GeoParish() { }

    public GeoParish(string id, string cantonId, string name)
    {
        Id = id;
        CantonId = cantonId;
        Name = name;
    }
}
