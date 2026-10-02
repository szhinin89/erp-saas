using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>
/// ADR-036 (D-2) — el documento quedó descartado sin haber tenido nunca un intento de transmisión
/// externa (transición Draft/Failed/DeadLetter→Discarded). <see cref="Reason"/> registra por qué.
/// </summary>
public sealed class ElectronicDocumentDiscardedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public ElectronicDocumentState FromState { get; }
    public ElectronicDocumentState ToState { get; }
    public string Reason { get; }

    public ElectronicDocumentDiscardedEvent(
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
    string IAuditEvent.Action => "Discarded";
    string? IAuditEvent.Reason => Reason;
}
