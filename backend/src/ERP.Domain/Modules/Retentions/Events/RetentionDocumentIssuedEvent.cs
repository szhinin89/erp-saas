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

    /// <summary>
    /// RETENTIONS-EXPENSES-INTEGRATION-01D-2 — fecha de emisión congelada por
    /// <c>RetentionDocument.Issue()</c>, necesaria para <c>PostingFact.EntryDate</c> del asiento de
    /// la retención (mismo dato que <c>RetentionDocument.IssueDate</c>, expuesto aquí porque el
    /// posting translator solo ve el evento, nunca el agregado).
    /// </summary>
    public DateOnly IssueDate { get; }

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
        decimal totalRetained,
        DateOnly issueDate
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
        IssueDate = issueDate;
    }

    Guid IAuditEvent.EntityId => RetentionDocumentId;
    string IAuditEvent.Action => "Issued";
    string? IAuditEvent.Reason => null;
}
