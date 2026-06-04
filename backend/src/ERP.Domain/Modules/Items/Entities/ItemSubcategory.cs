using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Nivel 3 (opcional) de la jerarquía de clasificación de ítems.
/// Siempre pertenece a una ItemCategory.
/// </summary>
public sealed class ItemSubcategory : MasterEntity, ISubscriberScopedEntity
{
    public string Code       { get; private set; } = null!;
    public string Name       { get; private set; } = null!;
    public Guid   CategoryId { get; private set; }

    private ItemSubcategory() { }

    public static ItemSubcategory Create(
        Guid subscriberId, string code, string name, Guid categoryId, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código de la subcategoría es obligatorio.", nameof(code));
        if (code.Length > 20)
            throw new ArgumentException("El código no puede superar 20 caracteres.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la subcategoría es obligatorio.", nameof(name));
        if (name.Length > 120)
            throw new ArgumentException("El nombre no puede superar 120 caracteres.", nameof(name));
        if (categoryId == Guid.Empty)
            throw new ArgumentException("La categoría es obligatoria.", nameof(categoryId));

        var sub = new ItemSubcategory
        {
            SubscriberId = subscriberId,
            Code         = code.Trim().ToUpperInvariant(),
            Name         = name.Trim(),
            CategoryId   = categoryId,
        };
        sub.SetCreated(createdBy);
        return sub;
    }

    public void Update(string code, string name, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        SetUpdated(updatedBy);
    }
}
