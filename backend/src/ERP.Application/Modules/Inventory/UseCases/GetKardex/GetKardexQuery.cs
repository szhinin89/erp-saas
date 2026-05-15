using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.GetKardex;

public sealed record GetKardexQuery(
    Guid      ProductoId,
    Guid      BodegaId,
    DateTime? FechaInicio,
    DateTime? FechaFin)
    : IRequest<Result<KardexResponse>>;
