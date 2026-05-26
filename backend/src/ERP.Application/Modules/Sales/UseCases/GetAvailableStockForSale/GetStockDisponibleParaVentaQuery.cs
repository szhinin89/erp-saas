using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;

namespace ERP.Application.Sales.UseCases.GetAvailableStockForSale;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record GetAvailableStockForSaleQuery(
    Guid    ProductId,
    Guid    WarehouseId
) : IRequest<Result<StockDisponibleDto>>, ICompanyScopedRequest;
