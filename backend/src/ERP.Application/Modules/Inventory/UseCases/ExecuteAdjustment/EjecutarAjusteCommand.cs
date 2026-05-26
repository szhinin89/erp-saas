using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.ExecuteAdjustment;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record ExecuteStockAdjustmentCommand(Guid AdjustmentId)
    : IRequest<Result<StockAdjustmentDto>>, ICompanyScopedRequest;
