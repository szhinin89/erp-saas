using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.HabilitarBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record EnableBodegaCommand(Guid Id)
    : IRequest<Result<BodegaDto>>;
