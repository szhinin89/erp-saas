using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.UpdateWarehouse;

public sealed record UpdateWarehouseCommand(
    Guid Id,
    Guid BranchId,
    string Name,
    string? StorageType,
    string? Address,
    string? Phone,
    string? Email,
    string? Manager,
    string? Latitude,
    string? Longitude,
    decimal? Capacity,
    decimal? DailyDispatchGoal
) : IRequest<Result<WarehouseListItemDto>>, ICompanyScopedRequest;
