using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.EnableWarehouse;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record EnableWarehouseCommand(Guid Id)
    : IRequest<Result<WarehouseDto>>, ICompanyScopedRequest;
