using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>Se levanta cuando el XML generado y validado contra XSD queda almacenado (transición Draft→XmlGenerated).</summary>
public sealed class ElectronicDocumentXmlGeneratedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public ElectronicDocumentState FromState { get; }
    public ElectronicDocumentState ToState { get; }

    public ElectronicDocumentXmlGeneratedEvent(
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
    string IAuditEvent.Action => "XmlGenerated";
    string? IAuditEvent.Reason => null;
}
