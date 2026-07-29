using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Domain.Modules.Ride.Events;

/// <summary>Se levanta cuando una huella (fingerprint) que ya estaba Generated se regenera explícitamente.</summary>
public sealed class RidePdfRegeneratedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid RidePdfDocumentId { get; }
    public Guid ElectronicDocumentId { get; }
    public RideDocumentType DocumentType { get; }
    public string StoragePath { get; }

    public RidePdfRegeneratedEvent(
        Guid tenantId,
        Guid ridePdfDocumentId,
        Guid electronicDocumentId,
        RideDocumentType documentType,
        string storagePath
    )
    {
        TenantId = tenantId;
        RidePdfDocumentId = ridePdfDocumentId;
        ElectronicDocumentId = electronicDocumentId;
        DocumentType = documentType;
        StoragePath = storagePath;
    }

    Guid IAuditEvent.EntityId => RidePdfDocumentId;
    string IAuditEvent.Action => "Regenerated";
    string? IAuditEvent.Reason => null;
}
