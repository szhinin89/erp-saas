using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.GetTransferenciasList;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record GetTransferenciasListQuery(
    int       PageNumber      = 1,
    int       PageSize        = 20,
    Guid?     SourceWarehouseId  = null,
    Guid?     TargetWarehouseId = null,
    string?   Status          = null,
    DateTime? DateFrom      = null,
    DateTime? DateTo      = null
) : IRequest<Result<TransferenciasPagedResult>>;
