using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Domain.Modules.Retentions.Events;

/// <summary>Se levanta cuando <c>RetentionDocument.Issue()</c> emite la retención.</summary>
public sealed class RetentionDocumentIssuedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid RetentionDocumentId { get; }
    public Guid CompanyId { get; }
    public RetentionSourceDocumentType SourceDocumentType { get; }
    public Guid SourceDocumentId { get; }
    public Guid SubjectBusinessPartnerId { get; }
    public string RetentionNumber { get; }
    public decimal TotalRetainedVat { get; }
    public decimal TotalRetainedIncome { get; }
    public decimal TotalRetained { get; }

    public RetentionDocumentIssuedEvent(
        Guid tenantId,
        Guid retentionDocumentId,
        Guid companyId,
        RetentionSourceDocumentType sourceDocumentType,
        Guid sourceDocumentId,
        Guid subjectBusinessPartnerId,
        string retentionNumber,
        decimal totalRetainedVat,
        decimal totalRetainedIncome,
        decimal totalRetained
    )
    {
        TenantId = tenantId;
        RetentionDocumentId = retentionDocumentId;
        CompanyId = companyId;
        SourceDocumentType = sourceDocumentType;
        SourceDocumentId = sourceDocumentId;
        SubjectBusinessPartnerId = subjectBusinessPartnerId;
        RetentionNumber = retentionNumber;
        TotalRetainedVat = totalRetainedVat;
        TotalRetainedIncome = totalRetainedIncome;
        TotalRetained = totalRetained;
    }

    Guid IAuditEvent.EntityId => RetentionDocumentId;
    string IAuditEvent.Action => "Issued";
    string? IAuditEvent.Reason => null;
}
