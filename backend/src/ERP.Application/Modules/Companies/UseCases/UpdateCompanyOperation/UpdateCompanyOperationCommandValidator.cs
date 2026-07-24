using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyOperation;

public sealed class UpdateCompanyOperationCommandValidator : AbstractValidator<UpdateCompanyOperationCommand>
{
    private static readonly string[] SupportedLanguageCodes = ["es", "en", "qu"];

    public UpdateCompanyOperationCommandValidator()
    {
        RuleFor(x => x.LanguageCode)
            .NotEmpty().WithMessage("El idioma principal es obligatorio.")
            .Must(code => SupportedLanguageCodes.Contains(code.Trim().ToLowerInvariant()))
            .WithMessage("Idioma no soportado.");
    }
}
