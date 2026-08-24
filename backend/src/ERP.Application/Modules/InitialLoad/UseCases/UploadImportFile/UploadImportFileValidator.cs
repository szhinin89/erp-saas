using FluentValidation;

namespace ERP.Application.Modules.InitialLoad.UseCases.UploadImportFile;

public sealed class UploadImportFileValidator : AbstractValidator<UploadImportFileCommand>
{
    private const long MaxSizeBytes = 10 * 1024 * 1024;

    public UploadImportFileValidator()
    {
        RuleFor(x => x.ImportBatchId).NotEmpty();
        RuleFor(x => x.Content).NotNull();

        When(
            x => x.Content is not null,
            () =>
            {
                RuleFor(x => x.Content.FileName)
                    .NotEmpty()
                    .Must(name => name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    .WithMessage("Solo se admiten archivos .xlsx.");

                RuleFor(x => x.Content.SizeBytes)
                    .GreaterThan(0)
                    .WithMessage("El archivo está vacío.")
                    .LessThanOrEqualTo(MaxSizeBytes)
                    .WithMessage("El archivo supera el tamaño máximo permitido (10 MB).");
            }
        );
    }
}
