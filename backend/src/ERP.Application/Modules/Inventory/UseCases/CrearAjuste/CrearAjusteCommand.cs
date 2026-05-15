using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.CrearAjuste;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record CreateStockAdjustmentCommand(
    Guid    WarehouseId,
    Guid    ProductId,
    decimal AdjustmentQty,
    string  Reason,
    string? Notes
) : IRequest<Result<StockAdjustmentDto>>;
