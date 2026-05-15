using FluentValidation;

namespace ERP.Application.Inventory.UseCases.CrearAjuste;

public sealed class CrearAjusteCommandValidator : AbstractValidator<CrearAjusteCommand>
{
    public CrearAjusteCommandValidator()
    {
        RuleFor(x => x.BodegaId)
            .NotEmpty().WithMessage("La bodega es obligatoria.");

        RuleFor(x => x.ProductoId)
            .NotEmpty().WithMessage("El producto es obligatorio.");

        RuleFor(x => x.CantidadAjuste)
            .NotEqual(0).WithMessage("La cantidad de ajuste no puede ser cero.");

        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("El motivo es obligatorio.")
            .MaximumLength(200).WithMessage("El motivo no puede superar 200 caracteres.");

        RuleFor(x => x.Observaciones)
            .MaximumLength(1000).When(x => x.Observaciones is not null);
    }
}
