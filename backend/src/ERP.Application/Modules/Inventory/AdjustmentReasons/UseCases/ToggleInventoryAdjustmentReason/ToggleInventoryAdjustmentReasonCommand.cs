using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.ToggleInventoryAdjustmentReason;

public sealed record ToggleInventoryAdjustmentReasonCommand(Guid Id, bool Activate)
    : IRequest<Result<InventoryAdjustmentReasonDto>>, ITenantScopedRequest;

public sealed class ToggleInventoryAdjustmentReasonValidator
    : AbstractValidator<ToggleInventoryAdjustmentReasonCommand>
{
    public ToggleInventoryAdjustmentReasonValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
