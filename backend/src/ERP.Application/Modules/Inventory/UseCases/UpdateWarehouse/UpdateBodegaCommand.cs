using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.ActualizarBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record UpdateWarehouseCommand(
    Guid     Id,
    Guid     BranchId,
    string   Name,
    string?  StorageType,
    string?  Address,
    string?  Phone,
    string?  Email,
    string?  Manager,
    string?  Latitude,
    string?  Longitude,
    decimal? Capacity,
    decimal? DailyDispatchGoal
) : IRequest<Result<WarehouseDto>>;
