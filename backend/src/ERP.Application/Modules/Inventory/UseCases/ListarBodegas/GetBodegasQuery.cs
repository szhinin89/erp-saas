using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.ListarBodegas;

public sealed record GetWarehousesQuery(
    bool?  ActiveFilter,
    string? Search,
    Guid?  BranchId
) : IRequest<Result<IReadOnlyList<WarehouseDto>>>, ICacheable
{
    /// <inheritdoc />
    public int CacheTTL => 300;
}
