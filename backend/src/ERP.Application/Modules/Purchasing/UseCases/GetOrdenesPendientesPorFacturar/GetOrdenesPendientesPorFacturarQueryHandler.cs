using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.GetOrdenesPendientesPorFacturar;

public sealed class GetOrdersPendientesPorFacturarQueryHandler
    : IRequestHandler<GetOrdersPendientesPorFacturarQuery, Result<IReadOnlyList<PurchaseOrderDto>>>
{
    private readonly IPurchaseOrderRepository _repo;
    private readonly ISupplierRepository   _proveedorRepo;
    private readonly ICurrentTenant         _currentTenant;

    public GetOrdersPendientesPorFacturarQueryHandler(
        IPurchaseOrderRepository repo,
        ISupplierRepository proveedorRepo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _proveedorRepo = proveedorRepo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<PurchaseOrderDto>>> Handle(
        GetOrdersPendientesPorFacturarQuery query, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var ordenes  = await _repo.GetPendingToInvoiceAsync(tenantId, ct);

        var proveedorIds = ordenes.Select(o => o.SupplierId).Distinct().ToList();
        var proveedores  = new Dictionary<Guid, string>();
        foreach (var pid in proveedorIds)
        {
            var p = await _proveedorRepo.GetByIdAsync(tenantId, pid, ct);
            proveedores[pid] = p?.LegalName ?? pid.ToString();
        }

        var dtos = ordenes
            .Select(o => CrearOrderPurchaseCommandHandler.ToDto(o, proveedores.GetValueOrDefault(o.SupplierId, "")))
            .ToList();

        return Result<IReadOnlyList<PurchaseOrderDto>>.Success(dtos);
    }
}
