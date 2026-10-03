using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Application.Modules.Communications.DTOs;

/// <param name="ReferenceId">
/// ZH-EDOC-COMMUNICATIONS-01 — referencia al recurso en su módulo dueño (p. ej. ElectronicDocument.Id
/// para AuthorizedXml/RidePdf): el contenido lo resuelve ese módulo al enviar, sin copiar bytes al outbox.
/// </param>
public sealed record QueueCommunicationAttachmentDto(
    CommunicationAttachmentType AttachmentType,
    string FileName,
    string ContentType,
    string? FileStoragePath = null,
    byte[]? BinaryContent = null,
    Guid? ReferenceId = null
);

/// <param name="FailureCode">
/// No null si la comunicación no pudo prepararse al encolar (template inexistente/inválido/render, o
/// destinatario ausente — ZH-EDOC-COMMUNICATIONS-01): quedó registrada como Failed (evidencia durable,
/// recuperable) y no se enviará hasta reencolarla.
/// </param>
public sealed record QueuedCommunicationDto(Guid Id, bool WasAlreadyQueued, string? FailureCode = null);

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
