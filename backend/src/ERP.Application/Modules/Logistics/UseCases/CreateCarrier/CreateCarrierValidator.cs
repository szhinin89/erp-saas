using ERP.Domain.Modules.Logistics.Entities;
using FluentValidation;

namespace ERP.Application.Modules.Logistics.UseCases.CreateCarrier;

public class CreateCarrierValidator : AbstractValidator<CreateCarrierCommand>
{
    private static readonly string[] AllowedIdTypes = ["RUC", "CI", "PASSPORT"];

    public CreateCarrierValidator()
    {
        RuleFor(x => x.IdentificationType)
            .NotEmpty()
            .Must(t => AllowedIdTypes.Contains(t.ToUpperInvariant()))
            .WithMessage("Identification type must be RUC, CI or PASSPORT.");

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
