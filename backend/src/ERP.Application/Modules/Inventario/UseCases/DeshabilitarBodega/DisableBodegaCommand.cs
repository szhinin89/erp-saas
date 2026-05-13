using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventario.DTOs;

namespace ERP.Application.Modules.Inventario.UseCases.DeshabilitarBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record DisableBodegaCommand(Guid Id)
    : IRequest<Result<BodegaDto>>;
