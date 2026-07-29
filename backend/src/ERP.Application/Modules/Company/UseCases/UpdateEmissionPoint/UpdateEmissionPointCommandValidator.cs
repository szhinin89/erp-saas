using ERP.Domain.Modules.Company.Entities;
using FluentValidation;

namespace ERP.Application.Modules.Company.UseCases.UpdateEmissionPoint;

public sealed class UpdateEmissionPointCommandValidator
    : AbstractValidator<UpdateEmissionPointCommand>
{
    public UpdateEmissionPointCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(EmissionPoint.NameMaxLen).When(x => x.Name != null);
        RuleFor(x => x.EmissionType).IsInEnum().WithMessage("El tipo de emisión es obligatorio.");
    }
}
