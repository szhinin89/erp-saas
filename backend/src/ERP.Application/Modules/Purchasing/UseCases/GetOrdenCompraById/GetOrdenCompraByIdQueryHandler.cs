using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.GetOrdenCompraById;

public sealed class GetPurchaseOrderByIdQueryHandler
    : IRequestHandler<GetPurchaseOrderByIdQuery, Result<PurchaseOrderDetailDto?>>
{
    private readonly IPurchaseOrderRepository _repo;
    private readonly ISupplierRepository   _proveedorRepo;
    private readonly ICurrentTenant         _currentTenant;

    public GetPurchaseOrderByIdQueryHandler(
        IPurchaseOrderRepository repo,
        ISupplierRepository proveedorRepo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _proveedorRepo = proveedorRepo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<PurchaseOrderDetailDto?>> Handle(
        GetPurchaseOrderByIdQuery query, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;

        var orden = await _repo.GetByIdAsync(tenantId, query.OrderId, ct);
        if (orden is null)
            return Result<PurchaseOrderDetailDto?>.Success(null);

        var Supplier   = await _proveedorRepo.GetByIdAsync(tenantId, orden.SupplierId, ct);
        var vinculadas  = await _repo.GetBillLinksAsync(tenantId, orden.Id, ct);

        var lines = orden.Lines.Select(d => new PurchaseOrderLineDto(
            d.Id, d.ProductId, d.Description,
            d.OrderedQty, d.InvoicedQty, d.PendingToInvoice,
            d.UnitCost, d.Subtotal, d.TaxAmount, d.Total)).ToList();

        var linkedBills = vinculadas
            .Select(v => new LinkedPurchaseBillDto(v.PurchBillId, v.InvoiceNumber, v.LinkedAt))
            .ToList();

        return Result<PurchaseOrderDetailDto?>.Success(new PurchaseOrderDetailDto(
            orden.Id, orden.OrderNumber,
            orden.SupplierId, Supplier?.LegalName ?? orden.SupplierId.ToString(),
            orden.IssueDate, orden.RequiredDate,
            orden.Status, orden.Currency,
            orden.Subtotal, orden.TaxTotal, orden.Total,
            orden.Notes, orden.DeliveryAddress,
            orden.TargetWarehouseId,
            orden.SentAt, orden.ApprovedAt, orden.ApprovedBy, orden.ClosedAt,
            orden.CreatedAt,
            lines, linkedBills));
    }
}


