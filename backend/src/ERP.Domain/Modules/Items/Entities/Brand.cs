using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

public class Brand : MasterEntity, ITenantScopedEntity
{
    public const int MaxCodeLength         = 20;
    public const int MaxNameLength         = 120;
    public const int MaxManufacturerLength = 120;
    public const int MaxCountryLength      = 80;

    /// <summary>
    /// Código reservado del registro "No aplica" que crea el Bootstrap de empresa (catálogo Tipo
    /// C — ver política de Bootstrap en <c>CLAUDE.md</c>). Único, protegido: no editable ni
    /// deshabilitable.
    /// </summary>
    public const string NoAplicaCode = "NO_APLICA";

    public string  Code             { get; private set; } = null!;
    public string  Name             { get; private set; } = null!;
    public string? Manufacturer     { get; private set; }
    public string? CountryOfOrigin  { get; private set; }

    private Brand() { }

    public static Brand Create(
        Guid tenantId, string code, string name, Guid createdBy,
        string? manufacturer = null, string? countryOfOrigin = null)
    {
        var brand = new Brand
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            Code            = code.Trim().ToUpperInvariant(),
            Name            = name.Trim(),
            Manufacturer    = manufacturer?.Trim() is { Length: > 0 } m ? m : null,
            CountryOfOrigin = countryOfOrigin?.Trim() is { Length: > 0 } c ? c : null,
        };
        brand.SetCreated(createdBy);
        return brand;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa: idéntica a <see cref="Create"/> pero marca el
    /// registro como sembrado por el sistema (<see cref="MasterEntity.IsSystemSeeded"/>), bloqueando
    /// <see cref="Update"/> y <see cref="Disable"/> — catálogo Tipo C, ver política de Bootstrap en
    /// <c>CLAUDE.md</c>.
    /// </summary>
    public static Brand CreateSystemSeeded(
        Guid tenantId, string code, string name, Guid createdBy,
        string? manufacturer = null, string? countryOfOrigin = null)
    {
        var brand = Create(tenantId, code, name, createdBy, manufacturer, countryOfOrigin);
        brand.MarkAsSystemSeeded();
        return brand;
    }

    public void Update(
        string code, string name, Guid updatedBy,
        string? manufacturer = null, string? countryOfOrigin = null)
    {
        this.EnsureEditable("La marca", "modificarse");

        Code            = code.Trim().ToUpperInvariant();
        Name            = name.Trim();
        Manufacturer    = manufacturer?.Trim() is { Length: > 0 } m ? m : null;
        CountryOfOrigin = countryOfOrigin?.Trim() is { Length: > 0 } c ? c : null;
        SetUpdated(updatedBy);
    }

    public override void Disable(Guid updatedBy)
    {
        this.EnsureEditable("La marca", "deshabilitarse");

        base.Disable(updatedBy);
    }
}
