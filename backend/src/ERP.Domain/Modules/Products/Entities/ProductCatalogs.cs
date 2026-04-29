using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>Marca del producto.</summary>
public class Brand : MasterEntity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private Brand() { }

    public static Brand Create(Guid tenantId, string code, string name, Guid createdBy)
    {
        var brand = new Brand
        {
            Id       = Guid.NewGuid(),
            TenantId = tenantId,
            Code     = code.ToUpperInvariant(),
            Name     = name,
        };
        brand.SetCreated(createdBy);
        return brand;
    }

    public void Update(string code, string name, Guid updatedBy)
    {
        Code = code.ToUpperInvariant();
        Name = name;
        SetUpdated(updatedBy);
    }
}

/// <summary>Tipo de producto (ej: mercadería, materia prima, servicio, activo).</summary>
public class ProductType : MasterEntity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private ProductType() { }

    public static ProductType Create(Guid tenantId, string code, string name, Guid createdBy)
    {
        var type = new ProductType
        {
            Id       = Guid.NewGuid(),
            TenantId = tenantId,
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

/// <summary>Unidad de medida (ej: UND, KG, LT, MT, CAJA).</summary>
public class UnitOfMeasure : MasterEntity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Symbol { get; private set; }

    private UnitOfMeasure() { }

    public static UnitOfMeasure Create(
        Guid tenantId, string code, string name, Guid createdBy, string? symbol = null)
    {
        var unit = new UnitOfMeasure
        {
            Id       = Guid.NewGuid(),
            TenantId = tenantId,
            Code     = code.ToUpperInvariant(),
            Name     = name,
            Symbol   = symbol,
        };
        unit.SetCreated(createdBy);
        return unit;
    }

    public void Update(string code, string name, string? symbol, Guid updatedBy)
    {
        Code   = code.ToUpperInvariant();
        Name   = name;
        Symbol = symbol;
        SetUpdated(updatedBy);
    }
}

/// <summary>
/// Arancel / código arancelario del producto.
/// Usado para declaraciones de comercio exterior e impuestos especiales.
/// </summary>
public class Tariff : MasterEntity
{
    public string Code { get; private set; } = null!;
    public string Description { get; private set; } = null!;

    private Tariff() { }

    public static Tariff Create(Guid tenantId, string code, string description, Guid createdBy)
    {
        var tariff = new Tariff
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
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
