using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.Services;
using MediatR;

namespace ERP.Application.Modules.Communications.UseCases.QueueEmail;

public sealed class QueueEmailCommandHandler
    : IRequestHandler<QueueEmailCommand, Result<QueuedCommunicationDto>>
{
    private readonly ICommunicationQueue _queue;

    public QueueEmailCommandHandler(ICommunicationQueue queue)
    {
        _queue = queue;
    }

    public async Task<Result<QueuedCommunicationDto>> Handle(
        QueueEmailCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await _queue.QueueEmailAsync(
            new QueueEmailRequest(
                command.Purpose,
                command.RecipientName,
                command.RecipientEmail,
                command.Subject,
                command.BodyHtml,
                command.BodyText,
                command.Priority,
                command.ScheduledAtUtc,
                command.MaxRetries,
                command.CorrelationType,
                command.CorrelationId,
                command.IdempotencyKey,
                command.Attachments
            ),
            cancellationToken
        );

        return Result<QueuedCommunicationDto>.Success(result);
    }
}
