using FluentValidation;
using ERP.Domain.Compras.Entities;

namespace ERP.Application.Modules.Compras.UseCases.RechazarCompra;

public sealed class RechazarCompraCommandValidator : AbstractValidator<RechazarCompraCommand>
{
    public RechazarCompraCommandValidator()
    {
        RuleFor(x => x.CompraFacturaId)
            .NotEmpty()
            .WithMessage("El ID de la factura de compra es obligatorio.");

        RuleFor(x => x.Motivo)
            .NotEmpty()
            .WithMessage("El motivo de rechazo es obligatorio.")
            .MaximumLength(CompraFactura.MotivoRechazoMaxLen)
            .WithMessage($"El motivo no puede superar {CompraFactura.MotivoRechazoMaxLen} caracteres.");
    }
}
