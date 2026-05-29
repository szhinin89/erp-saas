using FluentValidation;
using ERP.Application.Common;

namespace ERP.Application.Modules.Onboarding.CompleteCompanyOnboarding;

public sealed class CompleteCompanyOnboardingValidator : AbstractValidator<CompleteCompanyOnboardingCommand>
{
    public CompleteCompanyOnboardingValidator()
    {
        RuleFor(x => x.TaxId)
            .NotEmpty().WithMessage("El RUC / número de identificación es obligatorio.")
            .MaximumLength(20)
            .Must(tid => !tid.Trim().StartsWith("TMP-EC-", StringComparison.OrdinalIgnoreCase))
            .WithMessage("No puede usar un RUC provisional como identificación definitiva. Ingrese el RUC real de la empresa.");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("La razón social es obligatoria.")
            .MaximumLength(200);

        RuleFor(x => x.MainAddress)
            .NotEmpty().WithMessage("La dirección principal es obligatoria.")
            .Must(a => a.Trim() != "—").WithMessage("Debe ingresar una dirección real.")
            .MaximumLength(500);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El email no tiene formato válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.TradeName).MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(40);
    }
}
