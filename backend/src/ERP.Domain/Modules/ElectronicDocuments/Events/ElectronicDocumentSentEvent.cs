using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>Se levanta cuando el XML firmado fue enviado al servicio de recepción del SRI (transición Signed→Sent).</summary>
public sealed class ElectronicDocumentSentEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public ElectronicDocumentState FromState { get; }
    public ElectronicDocumentState ToState { get; }

    public ElectronicDocumentSentEvent(
        Guid tenantId,
        Guid electronicDocumentId,
        ElectronicDocumentType documentType,
        ElectronicDocumentState fromState,
        ElectronicDocumentState toState
    )
    {
        TenantId = tenantId;
        ElectronicDocumentId = electronicDocumentId;
        DocumentType = documentType;
        FromState = fromState;
        ToState = toState;
    }

    Guid IAuditEvent.EntityId => ElectronicDocumentId;
    string IAuditEvent.Action => "Sent";
    string? IAuditEvent.Reason => null;
}
