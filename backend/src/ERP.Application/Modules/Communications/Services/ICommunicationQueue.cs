using ERP.Application.Modules.Communications.DTOs;
using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Application.Modules.Communications.Services;

public interface ICommunicationQueue
{
    Task<QueuedCommunicationDto> QueueEmailAsync(
        QueueEmailRequest request,
        CancellationToken ct = default
    );
}

public sealed record QueueEmailRequest(
    string Purpose,
    string? RecipientName,
    string RecipientEmail,
    string Subject,
    string? BodyHtml,
    string? BodyText,
    CommunicationPriority Priority = CommunicationPriority.Normal,
    DateTime? ScheduledAtUtc = null,
    int? MaxRetries = null,
    string? CorrelationType = null,
    Guid? CorrelationId = null,
    string? IdempotencyKey = null,
    IReadOnlyCollection<QueueCommunicationAttachmentDto>? Attachments = null,
    Guid? BranchId = null,
    bool SaveImmediately = true
);
