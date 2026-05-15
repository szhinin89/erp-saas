using FluentValidation;

namespace ERP.Application.Modules.Expenses.UseCases.CrearGasto;

public sealed class CrearGastoCommandValidator : AbstractValidator<CrearGastoCommand>
{
    public CrearGastoCommandValidator()
    {
        When(x => x.Modo == ModoCreacionGasto.Xml, () =>
        {
            RuleFor(x => x.XmlContent)
                .NotNull().WithMessage("El XML es obligatorio en modo XML.")
                .Must(b => b is { Length: > 0 }).WithMessage("El archivo XML no puede estar vacío.");

            RuleFor(x => x.CategoriaGasto)
                .NotEmpty().WithMessage("La categoría de gasto es obligatoria en modo XML.")
                .MaximumLength(ERP.Domain.Modules.Expenses.Entities.GastoFactura.CategoriaGastoMaxLen);
        });

        When(x => x.Modo == ModoCreacionGasto.Manual, () =>
        {
            RuleFor(x => x.FechaEmision)
                .NotNull().WithMessage("La fecha de emisión es obligatoria en modo manual.");

            RuleFor(x => x.Concepto)
                .NotEmpty().WithMessage("El concepto es obligatorio en modo manual.")
                .MaximumLength(ERP.Domain.Modules.Expenses.Entities.GastoFactura.ConceptoMaxLen);

            RuleFor(x => x.CategoriaGasto)
                .NotEmpty().WithMessage("La categoría de gasto es obligatoria en modo manual.")
                .MaximumLength(ERP.Domain.Modules.Expenses.Entities.GastoFactura.CategoriaGastoMaxLen);

            RuleFor(x => x.Subtotal).NotNull().WithMessage("El subtotal es obligatorio en modo manual.");
            RuleFor(x => x.Impuesto).NotNull().WithMessage("El impuesto es obligatorio en modo manual.");
            RuleFor(x => x.Total).NotNull().WithMessage("El total es obligatorio en modo manual.");

            RuleFor(x => x.Subtotal!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("El subtotal no puede ser negativo.");
            RuleFor(x => x.Impuesto!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("El impuesto no puede ser negativo.");
            RuleFor(x => x.Total!.Value)
                .GreaterThan(0).WithMessage("El total debe ser mayor a cero.");
        });
    }
}
