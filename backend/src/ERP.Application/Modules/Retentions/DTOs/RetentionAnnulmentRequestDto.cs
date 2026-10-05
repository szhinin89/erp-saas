using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Application.Modules.Retentions.DTOs;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 — situación de la anulación ante el SRI de una retención autorizada,
/// con los datos exactos que el usuario necesita para el trámite en SRI en Línea (clave de acceso,
/// número, fecha de emisión, receptor, motivo) y el plazo ORDINARIO (día 7 del mes siguiente a la
/// emisión; debe validarse contra el SRI — el ERP no tiene calendario de días hábiles).
/// 01B: última verificación en ConsultaComprobante (resultado técnico, estado fiscal informado por el
/// SRI y literal crudo) y si el usuario puede desistir.
/// </summary>
public sealed record RetentionAnnulmentRequestDto(
    Guid Id,
    Guid RetentionDocumentId,
    RetentionSourceDocumentType SourceDocumentType,
    Guid SourceDocumentId,
    RetentionAnnulmentStatus Status,
    string Reason,
    Guid RequestedBy,
    DateTime RequestedAtUtc,
    string AccessKey,
    string RetentionNumber,
    DateOnly RetentionIssueDate,
    string ReceptorIdentification,
    string ReceptorName,
    DateOnly OrdinaryDeadline,
    bool IsPastOrdinaryDeadline,
    DateOnly? SubmittedOn,
    DateTime? SubmittedAtUtc,
    string? SubmissionReference,
    DateOnly? ResolvedOn,
    DateTime? ResolvedAtUtc,
    string? EvidenceReference,
    string? Notes,
    DateTime? FinalizedAtUtc,
    int FinalizationAttempts,
    string? LastFinalizationError,
    bool RequiresFinalization,
    DateTime? LastSriCheckAtUtc,
    SriStatusQueryOutcome? LastSriQueryOutcome,
    SriFiscalStatus? LastSriFiscalStatus,
    string? LastSriRawStatus,
    int SriCheckCount,
    bool CanAbandon
)
{
    public static RetentionAnnulmentRequestDto From(
        RetentionAnnulmentRequest r,
        DateOnly companyToday
    ) =>
        new(
            r.Id,
            r.RetentionDocumentId,
            r.SourceDocumentType,
            r.SourceDocumentId,
            r.Status,
            r.Reason,
            r.RequestedBy,
            r.RequestedAtUtc,
            r.AccessKey,
            r.RetentionNumber,
            r.RetentionIssueDate,
            r.ReceptorIdentification,
            r.ReceptorName,
            r.OrdinaryDeadline,
            companyToday > r.OrdinaryDeadline,
            r.SubmittedOn,
            r.SubmittedAtUtc,
            r.SubmissionReference,
            r.ResolvedOn,
            r.ResolvedAtUtc,
            r.EvidenceReference,
            r.Notes,
            r.FinalizedAtUtc,
            r.FinalizationAttempts,
            r.LastFinalizationError,
            r.RequiresFinalization,
            r.LastSriCheckAtUtc,
            r.LastSriQueryOutcome,
            r.LastSriFiscalStatus,
            r.LastSriRawStatus,
            r.SriCheckCount,
            r.CanBeAbandoned
        );
}
