using FluentValidation;

namespace ERP.Application.Inventory.UseCases.CreateTransfer;

public sealed class CreateTransferCommandValidator : AbstractValidator<CreateTransferCommand>
{
    public CreateTransferCommandValidator()
    {
        RuleFor(x => x.SourceWarehouseId)
            .NotEmpty().WithMessage("La Warehouse origen es obligatoria.");

        RuleFor(x => x.TargetWarehouseId)
            .NotEmpty().WithMessage("La Warehouse destino es obligatoria.");

        RuleFor(x => x)
            .Must(x => x.SourceWarehouseId != x.TargetWarehouseId)
            .WithMessage("La Warehouse origen y destino deben ser distintas.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Debe incluir al menos un ítem en la transfer.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty().WithMessage("El ID del producto es obligatorio.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
        });
    }
}
