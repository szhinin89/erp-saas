using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>Se levanta cuando falla una etapa del pipeline previa a la firma (transición Draft/Failed→Failed).</summary>
public sealed class ElectronicDocumentFailedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public ElectronicDocumentState FromState { get; }
    public ElectronicDocumentState ToState { get; }
    public string Reason { get; }

    public ElectronicDocumentFailedEvent(
        Guid tenantId, Guid electronicDocumentId, ElectronicDocumentType documentType,
        ElectronicDocumentState fromState, ElectronicDocumentState toState, string reason)
    {
        TenantId = tenantId;
        ElectronicDocumentId = electronicDocumentId;
        DocumentType = documentType;
        FromState = fromState;
        ToState = toState;
        Reason = reason;
    }

    Guid IAuditEvent.EntityId => ElectronicDocumentId;
    string IAuditEvent.Action => "Failed";
    string? IAuditEvent.Reason => Reason;
}
