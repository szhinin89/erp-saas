using FluentValidation;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Application.Modules.Purchasing.UseCases.RejectPurchase;

public sealed class RejectPurchaseCommandValidator : AbstractValidator<RejectPurchaseCommand>
{
    public RejectPurchaseCommandValidator()
    {
        RuleFor(x => x.PurchBillId)
            .NotEmpty()
            .WithMessage("El ID de la salesBill de compra es obligatorio.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de rechazo es obligatorio.")
            .MaximumLength(PurchBill.RejectionReasonMaxLen)
            .WithMessage($"El motivo no puede superar {PurchBill.RejectionReasonMaxLen} caracteres.");
    }
}
