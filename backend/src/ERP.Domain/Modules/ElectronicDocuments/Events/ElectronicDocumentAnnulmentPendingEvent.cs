using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>ADR-036 (D-7) — se registró una solicitud de anulación ante el SRI (Authorized→AnnulmentPending).</summary>
public sealed class ElectronicDocumentAnnulmentPendingEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public ElectronicDocumentState FromState { get; }
    public ElectronicDocumentState ToState { get; }
    public Guid AnnulmentRequestId { get; }

    public ElectronicDocumentAnnulmentPendingEvent(
        Guid tenantId,
        Guid electronicDocumentId,
        ElectronicDocumentType documentType,
        ElectronicDocumentState fromState,
        ElectronicDocumentState toState,
        Guid annulmentRequestId
    )
    {
        TenantId = tenantId;
        ElectronicDocumentId = electronicDocumentId;
        DocumentType = documentType;
        FromState = fromState;
        ToState = toState;
        AnnulmentRequestId = annulmentRequestId;
    }

    Guid IAuditEvent.EntityId => ElectronicDocumentId;
    string IAuditEvent.Action => "AnnulmentPending";
    string? IAuditEvent.Reason => $"Solicitud de anulación {AnnulmentRequestId}";
}
