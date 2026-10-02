using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Domain.Modules.Retentions.Entities;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 — auditoría de entidad (ADR-022) de <see cref="RetentionAnnulmentRequest"/>:
/// quién y cuándo solicitó, presentó al SRI, resolvió (con evidencia), desistió, y la finalización del
/// origen (incluidos los fallos). Append-only. Nunca contiene credenciales del SRI.
/// </summary>
public sealed class RetentionAnnulmentRequestAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid RetentionDocumentId { get; private set; }
    public RetentionAnnulmentStatus? FromStatus { get; private set; }
    public RetentionAnnulmentStatus ToStatus { get; private set; }

    private RetentionAnnulmentRequestAudit() { }

    public static RetentionAnnulmentRequestAudit Create(
        AuditActor actor,
        Guid companyId,
        Guid requestId,
        Guid retentionDocumentId,
        string action,
        RetentionAnnulmentStatus? fromStatus,
        RetentionAnnulmentStatus toStatus,
        string? reason
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new RetentionAnnulmentRequestAudit
        {
            CompanyId = companyId,
            RetentionDocumentId = retentionDocumentId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
        };
        audit.SetCommon(actor, requestId, action, reason);
        return audit;
    }
}
