using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Modules.Inventory.Entities;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.CreateInventoryAdjustmentReason;

public sealed record CreateInventoryAdjustmentReasonCommand(
    Guid? CompanyId,
    string Code,
    string Name,
    string AllowedMovementType,
    bool RequiresNotes,
    int SortOrder
) : IRequest<Result<InventoryAdjustmentReasonDto>>, ITenantScopedRequest;

public sealed class CreateInventoryAdjustmentReasonValidator
    : AbstractValidator<CreateInventoryAdjustmentReasonCommand>
{
    public CreateInventoryAdjustmentReasonValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(InventoryAdjustmentReason.CodeMaxLen);
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
