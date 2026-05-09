using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Bodegas.DTOs;

namespace ERP.Application.Modules.Bodegas.UseCases.EnableBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record EnableBodegaCommand(Guid Id)
    : IRequest<Result<BodegaDto>>;
