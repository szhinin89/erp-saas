using FluentValidation;
using ERP.Domain.Configuration.Entities;

namespace ERP.Application.Configuration.UseCases.UpsertConfiguracionSRI;

public sealed class UpsertConfiguracionSRICommandValidator : AbstractValidator<UpsertConfiguracionSRICommand>
{
    public UpsertConfiguracionSRICommandValidator()
    {
        RuleFor(x => x.RucEmpresa)
            .NotEmpty().WithMessage("El RUC de la empresa es obligatorio.")
            .Length(13).WithMessage("El RUC debe tener exactamente 13 dígitos.")
            .Matches(@"^\d{13}$").WithMessage("El RUC solo debe contener dígitos numéricos.");

        RuleFor(x => x.RazonSocial)
            .NotEmpty().WithMessage("La razón social es obligatoria.")
            .MaximumLength(ConfiguracionSRI.RazonSocialMaxLen);

        RuleFor(x => x.DireccionMatriz)
            .NotEmpty().WithMessage("La dirección matriz es obligatoria.")
            .MaximumLength(ConfiguracionSRI.DireccionMatrizMaxLen);

        RuleFor(x => x.Establecimiento)
            .NotEmpty().WithMessage("El establecimiento es obligatorio.")
            .Matches(@"^\d{3}$").WithMessage("El establecimiento debe ser exactamente 3 dígitos (ej: 001).");

        RuleFor(x => x.PuntoEmision)
            .NotEmpty().WithMessage("El punto de emisión es obligatorio.")
            .Matches(@"^\d{3}$").WithMessage("El punto de emisión debe ser exactamente 3 dígitos (ej: 001).");

        RuleFor(x => x.CertificadoP12Path)
            .NotEmpty().WithMessage("La ruta del certificado P12 es obligatoria.");

        RuleFor(x => x.CertificadoPassword)
            .NotEmpty().WithMessage("La contraseña del certificado es obligatoria.");

        RuleFor(x => x.Ambiente)
            .Must(a => a == 1 || a == 2)
            .WithMessage("El ambiente debe ser 1 (pruebas) o 2 (producción).");

        RuleFor(x => x.TipoEmision)
            .Must(t => t == 1)
            .WithMessage("El tipo de emisión debe ser 1 (normal).");

        RuleFor(x => x.UrlSriAutorizacion)
            .NotEmpty().WithMessage("La URL de autorización SRI es obligatoria.")
            .MaximumLength(ConfiguracionSRI.UrlSriAutorizacionMaxLen);
    }
}
