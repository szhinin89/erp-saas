using FluentValidation;
using ERP.Domain.Configuration.Entities;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSriConfiguration;

public sealed class UpsertSriConfigurationCommandValidator : AbstractValidator<UpsertSriConfigurationCommand>
{
    public UpsertSriConfigurationCommandValidator()
    {
        // Vacío es válido: conserva la contraseña ya cifrada (si existe) o deja la configuración
        // sin contraseña hasta que se suba el certificado y se establezca una.
        RuleFor(x => x.CertPassword)
            .MaximumLength(SriSettings.CertPasswordMaxLen);

        RuleFor(x => x.Environment)
            .Must(a => a == 1 || a == 2)
            .WithMessage("El ambiente debe ser 1 (pruebas) o 2 (producción).");

        RuleFor(x => x.EmissionType)
            .Must(t => t == 1)
            .WithMessage("El tipo de emisión debe ser 1 (normal).");

        RuleFor(x => x.WsdlUrl)
            .NotEmpty().WithMessage("La URL de autorización SRI es obligatoria.")
            .MaximumLength(SriSettings.WsdlUrlMaxLen);
    }
}
