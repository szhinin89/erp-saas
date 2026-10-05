using ERP.Application.Modules.Retentions.DTOs;
using ERP.Domain.Modules.Retentions.Entities;

namespace ERP.Application.Modules.Retentions.UseCases;

/// <summary>
/// Única fuente de verdad para <see cref="RetentionDocumentDto"/> (consumida por
/// <c>CancelRetentionUseCases</c>/<c>GetRetentionBySourceUseCases</c>), mismo criterio que
/// <c>ExpenseDocumentMapper</c>.
/// </summary>
internal static class RetentionDocumentMapper
{
    public static RetentionDocumentDto ToDto(RetentionDocument document) =>
        new(
            document.Id,
            document.CompanyId,
            document.BranchId,
            document.SourceDocumentType,
            document.SourceDocumentId,
            document.SubjectBusinessPartnerId,
            document.EmissionPointId,
            document.RetentionNumber,
            document.IssueDate,
            document.Status,
            document.TotalRetainedVat,
            document.TotalRetainedIncome,
            document.TotalRetained,
            document.CancelReason,
            document.CancelledAt,
            document.CancelledBy,
            document
                .Lines.Select(l => new RetentionDocumentLineDto(
                    l.Id,
                    l.TaxType,
                    l.RetentionCode,
                    l.BaseAmount,
                    l.RetentionRate,
                    l.RetainedAmount,
                    l.Description,
                    l.RetentionCodeDescription
                ))
                .ToList(),
            document.FiscalPeriod,
            document.SourceDocumentSriTypeCode,
            document.SourceDocumentNumber,
            document.SourceDocumentIssueDate,
            document.SourceDocumentAuthorizationNumber,
            document.SourceDocumentTaxSupportCode,
            document.SourceDocumentSubtotal,
            document.SourceDocumentTotal
        );
}
