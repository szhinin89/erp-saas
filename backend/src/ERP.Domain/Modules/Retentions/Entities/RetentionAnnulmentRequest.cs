using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Events;

namespace ERP.Domain.Modules.Retentions.Entities;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 (ADR-036 D-6…D-8) — intención durable de anular ante el SRI una
/// retención cuyo comprobante ya fue AUTORIZADO, y con ella su documento origen (Compra/Gasto).
///
/// ZH-RETENTION-SRI-ANNULMENT-01B (Ficha Técnica v2.34 §8): el SRI no ofrece un WS para SOLICITAR la
/// anulación — el usuario la presenta en SRI en Línea y registra que lo hizo —, pero SÍ uno para
/// CONSULTARLA (ConsultaComprobante). El estado fiscal (AUTORIZADO / PENDIENTE DE ANULAR / ANULADO) lo
/// informa SOLO el SRI: el ERP lo consulta (<see cref="RecordSriCheck"/>) y únicamente un ANULADO
/// consultado acepta la solicitud (<see cref="AcceptSriAnnulment"/>). El usuario nunca declara el
/// estado fiscal. Mientras esté abierta, el comprobante sigue vigente, la retención sigue <c>Issued</c>
/// y nada se revierte. Solo ANULADO habilita la finalización (anulación del origen, una vez).
///
/// No reutiliza <see cref="RetentionDocument.Status"/>: el estado de la solicitud es propio. Una
/// retención tiene a lo sumo UNA solicitud abierta (índice único parcial en BD).
/// </summary>
public sealed class RetentionAnnulmentRequest
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public const int ReasonMaxLen = 500;
    public const int ReferenceMaxLen = 200;
    public const int NotesMaxLen = 1000;
    public const int ErrorMaxLen = 2000;
    public const int RawSriStatusMaxLen = 50;
    public const int SriEvidenceMaxLen = 8000;

    public Guid CompanyId { get; private set; }
    public Guid RetentionDocumentId { get; private set; }
    public Guid ElectronicDocumentId { get; private set; }
    public RetentionSourceDocumentType SourceDocumentType { get; private set; }
    public Guid SourceDocumentId { get; private set; }

    /// <summary>Motivo de anulación del documento origen: se reutiliza al finalizar (nadie lo vuelve a escribir).</summary>
    public string Reason { get; private set; } = null!;
    public Guid RequestedBy { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }

    public RetentionAnnulmentStatus Status { get; private set; }

    // ── Datos que el usuario necesita para el trámite en SRI en Línea (snapshot al solicitar) ──
    public string AccessKey { get; private set; } = null!;
    public string RetentionNumber { get; private set; } = null!;
    public DateOnly RetentionIssueDate { get; private set; }
    public string ReceptorIdentification { get; private set; } = null!;
    public string ReceptorName { get; private set; } = null!;

    /// <summary>Plazo ORDINARIO (día 7 del mes siguiente a la emisión) — ver <see cref="RetentionAnnulmentDeadline"/>.</summary>
    public DateOnly OrdinaryDeadline { get; private set; }

    // ── Presentación al SRI ──
    public DateOnly? SubmittedOn { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public Guid? SubmittedBy { get; private set; }
    public string? SubmissionReference { get; private set; }

    // ── Resolución del SRI ──
    public DateOnly? ResolvedOn { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public Guid? ResolutionBy { get; private set; }
    public string? EvidenceReference { get; private set; }
    public string? Notes { get; private set; }

    // ── Verificación automática en el SRI (ConsultaComprobante, solo con la solicitud presentada) ──
    public DateTime? LastSriCheckAtUtc { get; private set; }
    public SriStatusQueryOutcome? LastSriQueryOutcome { get; private set; }

    /// <summary>Estado fiscal de la última consulta EXITOSA; <c>Unknown</c> si la última consulta falló.</summary>
    public SriFiscalStatus? LastSriFiscalStatus { get; private set; }

    /// <summary>Literal crudo informado por el SRI (<c>estadoAutorizacion</c> o <c>estadoConsulta</c>).</summary>
    public string? LastSriRawStatus { get; private set; }
    public int SriCheckCount { get; private set; }

    /// <summary>Evidencia técnica del ANULADO: respuesta SOAP cruda de ConsultaComprobante.</summary>
    public string? SriAnnulmentEvidence { get; private set; }

    // ── Finalización del origen (solo si ANULADO) ──
    public DateTime? FinalizedAtUtc { get; private set; }
    public int FinalizationAttempts { get; private set; }
    public string? LastFinalizationError { get; private set; }

    /// <summary>La solicitud sigue en trámite (bloquea la CxP y cualquier otra solicitud).</summary>
    public bool IsOpen =>
        Status
            is RetentionAnnulmentStatus.PendingSubmission
                or RetentionAnnulmentStatus.PendingSriResolution;

    /// <summary>La última consulta al SRI confirmó AUTORIZADO: el comprobante sigue vigente.</summary>
    public bool SriReportsAuthorized =>
        LastSriQueryOutcome == SriStatusQueryOutcome.Success
        && LastSriFiscalStatus == SriFiscalStatus.Authorized;

    /// <summary>
    /// Se puede desistir: antes de presentar, o ya presentada cuando la última consulta al SRI confirmó
    /// AUTORIZADO. Regla única para <see cref="Abandon"/> y para la UI.
    /// </summary>
    public bool CanBeAbandoned =>
        Status == RetentionAnnulmentStatus.PendingSubmission
        || (Status == RetentionAnnulmentStatus.PendingSriResolution && SriReportsAuthorized);

    /// <summary>ANULADO confirmado pero el documento origen todavía no terminó de anularse.</summary>
    public bool RequiresFinalization =>
        Status == RetentionAnnulmentStatus.Accepted && FinalizedAtUtc is null;

    private RetentionAnnulmentRequest() { }

    public static RetentionAnnulmentRequest Create(
        RetentionDocument retention,
        Guid electronicDocumentId,
        string accessKey,
        string receptorIdentification,
        string receptorName,
        string reason,
        Guid requestedBy,
        Guid? id = null
    )
    {
        ArgumentNullException.ThrowIfNull(retention);
        if (retention.Status != RetentionStatus.Issued)
            throw new DomainRuleViolationException(
                "Solo se puede solicitar la anulación ante el SRI de una retención emitida."
            );
        if (retention.RetentionNumber is null || retention.IssueDate is null)
            throw new DomainRuleViolationException(
                "La retención no tiene número o fecha de emisión."
            );
        if (electronicDocumentId == Guid.Empty)
            throw new ArgumentException(
                "El comprobante electrónico es obligatorio.",
                nameof(electronicDocumentId)
            );
        if (string.IsNullOrWhiteSpace(accessKey))
            throw new ArgumentException("La clave de acceso es obligatoria.", nameof(accessKey));
        if (requestedBy == Guid.Empty)
            throw new ArgumentException(
                "El usuario que solicita es obligatorio.",
                nameof(requestedBy)
            );

        var request = new RetentionAnnulmentRequest
        {
            Id = id is { } fixedId && fixedId != Guid.Empty ? fixedId : Guid.NewGuid(),
            TenantId = retention.TenantId,
            CompanyId = retention.CompanyId,
            RetentionDocumentId = retention.Id,
            ElectronicDocumentId = electronicDocumentId,
            SourceDocumentType = retention.SourceDocumentType,
            SourceDocumentId = retention.SourceDocumentId,
            Reason = Required(reason, ReasonMaxLen, "El motivo de anulación es obligatorio."),
            RequestedBy = requestedBy,
            RequestedAtUtc = DateTime.UtcNow,
            Status = RetentionAnnulmentStatus.PendingSubmission,
            AccessKey = accessKey.Trim(),
            RetentionNumber = retention.RetentionNumber,
            RetentionIssueDate = retention.IssueDate.Value,
            ReceptorIdentification = string.IsNullOrWhiteSpace(receptorIdentification)
                ? "—"
                : receptorIdentification.Trim(),
            ReceptorName = string.IsNullOrWhiteSpace(receptorName) ? "—" : receptorName.Trim(),
            OrdinaryDeadline = RetentionAnnulmentDeadline.Ordinary(retention.IssueDate.Value),
        };
        request.SetCreated(requestedBy);
        request.Raise(null, "Requested", request.Reason);
        return request;
    }

    /// <summary>El usuario registra que presentó la solicitud en SRI en Línea. No significa ANULADO.</summary>
    public void MarkSubmitted(
        DateOnly submittedOn,
        string? reference,
        string? notes,
        Guid submittedBy
    )
    {
        if (Status == RetentionAnnulmentStatus.PendingSriResolution)
            return;
        if (Status != RetentionAnnulmentStatus.PendingSubmission)
            throw new DomainRuleViolationException(
                $"La solicitud ya no está pendiente de presentación (estado actual: {Status})."
            );
        if (submittedOn == default)
            throw new ArgumentException(
                "La fecha de presentación es obligatoria.",
                nameof(submittedOn)
            );
        if (submittedOn < RetentionIssueDate)
            throw new DomainRuleViolationException(
                "La fecha de presentación no puede ser anterior a la emisión de la retención."
            );
        if (submittedBy == Guid.Empty)
            throw new ArgumentException("El usuario es obligatorio.", nameof(submittedBy));

        var from = Status;
        SubmittedOn = submittedOn;
        SubmittedAtUtc = DateTime.UtcNow;
        SubmittedBy = submittedBy;
        SubmissionReference = Optional(reference, ReferenceMaxLen);
        Notes = Optional(notes, NotesMaxLen) ?? Notes;
        Status = RetentionAnnulmentStatus.PendingSriResolution;
        SetUpdated(submittedBy);
        Raise(from, "SubmittedToSri", SubmissionReference);
    }

    /// <summary>
    /// Registra una consulta a ConsultaComprobante. No cambia el estado de la solicitud: solo un ANULADO
    /// la acepta, y eso lo hace <see cref="AcceptSriAnnulment"/>. Una consulta fallida (rechazada, timeout,
    /// red) nunca trae estado fiscal. Solo se audita cuando cambia lo informado (el polling es frecuente).
    /// </summary>
    public void RecordSriCheck(
        DateTime checkedAtUtc,
        SriStatusQueryOutcome outcome,
        SriFiscalStatus fiscalStatus,
        string? rawStatus,
        Guid checkedBy
    )
    {
        if (Status != RetentionAnnulmentStatus.PendingSriResolution)
            throw new DomainRuleViolationException(
                "Solo se consulta en el SRI una solicitud ya presentada y pendiente de resolución."
            );
        if (outcome != SriStatusQueryOutcome.Success && fiscalStatus != SriFiscalStatus.Unknown)
            throw new ArgumentException(
                "Una consulta al SRI que no fue exitosa no tiene estado fiscal.",
                nameof(fiscalStatus)
            );
        if (outcome == SriStatusQueryOutcome.Success && fiscalStatus == SriFiscalStatus.Unknown)
            throw new ArgumentException(
                "Una consulta exitosa trae un estado fiscal reconocido.",
                nameof(fiscalStatus)
            );

        var raw = string.IsNullOrWhiteSpace(rawStatus) ? null : rawStatus.Trim();
        if (raw is { Length: > RawSriStatusMaxLen })
            raw = raw[..RawSriStatusMaxLen];
        var changed = LastSriQueryOutcome != outcome || LastSriFiscalStatus != fiscalStatus;

        LastSriCheckAtUtc = checkedAtUtc;
        LastSriQueryOutcome = outcome;
        LastSriFiscalStatus = fiscalStatus;
        LastSriRawStatus = raw;
        SriCheckCount++;
        SetUpdated(checkedBy);
        if (changed)
            Raise(Status, "SriChecked", $"{outcome}: {raw ?? "—"}");
    }

    /// <summary>
    /// El SRI informó ANULADO (ConsultaComprobante) → solicitud aceptada con evidencia técnica. Exige que
    /// la última consulta registrada sea ese ANULADO: no existe otra vía (ni manual) para aceptar.
    /// Idempotente: devuelve <c>false</c> si ya estaba aceptada.
    /// </summary>
    public bool AcceptSriAnnulment(
        DateOnly verifiedOn,
        string evidenceReference,
        string? rawEvidence,
        Guid acceptedBy
    )
    {
        if (Status == RetentionAnnulmentStatus.Accepted)
            return false;
        if (Status != RetentionAnnulmentStatus.PendingSriResolution)
            throw new DomainRuleViolationException(
                $"La solicitud no está pendiente de resolución del SRI (estado actual: {Status})."
            );
        if (
            LastSriQueryOutcome != SriStatusQueryOutcome.Success
            || LastSriFiscalStatus != SriFiscalStatus.Annulled
        )
            throw new DomainRuleViolationException(
                "Solo el SRI confirma la anulación: se requiere una consulta a ConsultaComprobante que informe ANULADO."
            );
        if (verifiedOn == default)
            throw new ArgumentException(
                "La fecha de verificación es obligatoria.",
                nameof(verifiedOn)
            );
        var from = Status;
        EvidenceReference = Required(
            evidenceReference,
            ReferenceMaxLen,
            "La referencia de la verificación en el SRI es obligatoria."
        );
        SriAnnulmentEvidence = string.IsNullOrWhiteSpace(rawEvidence)
            ? null
            : rawEvidence.Trim()[..Math.Min(rawEvidence.Trim().Length, SriEvidenceMaxLen)];
        ResolvedOn = verifiedOn;
        ResolvedAtUtc = DateTime.UtcNow;
        ResolutionBy = acceptedBy;
        Status = RetentionAnnulmentStatus.Accepted;
        SetUpdated(acceptedBy);
        Raise(from, "ResolvedAccepted", EvidenceReference);
        return true;
    }

    /// <summary>
    /// Desistir: antes de presentar al SRI, o ya presentada cuando la ÚLTIMA consulta al SRI confirmó que
    /// el comprobante sigue AUTORIZADO (el SRI no lo anuló: p.ej. el receptor no aceptó). Con PENDIENTE DE
    /// ANULAR, ANULADO, NO AUTORIZADO o sin una consulta exitosa no se puede: el SRI todavía podría anularlo.
    /// </summary>
    public void Abandon(string reason, Guid abandonedBy)
    {
        if (Status == RetentionAnnulmentStatus.Abandoned)
            return;
        if (!CanBeAbandoned)
            throw new DomainRuleViolationException(
                Status == RetentionAnnulmentStatus.PendingSriResolution
                    ? "Solo se puede desistir de una solicitud presentada cuando el SRI confirma que el comprobante sigue AUTORIZADO."
                    : $"La solicitud ya no admite desistimiento (estado actual: {Status})."
            );
        if (abandonedBy == Guid.Empty)
            throw new ArgumentException("El usuario es obligatorio.", nameof(abandonedBy));

        var from = Status;
        Notes = Required(reason, NotesMaxLen, "El motivo del desistimiento es obligatorio.");
        ResolvedAtUtc = DateTime.UtcNow;
        ResolutionBy = abandonedBy;
        Status = RetentionAnnulmentStatus.Abandoned;
        SetUpdated(abandonedBy);
        Raise(from, "Abandoned", Notes);
    }

    /// <summary>El documento origen quedó anulado por su flujo oficial. Idempotente.</summary>
    public void MarkFinalized(Guid finalizedBy)
    {
        if (Status != RetentionAnnulmentStatus.Accepted)
            throw new DomainRuleViolationException(
                "Solo una anulación confirmada por el SRI finaliza la anulación del documento origen."
            );
        if (FinalizedAtUtc is not null)
            return;

        FinalizedAtUtc = DateTime.UtcNow;
        LastFinalizationError = null;
        FinalizationAttempts++;
        SetUpdated(finalizedBy);
        Raise(Status, "OriginFinalized", null);
    }

    /// <summary>La finalización falló: queda visible y la recuperación la reintenta.</summary>
    public void RecordFinalizationFailure(string error, Guid updatedBy)
    {
        if (!RequiresFinalization)
            return;
        FinalizationAttempts++;
        LastFinalizationError = string.IsNullOrWhiteSpace(error)
            ? "Error desconocido."
            : error.Trim()[..Math.Min(error.Trim().Length, ErrorMaxLen)];
        SetUpdated(updatedBy);
        Raise(Status, "OriginFinalizationFailed", LastFinalizationError);
    }

    private void Raise(RetentionAnnulmentStatus? from, string action, string? reason) =>
        RaiseDomainEvent(
            new RetentionAnnulmentRequestTransitionedEvent(
                TenantId,
                CompanyId,
                Id,
                RetentionDocumentId,
                from,
                Status,
                action,
                reason
            )
        );

    private static string Required(string? value, int maxLen, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(message);
        var trimmed = value.Trim();
        if (trimmed.Length > maxLen)
            throw new ArgumentException($"El texto no puede superar {maxLen} caracteres.");
        return trimmed;
    }

    private static string? Optional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLen)
            throw new ArgumentException($"El texto no puede superar {maxLen} caracteres.");
        return trimmed;
    }
}
