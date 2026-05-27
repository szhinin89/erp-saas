using FluentValidation;
using ERP.Domain.Modules.Company.Entities;

namespace ERP.Application.Modules.Company.UseCases.CreateEmissionPoint;

public sealed class CreateEmissionPointCommandValidator : AbstractValidator<CreateEmissionPointCommand>
{
    public CreateEmissionPointCommandValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de punto de emisión es obligatorio.")
            .Matches(@"^\d{1,3}$").WithMessage("El código debe ser numérico (1-3 dígitos).")
            .MaximumLength(EmissionPoint.CodeMaxLen);
        RuleFor(x => x.Name)
            .MaximumLength(EmissionPoint.NameMaxLen).When(x => x.Name != null);
    }
}
