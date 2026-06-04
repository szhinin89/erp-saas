using ERP.Domain.Common;

namespace ERP.Domain.Modules.DigitalItems.Entities;

public sealed class DigitalDeliverable : BaseEntity
{
    public Guid DigitalItemId { get; private set; }
    public string Name { get; private set; } = null!;
    public Guid StorageObjectId { get; private set; }
    public string? Version { get; private set; }
    public bool IsActive { get; private set; }

    private DigitalDeliverable() { }

    internal static DigitalDeliverable Create(
        Guid digitalItemId, Guid subscriberId,
        string name, Guid storageObjectId, string? version)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del descargable es obligatorio.", nameof(name));

        return new DigitalDeliverable
        {
            Id             = Guid.NewGuid(),
            SubscriberId   = subscriberId,
            DigitalItemId  = digitalItemId,
            Name           = name.Trim(),
            StorageObjectId = storageObjectId,
            Version        = version?.Trim(),
            IsActive       = true,
        };
    }

    public void Update(string name, Guid storageObjectId, string? version)
    {
        Name            = name.Trim();
        StorageObjectId = storageObjectId;
        Version         = version?.Trim();
    }

    public void Disable() => IsActive = false;
}
