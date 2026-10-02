using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>ADR-036 (D-7) — la solicitud de anulación no prosperó (AnnulmentPending→Authorized); el comprobante sigue vigente.</summary>
public sealed class ElectronicDocumentAnnulmentRevertedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public ElectronicDocumentState FromState { get; }
    public ElectronicDocumentState ToState { get; }
    public string Reason { get; }

    public ElectronicDocumentAnnulmentRevertedEvent(
        Guid tenantId,
        Guid electronicDocumentId,
        ElectronicDocumentType documentType,
        ElectronicDocumentState fromState,
        ElectronicDocumentState toState,
        string reason
    )
    {
        TenantId = tenantId;
        ElectronicDocumentId = electronicDocumentId;
        DocumentType = documentType;
        FromState = fromState;
        ToState = toState;
        Reason = reason;
    }

    Guid IAuditEvent.EntityId => ElectronicDocumentId;
    string IAuditEvent.Action => "AnnulmentReverted";
    string? IAuditEvent.Reason => Reason;
}
