using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Bodegas.DTOs;

namespace ERP.Application.Modules.Bodegas.UseCases.DisableBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record DisableBodegaCommand(Guid Id)
    : IRequest<Result<BodegaDto>>;
