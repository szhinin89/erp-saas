using FluentValidation;

namespace ERP.Application.Inventario.UseCases.EjecutarAjuste;

public sealed class EjecutarAjusteCommandValidator : AbstractValidator<EjecutarAjusteCommand>
{
    public EjecutarAjusteCommandValidator()
    {
        RuleFor(x => x.AjusteId)
            .NotEmpty()
            .WithMessage("El ID del ajuste es obligatorio.");
    }
}
