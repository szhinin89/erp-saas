using FluentValidation;
using ERP.Domain.Configuration.Entities;

namespace ERP.Application.Configuration.UseCases.UpsertSriSettings;

public sealed class UpsertSriConfigurationCommandValidator : AbstractValidator<UpsertSriConfigurationCommand>
{
    public UpsertSriConfigurationCommandValidator()
    {
        RuleFor(x => x.CertP12Path)
            .NotEmpty().WithMessage("La ruta del certificado P12 es obligatoria.");

        RuleFor(x => x.CertPassword)
            .NotEmpty().WithMessage("La contraseña del certificado es obligatoria.");

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
