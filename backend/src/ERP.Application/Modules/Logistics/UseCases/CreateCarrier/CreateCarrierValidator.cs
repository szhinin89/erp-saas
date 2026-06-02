using ERP.Domain.Modules.Logistics.Entities;
using FluentValidation;

namespace ERP.Application.Modules.Logistics.UseCases.CreateCarrier;

public class CreateCarrierValidator : AbstractValidator<CreateCarrierCommand>
{
    // Transportistas: RUC (04) o cédula (05). Pasaporte (06) admitido para extranjeros.
    private static readonly string[] AllowedIdTypes = ["04", "05", "06"];

    public CreateCarrierValidator()
    {
        RuleFor(x => x.IdentificationType)
            .NotEmpty()
            .Must(t => AllowedIdTypes.Contains(t))
            .WithMessage("El tipo de identificación debe ser 04 (RUC), 05 (Cédula) o 06 (Pasaporte).");

        RuleFor(x => x.IdentificationNumber)
            .NotEmpty()
            .MaximumLength(Carrier.MaxIdentificationNumberLength);

        RuleFor(x => x.LegalName)
            .NotEmpty()
            .MaximumLength(Carrier.MaxLegalNameLength);

        RuleFor(x => x.LicensePlate)
            .NotEmpty()
            .MaximumLength(Carrier.MaxLicensePlateLength);

        RuleFor(x => x.Phone)
            .MaximumLength(Carrier.MaxPhoneLength)
            .When(x => x.Phone is not null);

        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(Carrier.MaxEmailLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
