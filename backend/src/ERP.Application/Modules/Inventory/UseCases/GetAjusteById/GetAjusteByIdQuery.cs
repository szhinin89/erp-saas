using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.GetAjusteById;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record GetAjusteByIdQuery(Guid AjusteId)
    : IRequest<Result<StockAdjustmentDto?>>;
