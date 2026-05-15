using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Products.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;

public sealed class CrearOrderPurchaseCommandHandler
    : IRequestHandler<CrearOrderPurchaseCommand, Result<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository  _ordenRepo;
    private readonly ISupplierRepository    _proveedorRepo;
    private readonly IProductRepository      _productRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _currentTenant;
    private readonly ICurrentUser            _currentUser;
    private readonly ILogger<CrearOrderPurchaseCommandHandler> _logger;

    public CrearOrderPurchaseCommandHandler(
        IPurchaseOrderRepository ordenRepo,
        ISupplierRepository proveedorRepo,
        IProductRepository productRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<CrearOrderPurchaseCommandHandler> logger)
    {
        _ordenRepo     = ordenRepo;
        _proveedorRepo = proveedorRepo;
        _productRepo   = productRepo;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
        _logger        = logger;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(
        CrearOrderPurchaseCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var Supplier = await _proveedorRepo.GetByIdAsync(tenantId, command.SupplierId, ct);
        if (Supplier is null || !Supplier.IsActive)
            return Result<PurchaseOrderDto>.Failure("El Supplier no existe o no está activo.");

        // Validar productos y construir detalles
        var productosValidados = new Dictionary<Guid, ERP.Domain.Products.Entities.Product>();
        foreach (var item in command.Items)
        {
            if (productosValidados.ContainsKey(item.ProductId)) continue;
            var producto = await _productRepo.GetByIdAsync(item.ProductId, tenantId, ct);
            if (producto is null || !producto.IsActive)
                return Result<PurchaseOrderDto>.Failure(
                    $"El producto {item.ProductId} no existe o no está activo.");
            productosValidados[item.ProductId] = producto;
        }

        var secuencial = await _ordenRepo.GetNextSequentialAsync(tenantId, ct);

        var orden = PurchaseOrder.Create(
            tenantId, secuencial,
            command.SupplierId, command.RequiredDate,
            command.TargetWarehouseId, command.DeliveryAddress, command.Notes,
            userId);

        foreach (var item in command.Items)
        {
            var producto = productosValidados[item.ProductId];
            var detalle  = PurchaseOrderLine.Create(
                tenantId, orden.Id, item.ProductId,
                producto.ShortName,
                item.Quantity, item.UnitPrice, item.VatPct,
                userId);
            orden.AddLine(detalle);
        }

        await _ordenRepo.AddAsync(orden, ct);
        await _ordenRepo.SaveChangesAsync(ct);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _currentUser.Email, _currentUser.FullName,
            module: "compras", action: "orden-compra.crear",
            entityType: "PurchaseOrder", entityId: orden.Id,
            description: orden.OrderNumber), ct);

        _logger.LogInformation("OC creada: {Numero} ({Id})", orden.OrderNumber, orden.Id);

        return Result<PurchaseOrderDto>.Success(ToDto(orden, Supplier.LegalName));
    }

    internal static PurchaseOrderDto ToDto(
        PurchaseOrder o, string proveedorNombre,
        IReadOnlyList<string>? advertencias = null) => new(
        o.Id, o.OrderNumber,
        o.SupplierId, proveedorNombre,
        o.IssueDate, o.RequiredDate,
        o.Status, o.Currency,
        o.Subtotal, o.TaxTotal, o.Total,
        o.Notes, o.DeliveryAddress,
        o.TargetWarehouseId,
        o.SentAt, o.ApprovedAt, o.ApprovedBy, o.ClosedAt,
        o.CreatedAt)
    {
        Advertencias = advertencias is { Count: > 0 } ? advertencias : null,
    };
}
