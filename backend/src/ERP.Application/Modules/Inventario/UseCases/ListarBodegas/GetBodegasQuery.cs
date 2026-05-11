using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventario.DTOs;

namespace ERP.Application.Modules.Inventario.UseCases.ListarBodegas;

public sealed record GetBodegasQuery(
    bool?  ActiveFilter,
    string? Search,
    Guid?  SucursalId
) : IRequest<Result<IReadOnlyList<BodegaDto>>>;
