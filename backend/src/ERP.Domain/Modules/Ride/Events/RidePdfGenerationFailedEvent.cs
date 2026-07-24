using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Domain.Modules.Ride.Events;

/// <summary>Se levanta cuando un intento de generación falla (parser, plantilla, render o storage).</summary>
public sealed class RidePdfGenerationFailedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid RidePdfDocumentId { get; }
    public Guid ElectronicDocumentId { get; }
    public RideDocumentType DocumentType { get; }
    public string Reason { get; }

    public RidePdfGenerationFailedEvent(
        Guid tenantId, Guid ridePdfDocumentId, Guid electronicDocumentId,
        RideDocumentType documentType, string reason)
    {
        TenantId = tenantId;
        RidePdfDocumentId = ridePdfDocumentId;
        ElectronicDocumentId = electronicDocumentId;
        DocumentType = documentType;
        Reason = reason;
    }

    Guid IAuditEvent.EntityId => RidePdfDocumentId;
    string IAuditEvent.Action => "Failed";
    string? IAuditEvent.Reason => Reason;
}
