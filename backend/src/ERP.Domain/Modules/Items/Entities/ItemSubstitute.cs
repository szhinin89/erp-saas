using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

public sealed class ItemSubstitute : BaseEntity
{
    public Guid ItemId { get; private set; }
    public Guid SubstituteItemId { get; private set; }
    public int Priority { get; private set; }
    public string? Note { get; private set; }
    public bool IsActive { get; private set; }

    private ItemSubstitute() { }

    public static ItemSubstitute Create(
        Guid itemId, Guid subscriberId,
        Guid substituteItemId, int priority, string? note)
    {
        if (itemId == substituteItemId)
            throw new ArgumentException("Un ítem no puede ser sustituto de sí mismo.");

        return new ItemSubstitute
        {
            Id               = Guid.NewGuid(),
            SubscriberId     = subscriberId,
            ItemId           = itemId,
            SubstituteItemId = substituteItemId,
            Priority         = priority,
            Note             = note?.Trim(),
            IsActive         = true,
        };
    }

    public void Disable() => IsActive = false;
}
