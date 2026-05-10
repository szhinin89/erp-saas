using FluentValidation;

namespace ERP.Application.Inventario.UseCases.CrearTransferencia;

public sealed class CrearTransferenciaCommandValidator : AbstractValidator<CrearTransferenciaCommand>
{
    public CrearTransferenciaCommandValidator()
    {
        RuleFor(x => x.BodegaOrigenId)
            .NotEmpty().WithMessage("La bodega origen es obligatoria.");

        RuleFor(x => x.BodegaDestinoId)
            .NotEmpty().WithMessage("La bodega destino es obligatoria.");

        RuleFor(x => x)
            .Must(x => x.BodegaOrigenId != x.BodegaDestinoId)
            .WithMessage("La bodega origen y destino deben ser distintas.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Debe incluir al menos un ítem en la transferencia.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductoId)
                .NotEmpty().WithMessage("El ID del producto es obligatorio.");

            item.RuleFor(i => i.Cantidad)
                .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
        });
    }
}
