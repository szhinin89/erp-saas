using ERP.Domain.Common;
using ERP.Domain.Modules.DigitalItems.Enums;

namespace ERP.Domain.Modules.DigitalItems.Entities;

/// <summary>
/// Aggregate Root. Configuración de entrega digital de un ítem.
/// Scope: subscriber.
/// ItemId debe apuntar a un Item con ItemType=Digital.
/// </summary>
public sealed class DigitalItem : MasterEntity, ISubscriberScopedEntity
{
    private readonly List<DigitalDeliverable> _deliverables = new();

    public Guid ItemId { get; private set; }
    public LicenseType LicenseType { get; private set; }
    public DeliveryMethod DeliveryMethod { get; private set; }
    public int? MaxDownloads { get; private set; }
    public int? ValidityDays { get; private set; }

    public IReadOnlyList<DigitalDeliverable> Deliverables => _deliverables.AsReadOnly();

    private DigitalItem() { }

    public static DigitalItem Create(
        Guid subscriberId,
        Guid itemId,
        LicenseType licenseType,
        DeliveryMethod deliveryMethod,
        Guid createdBy,
        int? maxDownloads = null,
        int? validityDays = null)
    {
        if (maxDownloads is <= 0)
            throw new ArgumentException("El límite de descargas debe ser mayor que cero.", nameof(maxDownloads));
        if (validityDays is <= 0)
            throw new ArgumentException("Los días de validez deben ser mayores que cero.", nameof(validityDays));

        var di = new DigitalItem
        {
            SubscriberId   = subscriberId,
            ItemId         = itemId,
            LicenseType    = licenseType,
            DeliveryMethod = deliveryMethod,
            MaxDownloads   = maxDownloads,
            ValidityDays   = validityDays,
        };
        di.SetCreated(createdBy);
        return di;
    }

    public void Update(
        LicenseType licenseType, DeliveryMethod deliveryMethod,
        int? maxDownloads, int? validityDays, Guid updatedBy)
    {
        LicenseType    = licenseType;
        DeliveryMethod = deliveryMethod;
        MaxDownloads   = maxDownloads;
        ValidityDays   = validityDays;
        SetUpdated(updatedBy);
    }

    public DigitalDeliverable AddDeliverable(
        string name, Guid storageObjectId, Guid updatedBy, string? version = null)
    {
        var deliverable = DigitalDeliverable.Create(Id, SubscriberId, name, storageObjectId, version);
        _deliverables.Add(deliverable);
        SetUpdated(updatedBy);
        return deliverable;
    }

    public void UpdateDeliverable(
        Guid deliverableId, string name, Guid storageObjectId, string? version, Guid updatedBy)
    {
        var d = _deliverables.FirstOrDefault(x => x.Id == deliverableId)
            ?? throw new InvalidOperationException("Descargable no encontrado.");
        d.Update(name, storageObjectId, version);
        SetUpdated(updatedBy);
    }

    public void RemoveDeliverable(Guid deliverableId, Guid updatedBy)
    {
        var d = _deliverables.FirstOrDefault(x => x.Id == deliverableId)
            ?? throw new InvalidOperationException("Descargable no encontrado.");
        d.Disable();
        SetUpdated(updatedBy);
    }
}
