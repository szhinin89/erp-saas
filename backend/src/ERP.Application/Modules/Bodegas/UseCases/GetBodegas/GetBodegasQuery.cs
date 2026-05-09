using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Bodegas.DTOs;

namespace ERP.Application.Modules.Bodegas.UseCases.GetBodegas;

public sealed record GetBodegasQuery(
    bool?  ActiveFilter,
    string? Search,
    Guid?  SucursalId
) : IRequest<Result<IReadOnlyList<BodegaDto>>>;
