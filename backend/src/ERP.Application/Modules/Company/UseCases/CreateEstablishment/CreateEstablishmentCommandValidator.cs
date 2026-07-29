using ERP.Domain.Modules.Company.Entities;
using FluentValidation;

namespace ERP.Application.Modules.Company.UseCases.CreateEstablishment;

public sealed class CreateEstablishmentCommandValidator
    : AbstractValidator<CreateEstablishmentCommand>
{
    public CreateEstablishmentCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("El código de establecimiento es obligatorio.")
            .Matches(@"^\d{1,3}$")
            .WithMessage("El código debe ser numérico (1-3 dígitos).")
            .MaximumLength(Establishment.CodeMaxLen);
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio.")
            .MaximumLength(Establishment.NameMaxLen);
        RuleFor(x => x.Address)
            .NotEmpty()
            .WithMessage("La dirección fiscal es obligatoria.")
            .MaximumLength(Establishment.AddressMaxLen);
        RuleFor(x => x.Phone).MaximumLength(Establishment.PhoneMaxLen).When(x => x.Phone != null);
    }
}
