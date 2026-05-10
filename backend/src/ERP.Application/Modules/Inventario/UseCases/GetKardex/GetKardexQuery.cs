using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;

namespace ERP.Application.Inventario.UseCases.GetKardex;

public sealed record GetKardexQuery(
    Guid      ProductoId,
    Guid      BodegaId,
    DateTime? FechaInicio,
    DateTime? FechaFin)
    : IRequest<Result<KardexResponse>>;
