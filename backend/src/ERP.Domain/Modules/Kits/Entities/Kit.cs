using ERP.Domain.Common;
using ERP.Domain.Modules.Kits.Enums;

namespace ERP.Domain.Modules.Kits.Entities;

/// <summary>
/// Aggregate Root. Receta / kit / bundle.
/// Scope: subscriber — el catálogo de kits es compartido entre companies.
/// ItemId debe apuntar a un Item con ItemType=Kit o Bundle.
/// INVARIANTE: Un componente no puede ser el mismo kit (sin recursión directa).
/// </summary>
public sealed class Kit : MasterEntity, ISubscriberScopedEntity
{
    private readonly List<KitLine> _lines = new();

    public Guid ItemId { get; private set; }
    public KitType KitType { get; private set; }

    public IReadOnlyList<KitLine> Lines => _lines.AsReadOnly();

    private Kit() { }

    public static Kit Create(
        Guid subscriberId,
        Guid itemId,
        KitType kitType,
        Guid createdBy)
    {
        var kit = new Kit
        {
            SubscriberId = subscriberId,
            ItemId       = itemId,
            KitType      = kitType,
        };
        kit.SetCreated(createdBy);
        return kit;
    }

    public void UpdateKitType(KitType kitType, Guid updatedBy)
    {
        KitType = kitType;
        SetUpdated(updatedBy);
    }

    public KitLine AddLine(
        Guid componentItemId,
        Guid? componentVariantId,
        decimal quantity,
        string uomCode,
        Guid updatedBy,
        bool isOptional = false,
        int sortOrder = 0)
    {
        if (componentItemId == ItemId)
            throw new InvalidOperationException("Un kit no puede contener su propio ítem como componente.");

        var duplicate = _lines.Any(l =>
            l.IsActive &&
            l.ComponentItemId == componentItemId &&
            l.ComponentVariantId == componentVariantId);

        if (duplicate)
            throw new InvalidOperationException("Ya existe una línea con ese componente y variante.");

        var line = KitLine.Create(Id, SubscriberId, componentItemId, componentVariantId,
            quantity, uomCode, isOptional, sortOrder);
        _lines.Add(line);
        SetUpdated(updatedBy);
        return line;
    }

    public void UpdateLine(
        Guid lineId, decimal quantity, string uomCode,
        bool isOptional, int sortOrder, Guid updatedBy)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException("Línea de kit no encontrada.");

        line.Update(quantity, uomCode, isOptional, sortOrder);
        SetUpdated(updatedBy);
    }

    public void RemoveLine(Guid lineId, Guid updatedBy)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException("Línea de kit no encontrada.");

        line.Disable();
        SetUpdated(updatedBy);
    }
}
