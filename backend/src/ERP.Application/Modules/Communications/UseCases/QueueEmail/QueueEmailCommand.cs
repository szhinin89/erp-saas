using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Domain.Modules.Communications.Enums;
using MediatR;

namespace ERP.Application.Modules.Communications.UseCases.QueueEmail;

public sealed record QueueEmailCommand(
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
    IReadOnlyCollection<QueueCommunicationAttachmentDto>? Attachments = null
) : IRequest<Result<QueuedCommunicationDto>>, ICompanyScopedRequest;
