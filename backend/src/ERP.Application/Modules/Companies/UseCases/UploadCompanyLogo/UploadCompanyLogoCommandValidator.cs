using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.UploadCompanyLogo;

public sealed class UploadCompanyLogoCommandValidator : AbstractValidator<UploadCompanyLogoCommand>
{
    public UploadCompanyLogoCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("Debe adjuntar un archivo de imagen.");

        RuleFor(x => x.File.SizeBytes)
            .GreaterThan(0).WithMessage("El archivo está vacío.")
            .When(x => x.File is not null);

        RuleFor(x => x.File.FileName)
            .NotEmpty().WithMessage("El archivo debe tener un nombre.")
            .When(x => x.File is not null);
    }
}
