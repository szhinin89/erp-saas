using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>Product brand with optional manufacturer and country of origin.</summary>
public class Brand : MasterEntity, ISubscriberScopedEntity
{
    public const int MaxCodeLength         = 20;
    public const int MaxNameLength         = 120;
    public const int MaxManufacturerLength = 120;
    public const int MaxCountryLength      = 80;

    public string  Code             { get; private set; } = null!;
    public string  Name             { get; private set; } = null!;
    public string? Manufacturer     { get; private set; }
    public string? CountryOfOrigin  { get; private set; }

    private Brand() { }

    public static Brand Create(
        Guid subscriberId, string code, string name, Guid createdBy,
        string? manufacturer = null, string? countryOfOrigin = null)
    {
        var brand = new Brand
        {
            Id              = Guid.NewGuid(),
            SubscriberId        = subscriberId,
            Code            = code.Trim().ToUpperInvariant(),
            Name            = name.Trim(),
            Manufacturer    = manufacturer?.Trim() is { Length: > 0 } m ? m : null,
            CountryOfOrigin = countryOfOrigin?.Trim() is { Length: > 0 } c ? c : null,
        };
        brand.SetCreated(createdBy);
        return brand;
    }

    public void Update(
        string code, string name, Guid updatedBy,
        string? manufacturer = null, string? countryOfOrigin = null)
    {
        Code            = code.Trim().ToUpperInvariant();
        Name            = name.Trim();
        Manufacturer    = manufacturer?.Trim() is { Length: > 0 } m ? m : null;
        CountryOfOrigin = countryOfOrigin?.Trim() is { Length: > 0 } c ? c : null;
        SetUpdated(updatedBy);
    }
}

/// <summary>Tipo de producto (ej: mercaderÃ­a, materia prima, servicio, activo).</summary>
public class ProductType : MasterEntity, ISubscriberScopedEntity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private ProductType() { }

    public static ProductType Create(Guid subscriberId, string code, string name, Guid createdBy)
    {
        var type = new ProductType
        {
            Id       = Guid.NewGuid(),
            SubscriberId = subscriberId,
            Code     = code.ToUpperInvariant(),
            Name     = name,
        };
        type.SetCreated(createdBy);
        return type;
    }

    public void Update(string code, string name, Guid updatedBy)
    {
        Code = code.ToUpperInvariant();
        Name = name;
        SetUpdated(updatedBy);
    }
}

/// <summary>
/// Arancel / cÃ³digo arancelario del producto.
/// Usado para declaraciones de comercio exterior e impuestos especiales.
/// </summary>
public class Tariff : MasterEntity, ISubscriberScopedEntity
{
    public string Code { get; private set; } = null!;
    public string Description { get; private set; } = null!;

    private Tariff() { }

    public static Tariff Create(Guid subscriberId, string code, string description, Guid createdBy)
    {
        var tariff = new Tariff
        {
            Id          = Guid.NewGuid(),
            SubscriberId    = subscriberId,
            Code        = code,
            Description = description,
        };
        tariff.SetCreated(createdBy);
        return tariff;
    }

    public void Update(string code, string description, Guid updatedBy)
    {
        Code        = code;
        Description = description;
        SetUpdated(updatedBy);
    }
}
