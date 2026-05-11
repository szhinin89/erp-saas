using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Gastos.DTOs;
using ERP.Domain.Modules.Gastos.Enums;

namespace ERP.Application.Modules.Gastos.UseCases.GetGastos;

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record GetGastosQuery(
    EstadoGasto? Estado,
    Guid?        ProveedorId,
    DateTime?    Desde,
    DateTime?    Hasta,
    string?      Search
) : IRequest<Result<IReadOnlyList<GastoFacturaDto>>>;
