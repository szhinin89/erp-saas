using FluentValidation;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.ImportPurchaseReception;

public sealed class ImportPurchaseReceptionValidator
    : AbstractValidator<ImportPurchaseReceptionCommand>
{
    public ImportPurchaseReceptionValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("Debe adjuntar un archivo.");

        RuleFor(x => x.File.SizeBytes)
            .GreaterThan(0)
            .WithMessage("El archivo está vacío.")
            .When(x => x.File is not null);

        RuleFor(x => x.File.FileName)
            .NotEmpty()
            .WithMessage("El archivo debe tener un nombre.")
            .Must(name => name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .WithMessage("El archivo debe tener extensión .txt.")
            .When(x => x.File is not null);
    }
}
