using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetItemWarehouseAvailability;

public sealed record GetItemWarehouseAvailabilityQuery(Guid ItemId)
    : IRequest<Result<IReadOnlyList<ItemWarehouseAvailabilityDto>>>, IBranchScopedRequest;
