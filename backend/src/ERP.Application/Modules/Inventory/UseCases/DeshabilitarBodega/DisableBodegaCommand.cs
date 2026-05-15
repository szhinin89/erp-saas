using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.DeshabilitarBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record DisableBodegaCommand(Guid Id)
    : IRequest<Result<BodegaDto>>;
