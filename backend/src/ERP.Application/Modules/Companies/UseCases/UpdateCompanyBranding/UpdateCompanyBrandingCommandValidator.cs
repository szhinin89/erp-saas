using FluentValidation;
using System.Text.Json;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyBranding;

public sealed class UpdateCompanyBrandingCommandValidator : AbstractValidator<UpdateCompanyBrandingCommand>
{
    public UpdateCompanyBrandingCommandValidator()
    {
        RuleFor(x => x.BrandingConfiguration)
            .Must(IsValidJson).WithMessage("La configuración de marca debe ser un JSON válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.BrandingConfiguration));
    }

    private static bool IsValidJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        try
        {
            JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
