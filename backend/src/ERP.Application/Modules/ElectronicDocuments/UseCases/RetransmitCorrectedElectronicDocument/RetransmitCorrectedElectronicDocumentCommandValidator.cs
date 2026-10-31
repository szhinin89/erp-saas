using ERP.Domain.Modules.ElectronicDocuments.Entities;
using FluentValidation;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.RetransmitCorrectedElectronicDocument;

public sealed class RetransmitCorrectedElectronicDocumentCommandValidator
    : AbstractValidator<RetransmitCorrectedElectronicDocumentCommand>
{
    public RetransmitCorrectedElectronicDocumentCommandValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la corrección es obligatorio.")
            .MaximumLength(ElectronicDocument.RetransmissionReasonMaxLen);

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("La clave de idempotencia de la solicitud es obligatoria.");
    }
}
