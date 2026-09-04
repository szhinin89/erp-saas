using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Domain.Modules.Retentions.Events;

/// <summary>Se levanta cuando <c>RetentionDocument.Cancel()</c> anula una retención emitida.</summary>
public sealed class RetentionDocumentCancelledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid RetentionDocumentId { get; }
    public Guid CompanyId { get; }
    public RetentionSourceDocumentType SourceDocumentType { get; }
    public Guid SourceDocumentId { get; }
    public Guid SubjectBusinessPartnerId { get; }
    public string? RetentionNumber { get; }
    public decimal TotalRetained { get; }
    public string CancelReason { get; }

    public RetentionDocumentCancelledEvent(
        Guid tenantId,
        Guid retentionDocumentId,
        Guid companyId,
        RetentionSourceDocumentType sourceDocumentType,
        Guid sourceDocumentId,
        Guid subjectBusinessPartnerId,
        string? retentionNumber,
        decimal totalRetained,
        string cancelReason
    )
    {
        TenantId = tenantId;
        RetentionDocumentId = retentionDocumentId;
        CompanyId = companyId;
        SourceDocumentType = sourceDocumentType;
        SourceDocumentId = sourceDocumentId;
        SubjectBusinessPartnerId = subjectBusinessPartnerId;
        RetentionNumber = retentionNumber;
        TotalRetained = totalRetained;
        CancelReason = cancelReason;
    }

    Guid IAuditEvent.EntityId => RetentionDocumentId;
    string IAuditEvent.Action => "Cancelled";
    string? IAuditEvent.Reason => CancelReason;
}
