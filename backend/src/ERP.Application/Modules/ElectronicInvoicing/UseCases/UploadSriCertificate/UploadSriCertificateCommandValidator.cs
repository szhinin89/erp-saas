using ERP.Domain.Configuration.Entities;
using FluentValidation;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.UploadSriCertificate;

public sealed class UploadSriCertificateCommandValidator
    : AbstractValidator<UploadSriCertificateCommand>
{
    private static readonly string[] AllowedExtensions = [".p12", ".pfx"];

    public UploadSriCertificateCommandValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("Debe adjuntar el archivo del certificado.");

        RuleFor(x => x.File.SizeBytes)
            .GreaterThan(0)
            .WithMessage("El archivo está vacío.")
            .LessThanOrEqualTo(SriSettings.CertMaxSizeBytes)
            .WithMessage("El certificado supera el tamaño máximo permitido (5 MB).")
            .When(x => x.File is not null);

        RuleFor(x => x.File.FileName)
            .NotEmpty()
            .WithMessage("El archivo debe tener un nombre.")
            .Must(name => AllowedExtensions.Contains(Path.GetExtension(name).ToLowerInvariant()))
            .WithMessage("El certificado debe tener extensión .p12 o .pfx.")
            .When(x => x.File is not null);
    }
}
