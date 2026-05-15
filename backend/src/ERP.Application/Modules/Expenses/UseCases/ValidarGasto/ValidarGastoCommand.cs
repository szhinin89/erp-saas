using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Expenses.DTOs;

namespace ERP.Application.Modules.Expenses.UseCases.ValidarGasto;

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record ValidarGastoCommand(Guid GastoFacturaId)
    : IRequest<Result<GastoFacturaDto>>;
