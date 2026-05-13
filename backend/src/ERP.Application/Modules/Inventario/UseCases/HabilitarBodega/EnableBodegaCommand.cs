using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventario.DTOs;

namespace ERP.Application.Modules.Inventario.UseCases.HabilitarBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record EnableBodegaCommand(Guid Id)
    : IRequest<Result<BodegaDto>>;
