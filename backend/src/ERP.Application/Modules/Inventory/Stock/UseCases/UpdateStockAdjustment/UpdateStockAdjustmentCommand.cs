using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;
using ERP.Domain.Modules.Inventory.Entities;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.UpdateStockAdjustment;

public sealed record UpdateStockAdjustmentCommand(
    Guid Id,
    Guid WarehouseId,
    string WarehouseName,
    string MovementType,
    Guid ReasonId,
    string? Notes,
    IReadOnlyList<CreateStockAdjustmentLineInput> Lines
) : IRequest<Result<StockAdjustmentDto>>, IBranchScopedRequest;

public sealed class UpdateStockAdjustmentValidator : AbstractValidator<UpdateStockAdjustmentCommand>
{
    public UpdateStockAdjustmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.WarehouseName).NotEmpty();
        RuleFor(x => x.MovementType)
            .Must(m => m == StockAdjustment.MovementTypeIngreso || m == StockAdjustment.MovementTypeEgreso)
            .WithMessage(
                $"MovementType debe ser '{StockAdjustment.MovementTypeIngreso}' o '{StockAdjustment.MovementTypeEgreso}'."
            );
        RuleFor(x => x.ReasonId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("El ajuste debe tener al menos una línea.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.ItemId).NotEmpty();
                line.RuleFor(l => l.ItemName).NotEmpty();
                line.RuleFor(l => l.Quantity).GreaterThan(0);
                line.RuleFor(l => l.UnitCostBase).GreaterThanOrEqualTo(0).When(l => l.UnitCostBase.HasValue);
            });
    }
}
