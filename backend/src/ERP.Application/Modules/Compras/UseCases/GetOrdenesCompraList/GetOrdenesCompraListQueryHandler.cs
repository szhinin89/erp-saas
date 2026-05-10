using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Application.Modules.Compras.UseCases.CrearOrdenCompra;
using ERP.Domain.Compras.Interfaces;
using ERP.Domain.Proveedores.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.GetOrdenesCompraList;

public sealed class GetOrdenesCompraListQueryHandler
    : IRequestHandler<GetOrdenesCompraListQuery, Result<OrdenesCompraPagedResult>>
{
    private readonly IOrdenCompraRepository _repo;
    private readonly IProveedorRepository   _proveedorRepo;
    private readonly ICurrentTenant         _currentTenant;

    public GetOrdenesCompraListQueryHandler(
        IOrdenCompraRepository repo,
        IProveedorRepository proveedorRepo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _proveedorRepo = proveedorRepo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<OrdenesCompraPagedResult>> Handle(
        GetOrdenesCompraListQuery query, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;

        var (items, total) = await _repo.GetPagedAsync(
            tenantId, query.PageNumber, query.PageSize,
            query.ProveedorId, query.Estado, query.FechaDesde, query.FechaHasta, ct);

        // Batch de proveedores únicos para evitar N+1
        var proveedorIds = items.Select(o => o.ProveedorId).Distinct().ToList();
        var proveedores  = new Dictionary<Guid, string>();
        foreach (var pid in proveedorIds)
        {
            var p = await _proveedorRepo.GetByIdAsync(tenantId, pid, ct);
            proveedores[pid] = p?.RazonSocial ?? pid.ToString();
        }

        var dtos = items.Select(o =>
            CrearOrdenCompraCommandHandler.ToDto(o, proveedores.GetValueOrDefault(o.ProveedorId, ""))).ToList();

        return Result<OrdenesCompraPagedResult>.Success(
            new OrdenesCompraPagedResult(dtos, total, query.PageNumber, query.PageSize));
    }
}
