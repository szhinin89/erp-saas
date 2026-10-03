using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;

namespace ERP.Domain.Modules.Communications.Entities;

/// <summary>
/// SSOT durable de la entrega de una comunicación (ADR-039 D4): un registro = un canal × un rol de
/// destinatario. ZH-COMMUNICATIONS-CONTRACT-01: alcance explícito (<see cref="ScopeKind"/>; System =
/// tenant/empresa null, nunca un centinela), origen (<see cref="SourceModule"/>/<see cref="SourceType"/>/
/// <see cref="SourceId"/>), rol del destinatario e identidad única (<see cref="CommunicationIdentity"/>).
/// Hereda de <see cref="SystemAggregateRoot"/> (no de <c>BaseEntity</c>) porque el tenant es opcional.
/// </summary>
public sealed class CommunicationOutbox : SystemAggregateRoot, IOptionalCompanyScopeEntity
{
    public const int PurposeMaxLen = 100;
    public const int RecipientNameMaxLen = 200;
    public const int RecipientEmailMaxLen = 254;
    public const int RecipientPhoneMaxLen = 40;
    public const int SubjectMaxLen = 300;
    public const int BodyMaxLen = 16000;
    public const int IdempotencyKeyMaxLen = 300;
    public const int LastErrorMaxLen = 2000;
    public const int DefaultMaxRetries = 3;

    private readonly List<CommunicationOutboxAttachment> _attachments = new();

    public CommunicationScopeKind ScopeKind { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public Guid? BranchId { get; private set; }
    public CommunicationChannel Channel { get; private set; }
    public string Purpose { get; private set; } = null!;

    // Origen: null solo en filas anteriores al contrato que no pudieron atribuirse con certeza.
    public string? SourceModule { get; private set; }
    public string? SourceType { get; private set; }
    public Guid? SourceId { get; private set; }
    public CommunicationRecipientRole? RecipientRole { get; private set; }

    public string? RecipientName { get; private set; }
    public string? RecipientEmail { get; private set; }
    public string? RecipientPhone { get; private set; }
    public string Subject { get; private set; } = null!;
    public string? BodyHtml { get; private set; }
    public string? BodyText { get; private set; }
    public CommunicationStatus Status { get; private set; }
    public CommunicationPriority Priority { get; private set; }
    public DateTime ScheduledAtUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime? ProcessingStartedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>Clave de <see cref="CommunicationIdentity"/>; única por (tenant, empresa) con NULLS NOT DISTINCT.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Reenvío manual: comunicación original y número de reenvío (0 = original).</summary>
    public Guid? ResendOfCommunicationId { get; private set; }
    public int ResendSequence { get; private set; }

    /// <summary>Token del claim vigente (fencing): toda finalización exige este valor. Null fuera de Processing.</summary>
    public Guid? ClaimToken { get; private set; }

    /// <summary>Fin del lease del claim vigente; vencido, la fila vuelve a ser reclamable.</summary>
    public DateTime? LeaseUntilUtc { get; private set; }

    /// <summary>Categoría del último fallo (null si nunca falló o tras enviarse).</summary>
    public CommunicationFailureCategory? FailureCategory { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public IReadOnlyCollection<CommunicationOutboxAttachment> Attachments => _attachments.AsReadOnly();

    public CommunicationScope Scope => CommunicationScope.From(ScopeKind, TenantId, CompanyId, BranchId);

    private CommunicationOutbox() { }

    /// <summary>
    /// Crea la comunicación a partir de su identidad (alcance, propósito, canal, origen, rol).
    /// Para un reenvío manual, <paramref name="resendOfCommunicationId"/> es obligatorio y la identidad
    /// debe venir de <see cref="CommunicationIdentity.ForResend"/>.
    /// </summary>
    public static CommunicationOutbox CreateEmail(
        CommunicationIdentity identity,
        string? recipientName,
        string recipientEmail,
        string subject,
        string? bodyHtml,
        string? bodyText,
        CommunicationPriority priority,
        DateTime? scheduledAtUtc,
        int maxRetries,
        Guid createdBy,
        Guid? resendOfCommunicationId = null
    )
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (identity.Channel != CommunicationChannel.Email)
            throw new ArgumentException("CreateEmail requiere una identidad del canal Email.", nameof(identity));
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("El correo destinatario es obligatorio.", nameof(recipientEmail));
        if (!recipientEmail.Contains('@', StringComparison.Ordinal))
            throw new ArgumentException("El correo destinatario no es válido.", nameof(recipientEmail));
        if (string.IsNullOrWhiteSpace(bodyHtml) && string.IsNullOrWhiteSpace(bodyText))
            throw new ArgumentException("La comunicación debe tener cuerpo HTML o texto.", nameof(bodyText));
        if ((identity.ResendSequence > 0) != (resendOfCommunicationId is { } original && original != Guid.Empty))
            throw new ArgumentException(
                "Un reenvío manual requiere la comunicación original y una identidad de reenvío (y viceversa).",
                nameof(resendOfCommunicationId)
            );

        var scope = identity.Scope;
        var message = new CommunicationOutbox
        {
            Id = Guid.NewGuid(),
            ScopeKind = scope.Kind,
            TenantId = scope.TenantId,
            CompanyId = scope.CompanyId,
            BranchId = scope.BranchId,
            Channel = identity.Channel,
            Purpose = Required(identity.Purpose, PurposeMaxLen, nameof(identity)),
            SourceModule = identity.Source.Module,
            SourceType = identity.Source.Type,
            SourceId = identity.Source.Id,
            RecipientRole = identity.RecipientRole,
            RecipientName = Optional(recipientName, RecipientNameMaxLen, nameof(recipientName)),
            RecipientEmail = Required(recipientEmail.ToLowerInvariant(), RecipientEmailMaxLen, nameof(recipientEmail)),
            Subject = Required(subject, SubjectMaxLen, nameof(subject)),
            BodyHtml = Optional(bodyHtml, BodyMaxLen, nameof(bodyHtml)),
            BodyText = Optional(bodyText, BodyMaxLen, nameof(bodyText)),
            Status = CommunicationStatus.Pending,
            Priority = priority,
            ScheduledAtUtc = UtcDateTime.EnsureUtc(scheduledAtUtc ?? DateTime.UtcNow),
            MaxRetries = Math.Clamp(maxRetries, 0, 20),
            IdempotencyKey = identity.Key,
            ResendOfCommunicationId = resendOfCommunicationId,
            ResendSequence = identity.ResendSequence,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
        return message;
    }

    public void AddAttachment(
        CommunicationAttachmentType attachmentType,
        string fileName,
        string contentType,
        string? fileStoragePath,
        byte[]? binaryContent,
        Guid createdBy
    ) => _attachments.Add(CommunicationOutboxAttachment.Create(TenantId, CompanyId, Id, attachmentType, fileName, contentType, fileStoragePath, binaryContent, createdBy));

    // ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — las transiciones de entrega (claim → Sent/Failed/
    // reprogramada) ya no se hacen en memoria + SaveChanges: son UPDATE condicionados en PostgreSQL
    // (CommunicationOutboxDeliveryStore), con ClaimToken como fencing. La regla de reintento/backoff
    // vive en CommunicationRetryPolicy.

    public void Cancel(Guid updatedBy)
    {
        if (Status == CommunicationStatus.Sent)
            throw new DomainRuleViolationException("Una comunicación enviada no puede cancelarse.");

        Status = CommunicationStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    private static string Required(string value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El valor es obligatorio.", paramName);
        return Trim(value, maxLength, paramName);
    }

    private static string? Optional(string? value, int maxLength, string paramName) =>
        string.IsNullOrWhiteSpace(value) ? null : Trim(value, maxLength, paramName);

    private static string Trim(string value, int maxLength, string paramName)
    {
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"El valor no puede superar {maxLength} caracteres.", paramName);
        return normalized;
    }
}
