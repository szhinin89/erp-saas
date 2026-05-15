using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.GetOrdenesCompraList;

public sealed class GetPurchaseOrdersListQueryHandler
    : IRequestHandler<GetPurchaseOrdersListQuery, Result<PurchaseOrdersPagedResult>>
{
    private readonly IPurchaseOrderRepository _repo;
    private readonly ISupplierRepository   _proveedorRepo;
    private readonly ICurrentTenant         _currentTenant;

    public GetPurchaseOrdersListQueryHandler(
        IPurchaseOrderRepository repo,
        ISupplierRepository proveedorRepo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _proveedorRepo = proveedorRepo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<PurchaseOrdersPagedResult>> Handle(
        GetPurchaseOrdersListQuery query, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;

        var (items, total) = await _repo.GetPagedAsync(
            tenantId, query.PageNumber, query.PageSize,
            query.SupplierId, query.Status, query.DateFrom, query.DateTo, ct);

        // Batch de proveedores únicos para evitar N+1
        var proveedorIds = items.Select(o => o.SupplierId).Distinct().ToList();
        var proveedores  = new Dictionary<Guid, string>();
        foreach (var pid in proveedorIds)
        {
            var p = await _proveedorRepo.GetByIdAsync(tenantId, pid, ct);
            proveedores[pid] = p?.LegalName ?? pid.ToString();
        }

        var dtos = items.Select(o =>
            CrearOrderPurchaseCommandHandler.ToDto(o, proveedores.GetValueOrDefault(o.SupplierId, ""))).ToList();

        return Result<PurchaseOrdersPagedResult>.Success(
            new PurchaseOrdersPagedResult(dtos, total, query.PageNumber, query.PageSize));
    }
}
