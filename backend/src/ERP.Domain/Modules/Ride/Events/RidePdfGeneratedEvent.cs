using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Domain.Modules.Ride.Events;

/// <summary>Se levanta la primera vez que una huella (fingerprint) de RIDE se genera con éxito.</summary>
public sealed class RidePdfGeneratedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid RidePdfDocumentId { get; }
    public Guid ElectronicDocumentId { get; }
    public RideDocumentType DocumentType { get; }
    public string StoragePath { get; }

    public RidePdfGeneratedEvent(
        Guid tenantId, Guid ridePdfDocumentId, Guid electronicDocumentId,
        RideDocumentType documentType, string storagePath)
    {
        TenantId = tenantId;
        RidePdfDocumentId = ridePdfDocumentId;
        ElectronicDocumentId = electronicDocumentId;
        DocumentType = documentType;
        StoragePath = storagePath;
    }

    Guid IAuditEvent.EntityId => RidePdfDocumentId;
    string IAuditEvent.Action => "Generated";
    string? IAuditEvent.Reason => null;
}
