using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Gastos.DTOs;

namespace ERP.Application.Modules.Gastos.UseCases.RechazarGasto;

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record RechazarGastoCommand(Guid GastoFacturaId, string Motivo)
    : IRequest<Result<GastoFacturaDto>>;
