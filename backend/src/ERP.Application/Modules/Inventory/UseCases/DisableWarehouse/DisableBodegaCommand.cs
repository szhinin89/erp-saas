using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.DisableWarehouse;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record DisableWarehouseCommand(Guid Id)
    : IRequest<Result<WarehouseDto>>, ICompanyScopedRequest;
