using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Application.Modules.Compras.UseCases.CrearOrdenCompra;
using ERP.Domain.Compras.Interfaces;
using ERP.Domain.Proveedores.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.GetOrdenesPendientesPorFacturar;

public sealed class GetOrdenesPendientesPorFacturarQueryHandler
    : IRequestHandler<GetOrdenesPendientesPorFacturarQuery, Result<IReadOnlyList<OrdenCompraDto>>>
{
    private readonly IOrdenCompraRepository _repo;
    private readonly IProveedorRepository   _proveedorRepo;
    private readonly ICurrentTenant         _currentTenant;

    public GetOrdenesPendientesPorFacturarQueryHandler(
        IOrdenCompraRepository repo,
        IProveedorRepository proveedorRepo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _proveedorRepo = proveedorRepo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<OrdenCompraDto>>> Handle(
        GetOrdenesPendientesPorFacturarQuery query, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var ordenes  = await _repo.GetPendientesPorFacturarAsync(tenantId, ct);

        var proveedorIds = ordenes.Select(o => o.ProveedorId).Distinct().ToList();
        var proveedores  = new Dictionary<Guid, string>();
        foreach (var pid in proveedorIds)
        {
            var p = await _proveedorRepo.GetByIdAsync(tenantId, pid, ct);
            proveedores[pid] = p?.RazonSocial ?? pid.ToString();
        }

        var dtos = ordenes
            .Select(o => CrearOrdenCompraCommandHandler.ToDto(o, proveedores.GetValueOrDefault(o.ProveedorId, "")))
            .ToList();

        return Result<IReadOnlyList<OrdenCompraDto>>.Success(dtos);
    }
}
