using ERP.Domain.Modules.Communications.Entities;
using FluentValidation;

namespace ERP.Application.Modules.Communications.UseCases.QueueEmail;

public sealed class QueueEmailCommandValidator : AbstractValidator<QueueEmailCommand>
{
    public QueueEmailCommandValidator()
    {
        RuleFor(x => x.Purpose)
            .NotEmpty()
            .MaximumLength(CommunicationOutbox.PurposeMaxLen);

        RuleFor(x => x.RecipientName)
            .MaximumLength(CommunicationOutbox.RecipientNameMaxLen)
            .When(x => !string.IsNullOrWhiteSpace(x.RecipientName));

        RuleFor(x => x.RecipientEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(CommunicationOutbox.RecipientEmailMaxLen);

        RuleFor(x => x.Subject)
            .NotEmpty()
            .MaximumLength(CommunicationOutbox.SubjectMaxLen);

        RuleFor(x => x.BodyHtml)
            .MaximumLength(CommunicationOutbox.BodyMaxLen)
            .When(x => !string.IsNullOrWhiteSpace(x.BodyHtml));

        RuleFor(x => x.BodyText)
            .MaximumLength(CommunicationOutbox.BodyMaxLen)
            .When(x => !string.IsNullOrWhiteSpace(x.BodyText));

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.BodyHtml) || !string.IsNullOrWhiteSpace(x.BodyText))
            .WithMessage("La comunicación debe tener cuerpo HTML o texto.");

        RuleFor(x => x.MaxRetries)
            .InclusiveBetween(0, 20)
            .When(x => x.MaxRetries.HasValue);

        RuleFor(x => x.CorrelationType)
            .MaximumLength(CommunicationOutbox.CorrelationTypeMaxLen)
            .When(x => !string.IsNullOrWhiteSpace(x.CorrelationType));

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(CommunicationOutbox.IdempotencyKeyMaxLen)
            .When(x => !string.IsNullOrWhiteSpace(x.IdempotencyKey));

        RuleForEach(x => x.Attachments).ChildRules(a =>
        {
            a.RuleFor(x => x.FileName).NotEmpty().MaximumLength(CommunicationOutboxAttachment.FileNameMaxLen);
            a.RuleFor(x => x.ContentType).NotEmpty().MaximumLength(CommunicationOutboxAttachment.ContentTypeMaxLen);
            a.RuleFor(x => x.FileStoragePath)
                .MaximumLength(CommunicationOutboxAttachment.FileStoragePathMaxLen)
                .When(x => !string.IsNullOrWhiteSpace(x.FileStoragePath));
            a.RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.FileStoragePath) || (x.BinaryContent is { Length: > 0 }))
                .WithMessage("El adjunto debe tener ruta de almacenamiento o contenido binario.");
        });
    }
}
