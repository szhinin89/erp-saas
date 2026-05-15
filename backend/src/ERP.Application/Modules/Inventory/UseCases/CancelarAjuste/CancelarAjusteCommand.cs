using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.CancelarAjuste;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CancelarAjusteCommand(Guid AjusteId)
    : IRequest<Result<StockAdjustmentDto>>;
