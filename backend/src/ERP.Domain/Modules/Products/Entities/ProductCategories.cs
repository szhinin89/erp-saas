using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>Línea de producto — nivel 1 de categorización.</summary>
public class ProductLine : MasterEntity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private ProductLine() { }

    public static ProductLine Create(Guid subscriberId, string code, string name, Guid createdBy)
    {
        var line = new ProductLine
        {
            Id       = Guid.NewGuid(),
            SubscriberId = subscriberId,
            Code     = code.ToUpperInvariant(),
            Name     = name,
        };
        line.SetCreated(createdBy);
        return line;
    }

    public void Update(string code, string name, Guid updatedBy)
    {
        Code = code.ToUpperInvariant();
        Name = name;
        SetUpdated(updatedBy);
    }
}

/// <summary>Categoría de producto — nivel 2 de categorización.</summary>
public class ProductCategory : MasterEntity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public Guid LineId { get; private set; }

    private ProductCategory() { }

    public static ProductCategory Create(
        Guid subscriberId, string code, string name, Guid lineId, Guid createdBy)
    {
        var category = new ProductCategory
        {
            Id       = Guid.NewGuid(),
            SubscriberId = subscriberId,
            Code     = code.ToUpperInvariant(),
            Name     = name,
            LineId   = lineId,
        };
        category.SetCreated(createdBy);
        return category;
    }

    public void Update(string code, string name, Guid lineId, Guid updatedBy)
    {
        Code   = code.ToUpperInvariant();
        Name   = name;
        LineId = lineId;
        SetUpdated(updatedBy);
    }
}

/// <summary>Subcategoría de producto — nivel 3 de categorización.</summary>
public class ProductSubcategory : MasterEntity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public Guid CategoryId { get; private set; }

    private ProductSubcategory() { }

    public static ProductSubcategory Create(
        Guid subscriberId, string code, string name, Guid categoryId, Guid createdBy)
    {
        var sub = new ProductSubcategory
        {
            Id         = Guid.NewGuid(),
            SubscriberId   = subscriberId,
            Code       = code.ToUpperInvariant(),
            Name       = name,
            CategoryId = categoryId,
        };
        sub.SetCreated(createdBy);
        return sub;
    }

    public void Update(string code, string name, Guid categoryId, Guid updatedBy)
    {
        Code       = code.ToUpperInvariant();
        Name       = name;
        CategoryId = categoryId;
        SetUpdated(updatedBy);
    }
}
