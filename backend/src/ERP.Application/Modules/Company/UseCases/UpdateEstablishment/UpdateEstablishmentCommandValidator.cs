using FluentValidation;
using ERP.Domain.Modules.Company.Entities;

namespace ERP.Application.Modules.Company.UseCases.UpdateEstablishment;

public sealed class UpdateEstablishmentCommandValidator : AbstractValidator<UpdateEstablishmentCommand>
{
    public UpdateEstablishmentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(Establishment.NameMaxLen);
        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("La dirección fiscal es obligatoria.")
            .MaximumLength(Establishment.AddressMaxLen);
        RuleFor(x => x.Phone)
            .MaximumLength(Establishment.PhoneMaxLen).When(x => x.Phone != null);
    }
}
