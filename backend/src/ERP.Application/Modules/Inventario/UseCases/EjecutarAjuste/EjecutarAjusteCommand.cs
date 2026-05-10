using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;

namespace ERP.Application.Inventario.UseCases.EjecutarAjuste;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record EjecutarAjusteCommand(Guid AjusteId)
    : IRequest<Result<AjusteInventarioDto>>;
