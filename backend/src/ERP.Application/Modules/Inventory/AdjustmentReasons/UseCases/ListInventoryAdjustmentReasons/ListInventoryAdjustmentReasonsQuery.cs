using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.ListInventoryAdjustmentReasons;

public sealed record ListInventoryAdjustmentReasonsQuery(
    Guid? CompanyId,
    bool IncludeInactive = false
) : IRequest<Result<IReadOnlyList<InventoryAdjustmentReasonDto>>>, ITenantScopedRequest;
