using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Gastos.DTOs;

namespace ERP.Application.Modules.Gastos.UseCases.AprobarGasto;

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record AprobarGastoCommand(Guid GastoFacturaId)
    : IRequest<Result<GastoFacturaDto>>;
