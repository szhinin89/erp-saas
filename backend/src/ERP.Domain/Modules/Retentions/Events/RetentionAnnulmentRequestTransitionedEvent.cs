using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Domain.Modules.Retentions.Events;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 — cada paso de una solicitud de anulación (solicitada, presentada,
/// resuelta, desistida, origen finalizado, fallo de finalización) y, desde 01B, cada cambio en lo que
/// informa la consulta al SRI (<c>SriChecked</c>). Lo consume la auditoría de entidad.
/// </summary>
public sealed class RetentionAnnulmentRequestTransitionedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid CompanyId { get; }
    public Guid RequestId { get; }
    public Guid RetentionDocumentId { get; }
    public RetentionAnnulmentStatus? FromStatus { get; }
    public RetentionAnnulmentStatus ToStatus { get; }
    public string Action { get; }
    public string? Detail { get; }

    public RetentionAnnulmentRequestTransitionedEvent(
        Guid tenantId,
        Guid companyId,
        Guid requestId,
        Guid retentionDocumentId,
        RetentionAnnulmentStatus? fromStatus,
        RetentionAnnulmentStatus toStatus,
        string action,
        string? detail
    )
    {
        TenantId = tenantId;
        CompanyId = companyId;
        RequestId = requestId;
        RetentionDocumentId = retentionDocumentId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Action = action;
        Detail = detail;
    }

    Guid IAuditEvent.EntityId => RequestId;
    string IAuditEvent.Action => Action;
    string? IAuditEvent.Reason => Detail;
}
