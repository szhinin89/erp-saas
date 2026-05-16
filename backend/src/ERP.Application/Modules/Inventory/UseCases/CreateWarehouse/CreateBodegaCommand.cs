using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.CrearBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record CreateWarehouseCommand(
    Guid    BranchId,
    string  Name,
    string? Address,
    string? Manager
) : IRequest<Result<WarehouseDto>>;
