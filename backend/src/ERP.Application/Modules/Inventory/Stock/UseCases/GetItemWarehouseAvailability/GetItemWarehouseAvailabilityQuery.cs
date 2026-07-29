using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetItemWarehouseAvailability;

public sealed record GetItemWarehouseAvailabilityQuery(Guid ItemId)
    : IRequest<Result<IReadOnlyList<ItemWarehouseAvailabilityDto>>>,
        IBranchScopedRequest;
