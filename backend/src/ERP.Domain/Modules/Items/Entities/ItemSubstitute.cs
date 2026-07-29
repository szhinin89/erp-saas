using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

public sealed class ItemSubstitute : AuditableEntity
{
    public Guid ItemId { get; private set; }
    public Guid SubstituteItemId { get; private set; }
    public int Priority { get; private set; }
    public string? Note { get; private set; }
    public bool IsActive { get; private set; }

    private ItemSubstitute() { }

    public static ItemSubstitute Create(
        Guid itemId,
        Guid tenantId,
        Guid substituteItemId,
        int priority,
        string? note,
        Guid createdBy
    )
    {
        if (itemId == substituteItemId)
            throw new ArgumentException("Un ítem no puede ser sustituto de sí mismo.");

        var entity = new ItemSubstitute
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemId = itemId,
            SubstituteItemId = substituteItemId,
            Priority = priority,
            Note = note?.Trim(),
            IsActive = true,
        };
        entity.SetCreated(createdBy);
        return entity;
    }

    public void Disable(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }
}
