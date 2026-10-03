using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Domain.Modules.Communications.Entities;

public sealed class CommunicationOutbox
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public const int PurposeMaxLen = 100;
    public const int RecipientNameMaxLen = 200;
    public const int RecipientEmailMaxLen = 254;
    public const int RecipientPhoneMaxLen = 40;
    public const int SubjectMaxLen = 300;
    public const int BodyMaxLen = 16000;
    public const int CorrelationTypeMaxLen = 100;
    public const int IdempotencyKeyMaxLen = 300;
    public const int LastErrorMaxLen = 2000;
    public const int DefaultMaxRetries = 3;

    private readonly List<CommunicationOutboxAttachment> _attachments = new();

    public Guid CompanyId { get; private set; }
    public Guid? BranchId { get; private set; }
    public CommunicationChannel Channel { get; private set; }
    public string Purpose { get; private set; } = null!;
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
    public string? CorrelationType { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public string? IdempotencyKey { get; private set; }

    /// <summary>Token del claim vigente (fencing): toda finalización exige este valor. Null fuera de Processing.</summary>
    public Guid? ClaimToken { get; private set; }

    /// <summary>Fin del lease del claim vigente; vencido, la fila vuelve a ser reclamable.</summary>
    public DateTime? LeaseUntilUtc { get; private set; }

    /// <summary>Categoría del último fallo (null si nunca falló o tras enviarse).</summary>
    public CommunicationFailureCategory? FailureCategory { get; private set; }

    public IReadOnlyCollection<CommunicationOutboxAttachment> Attachments => _attachments.AsReadOnly();

    private CommunicationOutbox() { }

    public static CommunicationOutbox CreateEmail(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        string purpose,
        string? recipientName,
        string recipientEmail,
        string subject,
        string? bodyHtml,
        string? bodyText,
        CommunicationPriority priority,
        DateTime? scheduledAtUtc,
        int? maxRetries,
        string? correlationType,
        Guid? correlationId,
        string? idempotencyKey,
        Guid createdBy
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId es obligatorio.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId es obligatorio.", nameof(companyId));
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("El correo destinatario es obligatorio.", nameof(recipientEmail));
        if (!recipientEmail.Contains('@', StringComparison.Ordinal))
            throw new ArgumentException("El correo destinatario no es válido.", nameof(recipientEmail));
        if (string.IsNullOrWhiteSpace(bodyHtml) && string.IsNullOrWhiteSpace(bodyText))
            throw new ArgumentException("La comunicación debe tener cuerpo HTML o texto.", nameof(bodyText));

        var message = new CommunicationOutbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId == Guid.Empty ? null : branchId,
            Channel = CommunicationChannel.Email,
            Purpose = Required(purpose, PurposeMaxLen, nameof(purpose)),
            RecipientName = Optional(recipientName, RecipientNameMaxLen, nameof(recipientName)),
            RecipientEmail = Required(recipientEmail.ToLowerInvariant(), RecipientEmailMaxLen, nameof(recipientEmail)),
            Subject = Required(subject, SubjectMaxLen, nameof(subject)),
            BodyHtml = Optional(bodyHtml, BodyMaxLen, nameof(bodyHtml)),
            BodyText = Optional(bodyText, BodyMaxLen, nameof(bodyText)),
            Status = CommunicationStatus.Pending,
            Priority = priority,
            ScheduledAtUtc = UtcDateTime.EnsureUtc(scheduledAtUtc ?? DateTime.UtcNow),
            MaxRetries = Math.Clamp(maxRetries ?? DefaultMaxRetries, 0, 20),
            CorrelationType = Optional(correlationType, CorrelationTypeMaxLen, nameof(correlationType)),
            CorrelationId = correlationId == Guid.Empty ? null : correlationId,
            IdempotencyKey = Optional(idempotencyKey, IdempotencyKeyMaxLen, nameof(idempotencyKey)),
        };
        message.SetCreated(createdBy);
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
        SetUpdated(updatedBy);
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
