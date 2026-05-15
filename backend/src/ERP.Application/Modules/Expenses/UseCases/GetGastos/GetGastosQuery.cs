using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Application.Modules.Expenses.UseCases.GetGastos;

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record GetGastosQuery(
    EstadoGasto? Estado,
    Guid?        ProveedorId,
    DateTime?    Desde,
    DateTime?    Hasta,
    string?      Search
) : IRequest<Result<IReadOnlyList<GastoFacturaDto>>>;
