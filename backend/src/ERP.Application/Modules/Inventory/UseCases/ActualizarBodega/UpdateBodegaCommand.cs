using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.ActualizarBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record UpdateWarehouseCommand(
    Guid    Id,
    Guid    BranchId,
    string  Name,
    string? Address,
    string? Manager
) : IRequest<Result<WarehouseDto>>;
