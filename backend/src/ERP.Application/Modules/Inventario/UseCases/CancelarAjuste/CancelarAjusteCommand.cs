using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;

namespace ERP.Application.Inventario.UseCases.CancelarAjuste;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CancelarAjusteCommand(Guid AjusteId)
    : IRequest<Result<AjusteInventarioDto>>;
