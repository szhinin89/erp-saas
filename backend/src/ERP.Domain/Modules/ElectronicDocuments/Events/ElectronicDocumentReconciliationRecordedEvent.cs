using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>
/// Caso #32 — un usuario autorizado registró evidencia de conciliación manual de un comprobante con
/// resultado SRI desconocido. Sin transición: <see cref="State"/> es el estado en que se registró.
/// </summary>
public sealed class ElectronicDocumentReconciliationRecordedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public ElectronicDocumentState State { get; }
    public string Evidence { get; }

    public ElectronicDocumentReconciliationRecordedEvent(
        Guid tenantId,
        Guid electronicDocumentId,
        ElectronicDocumentType documentType,
        ElectronicDocumentState state,
        string evidence
    )
    {
        TenantId = tenantId;
        ElectronicDocumentId = electronicDocumentId;
        DocumentType = documentType;
        State = state;
        Evidence = evidence;
    }

    Guid IAuditEvent.EntityId => ElectronicDocumentId;
    string IAuditEvent.Action => "ReconciliationRecorded";
    string? IAuditEvent.Reason => Evidence;
}
