using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Nivel 2 de la jerarquía de clasificación de ítems.
/// Siempre pertenece a una ItemFamily.
/// </summary>
public sealed class ItemCategory : MasterEntity, ISubscriberScopedEntity
{
    public string Code     { get; private set; } = null!;
    public string Name     { get; private set; } = null!;
    public Guid   FamilyId { get; private set; }

    private ItemCategory() { }

    public static ItemCategory Create(
        Guid subscriberId, string code, string name, Guid familyId, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código de la categoría es obligatorio.", nameof(code));
        if (code.Length > 20)
            throw new ArgumentException("El código no puede superar 20 caracteres.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la categoría es obligatorio.", nameof(name));
        if (name.Length > 120)
            throw new ArgumentException("El nombre no puede superar 120 caracteres.", nameof(name));
        if (familyId == Guid.Empty)
            throw new ArgumentException("La familia es obligatoria.", nameof(familyId));

        var cat = new ItemCategory
        {
            SubscriberId = subscriberId,
            Code         = code.Trim().ToUpperInvariant(),
            Name         = name.Trim(),
            FamilyId     = familyId,
        };
        cat.SetCreated(createdBy);
        return cat;
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
