using FluentValidation;

namespace ERP.Application.Sales.UseCases.AnularFactura;

public sealed class AnularFacturaCommandValidator : AbstractValidator<AnularFacturaCommand>
{
    public AnularFacturaCommandValidator()
    {
        RuleFor(x => x.VentaId)
            .NotEmpty().WithMessage("El ID de la factura es obligatorio.");
    }
}
