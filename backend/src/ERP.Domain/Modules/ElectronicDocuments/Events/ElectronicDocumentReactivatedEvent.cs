using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>Se levanta cuando un documento en DeadLetter se reactiva manualmente (transición DeadLetter→estado previo).</summary>
public sealed class ElectronicDocumentReactivatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public ElectronicDocumentState FromState { get; }
    public ElectronicDocumentState ToState { get; }

    public ElectronicDocumentReactivatedEvent(
        Guid tenantId, Guid electronicDocumentId, ElectronicDocumentType documentType,
        ElectronicDocumentState fromState, ElectronicDocumentState toState)
    {
        TenantId = tenantId;
        ElectronicDocumentId = electronicDocumentId;
        DocumentType = documentType;
        FromState = fromState;
        ToState = toState;
    }

    Guid IAuditEvent.EntityId => ElectronicDocumentId;
    string IAuditEvent.Action => "Reactivated";
    string? IAuditEvent.Reason => null;
}
