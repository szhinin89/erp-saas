using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>Se levanta cuando el SRI rechazó el comprobante (transición Sent/Received→Rejected).</summary>
public sealed class ElectronicDocumentRejectedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public ElectronicDocumentState FromState { get; }
    public ElectronicDocumentState ToState { get; }
    public string Reason { get; }

    /// <summary>
    /// Mensajes reales del SRI que motivaron el rechazo (identificador/tipo/mensaje/información
    /// adicional), cuando el rechazo vino de una respuesta SRI real (recepción o autorización).
    /// Null/vacío si no hubo mensajes estructurados disponibles — <see cref="Reason"/> sigue
    /// siendo la fuente de verdad del resumen en ese caso. Parámetro opcional agregado sin
    /// romper compatibilidad con los llamadores existentes.
    /// </summary>
    public IReadOnlyList<SriMessage>? SriMessages { get; }

    public ElectronicDocumentRejectedEvent(
        Guid tenantId,
        Guid electronicDocumentId,
        ElectronicDocumentType documentType,
        ElectronicDocumentState fromState,
        ElectronicDocumentState toState,
        string reason,
        IReadOnlyList<SriMessage>? sriMessages = null
    )
    {
        TenantId = tenantId;
        ElectronicDocumentId = electronicDocumentId;
        DocumentType = documentType;
        FromState = fromState;
        ToState = toState;
        Reason = reason;
        SriMessages = sriMessages;
    }

    Guid IAuditEvent.EntityId => ElectronicDocumentId;
    string IAuditEvent.Action => "Rejected";
    string? IAuditEvent.Reason => Reason;
}
