using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyBranding;

/// <summary>
/// CONFIG-FOUNDATION-P1-02: reemplaza la validación anterior ("es JSON válido") por reglas reales
/// sobre cada campo tipado — color hex válido si se especifica, longitudes máximas para
/// eslogan/pie de página.
/// </summary>
public sealed class UpdateCompanyBrandingCommandValidator
    : AbstractValidator<UpdateCompanyBrandingCommand>
{
    private const string HexColorPattern = "^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$";
    private const int MaxSloganLength = 200;
    private const int MaxFooterTextLength = 500;

    public UpdateCompanyBrandingCommandValidator()
    {
        RuleFor(x => x.PrimaryColor)
            .Matches(HexColorPattern)
            .WithMessage("El color primario debe ser un color hexadecimal válido (#RGB o #RRGGBB).")
            .When(x => !string.IsNullOrWhiteSpace(x.PrimaryColor));

        RuleFor(x => x.SecondaryColor)
            .Matches(HexColorPattern)
            .WithMessage("El color secundario debe ser un color hexadecimal válido (#RGB o #RRGGBB).")
            .When(x => !string.IsNullOrWhiteSpace(x.SecondaryColor));

        RuleFor(x => x.Slogan)
            .MaximumLength(MaxSloganLength)
            .WithMessage($"El eslogan no puede superar {MaxSloganLength} caracteres.");

        RuleFor(x => x.DocumentFooterText)
            .MaximumLength(MaxFooterTextLength)
            .WithMessage($"El pie de página de documentos no puede superar {MaxFooterTextLength} caracteres.");
    }
}
