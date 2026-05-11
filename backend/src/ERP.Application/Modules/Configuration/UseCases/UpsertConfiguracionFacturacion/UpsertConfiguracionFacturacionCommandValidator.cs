using FluentValidation;

namespace ERP.Application.Configuration.UseCases.UpsertConfiguracionFacturacion;

public sealed class UpsertConfiguracionFacturacionCommandValidator
    : AbstractValidator<UpsertConfiguracionFacturacionCommand>
{
    public UpsertConfiguracionFacturacionCommandValidator()
    {
        RuleFor(x => x.RazonSocial)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.NombreComercial)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.Ruc)
            .NotEmpty()
            .Length(13);

        RuleFor(x => x.DireccionMatriz)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(x => x.Telefono)
            .NotEmpty()
            .MaximumLength(25);

        RuleFor(x => x.Correo)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Correo));

        RuleFor(x => x.ContribuyenteEspecial)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.ContribuyenteEspecial));

        RuleFor(x => x.LogoBase64)
            .MaximumLength(100000)
            .When(x => !string.IsNullOrWhiteSpace(x.LogoBase64));

        RuleFor(x => x.LeyendaAdicional)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.LeyendaAdicional));

        RuleFor(x => x.AnchoTirilla)
            .InclusiveBetween(58, 80)
            .Must(x => x == 58 || x == 80)
            .WithMessage("El ancho de tirilla debe ser 58 o 80 mm.");
    }
}
