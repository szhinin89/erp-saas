using FluentValidation;
using ERP.Domain.Configuration.Entities;

namespace ERP.Application.Configuration.UseCases.UpsertSriSettings;

public sealed class UpsertConfiguracionSRICommandValidator : AbstractValidator<UpsertConfiguracionSRICommand>
{
    public UpsertConfiguracionSRICommandValidator()
    {
        RuleFor(x => x.Ruc)
            .NotEmpty().WithMessage("El RUC de la empresa es obligatorio.")
            .Length(13).WithMessage("El RUC debe tener exactamente 13 dígitos.")
            .Matches(@"^\d{13}$").WithMessage("El RUC solo debe contener dígitos numéricos.");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("La razón social es obligatoria.")
            .MaximumLength(SriSettings.LegalNameMaxLen);

        RuleFor(x => x.MainAddress)
            .NotEmpty().WithMessage("La dirección matriz es obligatoria.")
            .MaximumLength(SriSettings.AddressMaxLen);

        RuleFor(x => x.EstabCode)
            .NotEmpty().WithMessage("El establecimiento es obligatorio.")
            .Matches(@"^\d{3}$").WithMessage("El establecimiento debe ser exactamente 3 dígitos (ej: 001).");

        RuleFor(x => x.EmPointCode)
            .NotEmpty().WithMessage("El punto de emisión es obligatorio.")
            .Matches(@"^\d{3}$").WithMessage("El punto de emisión debe ser exactamente 3 dígitos (ej: 001).");

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
