using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.EjecutarAjuste;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record EjecutarAjusteCommand(Guid AjusteId)
    : IRequest<Result<StockAdjustmentDto>>;
