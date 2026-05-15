using FluentValidation;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Application.Modules.Purchasing.UseCases.RechazarCompra;

public sealed class RechazarCompraCommandValidator : AbstractValidator<RechazarCompraCommand>
{
    public RechazarCompraCommandValidator()
    {
        RuleFor(x => x.PurchBillId)
            .NotEmpty()
            .WithMessage("El ID de la factura de compra es obligatorio.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de rechazo es obligatorio.")
            .MaximumLength(PurchBill.RejectionReasonMaxLen)
            .WithMessage($"El motivo no puede superar {PurchBill.RejectionReasonMaxLen} caracteres.");
    }
}
