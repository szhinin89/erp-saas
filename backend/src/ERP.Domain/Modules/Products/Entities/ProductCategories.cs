using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>LÃ­nea de producto â€” nivel 1 de categorizaciÃ³n.</summary>
public class ProductLine : MasterEntity, ISubscriberScopedEntity
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

/// <summary>CategorÃ­a de producto â€” nivel 2 de categorizaciÃ³n.</summary>
public class ProductCategory : MasterEntity, ISubscriberScopedEntity
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

/// <summary>SubcategorÃ­a de producto â€” nivel 3 de categorizaciÃ³n.</summary>
public class ProductSubcategory : MasterEntity, ISubscriberScopedEntity
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
