using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Application.Modules.Communications.DTOs;

public sealed record QueueCommunicationAttachmentDto(
    CommunicationAttachmentType AttachmentType,
    string FileName,
    string ContentType,
    string? FileStoragePath = null,
    byte[]? BinaryContent = null
);

public sealed record QueuedCommunicationDto(Guid Id, bool WasAlreadyQueued);

public sealed record CommunicationOutboxItemDto(
    Guid Id,
    string Purpose,
    CommunicationChannel Channel,
    CommunicationStatus Status,
    CommunicationPriority Priority,
    string? RecipientName,
    string? RecipientEmail,
    string Subject,
    DateTime ScheduledAtUtc,
    DateTime? SentAtUtc,
    DateTime? FailedAtUtc,
    int RetryCount,
    int MaxRetries,
    string? LastError,
    string? CorrelationType,
    Guid? CorrelationId,
    string? IdempotencyKey
);
