using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.ListarBodegas;

public sealed record GetBodegasQuery(
    bool?  ActiveFilter,
    string? Search,
    Guid?  SucursalId
) : IRequest<Result<IReadOnlyList<BodegaDto>>>, ICacheable
{
    /// <inheritdoc />
    public int CacheTTL => 300;
}
