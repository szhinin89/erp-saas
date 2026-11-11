using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompany;

/// <summary>
/// Razón social y nombre comercial. Los implementan el comando de Admin Global
/// (<c>UpdateCompanyForAdminCoreCommand</c>, vía <see cref="ICompanyIdentityInput"/>) y el comando
/// de perfil de la empresa activa (<c>UpdateCompanyProfileCommand</c>).
/// </summary>
public interface ICompanyNamesInput
{
    string LegalName { get; }
    string? TradeName { get; }
}

/// <summary>
/// Identidad completa (nombres + RUC); solo la edita Admin Global
/// (<c>UpdateCompanyForAdminCoreCommand</c>). El administrador de la empresa activa edita únicamente
/// los nombres; el RUC y el estado no viajan en su comando.
/// </summary>
public interface ICompanyIdentityInput : ICompanyNamesInput
{
    string? TaxId { get; }
}

/// <summary>
/// Regla única de formato de razón social y nombre comercial, incluida (<c>Include</c>) por el
/// validator de cada comando que los edite: mismo mensaje y misma propiedad desde ambos contextos.
/// Interno: no se registra como validator independiente en el pipeline.
/// </summary>
internal sealed class CompanyNamesRules : AbstractValidator<ICompanyNamesInput>
{
    public CompanyNamesRules()
    {
        RuleFor(x => x.LegalName)
            .NotEmpty()
            .WithMessage("La razón social es obligatoria.")
            .MaximumLength(200);

        RuleFor(x => x.TradeName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.TradeName));
    }
}

/// <summary>
/// ZH-COMPANY-IDENTITY-SSOT-01 — regla única de formato de la identidad de empresa para Admin Global:
/// nombres (<see cref="CompanyNamesRules"/>) + RUC. RUC: validador oficial ecuatoriano
/// (<see cref="ERP.Domain.Common.Validators.RucValidator"/>); vacío = conservar el actual.
/// Interno: no se registra como validator independiente en el pipeline.
/// </summary>
internal sealed class CompanyIdentityRules : AbstractValidator<ICompanyIdentityInput>
{
    public CompanyIdentityRules()
    {
        Include(new CompanyNamesRules());

        RuleFor(x => x.TaxId)
            .Must(ruc =>
                ruc!.Trim().All(c => c is >= '0' and <= '9')
                && ERP.Domain.Common.Validators.RucValidator.EsRucValido(ruc)
            )
            .WithMessage("El RUC ecuatoriano no es válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxId));

        RuleFor(x => x.TradeName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.TradeName));
    }
}
