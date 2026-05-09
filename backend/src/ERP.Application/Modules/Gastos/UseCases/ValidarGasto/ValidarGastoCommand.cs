using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Gastos.DTOs;

namespace ERP.Application.Modules.Gastos.UseCases.ValidarGasto;

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record ValidarGastoCommand(Guid GastoFacturaId)
    : IRequest<Result<GastoFacturaDto>>;
