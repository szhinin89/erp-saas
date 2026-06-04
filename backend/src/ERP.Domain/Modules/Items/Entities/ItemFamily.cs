using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Nivel 1 de la jerarquía de clasificación de ítems.
/// Equivalente canónico de ProductLine.
/// Scope: subscriber (compartido entre companies).
/// </summary>
public sealed class ItemFamily : MasterEntity, ISubscriberScopedEntity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private ItemFamily() { }

    public static ItemFamily Create(Guid subscriberId, string code, string name, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código de la familia es obligatorio.", nameof(code));
        if (code.Length > 20)
            throw new ArgumentException("El código no puede superar 20 caracteres.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la familia es obligatorio.", nameof(name));
        if (name.Length > 120)
            throw new ArgumentException("El nombre no puede superar 120 caracteres.", nameof(name));

        var family = new ItemFamily
        {
            SubscriberId = subscriberId,
            Code         = code.Trim().ToUpperInvariant(),
            Name         = name.Trim(),
        };
        family.SetCreated(createdBy);
        return family;
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
