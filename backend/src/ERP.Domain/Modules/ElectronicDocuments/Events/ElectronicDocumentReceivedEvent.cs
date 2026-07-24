using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>Se levanta cuando el SRI acusó recibo del comprobante (transición Sent→Received).</summary>
public sealed class ElectronicDocumentReceivedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public ElectronicDocumentState FromState { get; }
    public ElectronicDocumentState ToState { get; }

    public ElectronicDocumentReceivedEvent(
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
    string IAuditEvent.Action => "Received";
    string? IAuditEvent.Reason => null;
}
