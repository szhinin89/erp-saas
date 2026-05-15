using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.GetAjustesList;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record GetStockAdjustmentsListQuery(
    int       PageNumber,
    int       PageSize,
    Guid?     WarehouseId,
    Guid?     ProductId,
    string?   Status,
    DateTime? DateFrom,
    DateTime? DateTo
) : IRequest<Result<StockAdjustmentsPagedResult>>;
