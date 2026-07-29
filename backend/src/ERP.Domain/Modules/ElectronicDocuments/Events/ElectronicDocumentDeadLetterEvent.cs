using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>
/// Se levanta cuando se agotan los reintentos de envío/consulta sin respuesta definitiva del SRI
/// (transición Sent/Received→DeadLetter). El mecanismo de reintento en sí (cuándo y cuántas
/// veces reintentar) no se implementa en esta fase — solo la transición de estado que ese
/// mecanismo futuro invocará.
/// </summary>
public sealed class ElectronicDocumentDeadLetterEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public ElectronicDocumentState FromState { get; }
    public ElectronicDocumentState ToState { get; }
    public string Reason { get; }

    public ElectronicDocumentDeadLetterEvent(
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
    string IAuditEvent.Action => "DeadLetter";
    string? IAuditEvent.Reason => Reason;
}
