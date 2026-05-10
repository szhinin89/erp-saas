using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;

namespace ERP.Application.Inventario.UseCases.GetAjusteById;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record GetAjusteByIdQuery(Guid AjusteId)
    : IRequest<Result<AjusteInventarioDto?>>;
