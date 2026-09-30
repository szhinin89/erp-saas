using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompany;

/// <summary>
/// Datos de identidad de empresa que llegan en un comando de edición (razón social, nombre
/// comercial y RUC). Lo implementan los dos comandos que editan la identidad:
/// <see cref="UpdateCompanyCommand"/> (usuario del tenant) y
/// <c>UpdateCompanyForAdminCoreCommand</c> (Admin Global).
/// </summary>
public interface ICompanyIdentityInput
{
    string LegalName { get; }
    string? TradeName { get; }
    string? TaxId { get; }
}

/// <summary>
/// ZH-COMPANY-IDENTITY-SSOT-01 — regla única de formato de la identidad de empresa, incluida por el
/// validator de cada comando (<c>Include</c>): mismo mensaje, misma propiedad y mismo
/// <c>VALIDATION_ERROR</c> desde ambos contextos. RUC: validador oficial ecuatoriano
/// (<see cref="ERP.Domain.Common.Validators.RucValidator"/>); vacío = conservar el actual.
/// Interno: no se registra como validator independiente en el pipeline.
/// </summary>
internal sealed class CompanyIdentityRules : AbstractValidator<ICompanyIdentityInput>
{
    public CompanyIdentityRules()
    {
        RuleFor(x => x.LegalName)
            .NotEmpty()
            .WithMessage("La razón social es obligatoria.")
            .MaximumLength(200);

        RuleFor(x => x.TaxId)
            .Must(ruc => ruc!.Trim().All(c => c is >= '0' and <= '9')
                && ERP.Domain.Common.Validators.RucValidator.EsRucValido(ruc))
            .WithMessage("El RUC ecuatoriano no es válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxId));

        RuleFor(x => x.TradeName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.TradeName));
    }
}
