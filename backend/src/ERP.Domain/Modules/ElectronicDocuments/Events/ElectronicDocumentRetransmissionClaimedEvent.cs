using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>
/// R12 Fase B — se reclamó la retransmisión corregida de un comprobante rechazado (transición
/// Rejected→Signed), persistida antes de la llamada de red al SRI. Misma clave de acceso; inicia el
/// intento de transmisión <see cref="TransmissionAttempt"/>.
/// </summary>
public sealed class ElectronicDocumentRetransmissionClaimedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public ElectronicDocumentState FromState { get; }
    public ElectronicDocumentState ToState { get; }
    public int TransmissionAttempt { get; }
    public string Reason { get; }

    public ElectronicDocumentRetransmissionClaimedEvent(
        Guid tenantId,
        Guid electronicDocumentId,
        ElectronicDocumentType documentType,
        ElectronicDocumentState fromState,
        ElectronicDocumentState toState,
        int transmissionAttempt,
        string reason
    )
    {
        TenantId = tenantId;
        ElectronicDocumentId = electronicDocumentId;
        DocumentType = documentType;
        FromState = fromState;
        ToState = toState;
        TransmissionAttempt = transmissionAttempt;
        Reason = reason;
    }

    Guid IAuditEvent.EntityId => ElectronicDocumentId;
    string IAuditEvent.Action => "RetransmissionClaimed";
    string? IAuditEvent.Reason => $"Intento {TransmissionAttempt}: {Reason}";
}
