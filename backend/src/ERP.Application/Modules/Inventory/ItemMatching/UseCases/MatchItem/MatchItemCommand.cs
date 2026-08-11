using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.MatchItem;

/// <summary>Vinculación manual de una línea de recepción a un Item existente — Item Matching.</summary>
public sealed record MatchItemCommand(
    Guid PurchaseReceptionLineId,
    Guid ItemId,
    Guid? PackagingLevelId = null
)
    : IRequest<Result<PurchaseReceptionLineMatchDto>>,
        IBranchScopedRequest;
