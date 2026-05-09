using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Gastos.DTOs;

namespace ERP.Application.Modules.Gastos.UseCases.GetGastoById;

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record GetGastoByIdQuery(Guid Id)
    : IRequest<Result<GastoFacturaDto?>>;
