using ERP.Domain.Modules.ElectronicDocuments.Entities;
using FluentValidation;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.CreateElectronicDocument;

public sealed class CreateElectronicDocumentCommandValidator
    : AbstractValidator<CreateElectronicDocumentCommand>
{
    public CreateElectronicDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentType)
            .IsInEnum()
            .WithMessage("El tipo de documento electrónico no es válido.");

        RuleFor(x => x.SourceModule)
            .NotEmpty()
            .WithMessage("El módulo de origen es obligatorio.")
            .MaximumLength(ElectronicDocument.SourceModuleMaxLen);

        RuleFor(x => x.SourceEntityId)
            .NotEqual(Guid.Empty)
            .WithMessage("El identificador del documento de origen es obligatorio.");
    }
}
