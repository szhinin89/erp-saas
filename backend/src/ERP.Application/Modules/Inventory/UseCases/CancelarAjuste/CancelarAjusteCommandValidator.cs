using FluentValidation;

namespace ERP.Application.Inventory.UseCases.CancelarAjuste;

public sealed class CancelarAjusteCommandValidator : AbstractValidator<CancelarAjusteCommand>
{
    public CancelarAjusteCommandValidator()
    {
        RuleFor(x => x.AjusteId)
            .NotEmpty()
            .WithMessage("El ID del ajuste es obligatorio.");
    }
}
