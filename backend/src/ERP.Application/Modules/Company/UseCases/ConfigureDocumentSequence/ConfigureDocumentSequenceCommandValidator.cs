using ERP.Domain.Modules.Company.Entities;
using FluentValidation;

namespace ERP.Application.Modules.Company.UseCases.ConfigureDocumentSequence;

public sealed class ConfigureDocumentSequenceCommandValidator
    : AbstractValidator<ConfigureDocumentSequenceCommand>
{
    public ConfigureDocumentSequenceCommandValidator()
    {
        RuleFor(x => x.EmissionPointId).NotEmpty();

        RuleFor(x => x.DocTypeCode)
            .NotEmpty()
            .WithMessage("El código de tipo documental SRI es obligatorio.")
            .MaximumLength(5);

        RuleFor(x => x.NextNumber)
            .GreaterThan(0)
            .WithMessage("El siguiente secuencial debe ser mayor que 0.")
            .LessThanOrEqualTo(DocumentSequence.MaxSequentialValue)
            .WithMessage(
                $"El siguiente secuencial no puede superar {DocumentSequence.MaxSequentialValue} (9 dígitos)."
            );
    }
}
