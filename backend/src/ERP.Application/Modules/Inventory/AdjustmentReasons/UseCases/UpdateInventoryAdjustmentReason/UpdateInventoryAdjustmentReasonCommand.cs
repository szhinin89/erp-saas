using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Modules.Inventory.Entities;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.UpdateInventoryAdjustmentReason;

public sealed record UpdateInventoryAdjustmentReasonCommand(
    Guid Id,
    string Name,
    string AllowedMovementType,
    bool RequiresNotes,
    int SortOrder
) : IRequest<Result<InventoryAdjustmentReasonDto>>, ITenantScopedRequest;

public sealed class UpdateInventoryAdjustmentReasonValidator
    : AbstractValidator<UpdateInventoryAdjustmentReasonCommand>
{
    public UpdateInventoryAdjustmentReasonValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(InventoryAdjustmentReason.NameMaxLen);
        RuleFor(x => x.AllowedMovementType)
            .Must(m =>
                m == InventoryAdjustmentReason.Ingreso
                || m == InventoryAdjustmentReason.Egreso
                || m == InventoryAdjustmentReason.Ambos
            )
            .WithMessage("AllowedMovementType debe ser 'Ingreso', 'Egreso' o 'Ambos'.");
    }
}
