using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.VincularFacturaAOrdenCompra;

public sealed class VincularFacturaAOrdenCompraCommandHandler
    : IRequestHandler<VincularFacturaAOrdenCompraCommand, Result<OrdenCompraDto>>
{
    private readonly IPurchaseOrderRepository  _ordenRepo;
    private readonly IPurchBillRepository       _compraRepo;
    private readonly ISupplierRepository    _proveedorRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _currentTenant;
    private readonly ICurrentUser            _currentUser;
    private readonly IUnitOfWork             _unitOfWork;
    private readonly ILogger<VincularFacturaAOrdenCompraCommandHandler> _logger;

    public VincularFacturaAOrdenCompraCommandHandler(
        IPurchaseOrderRepository ordenRepo,
        IPurchBillRepository compraRepo,
        ISupplierRepository proveedorRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<VincularFacturaAOrdenCompraCommandHandler> logger)
    {
        _ordenRepo     = ordenRepo;
        _compraRepo    = compraRepo;
        _proveedorRepo = proveedorRepo;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
        _unitOfWork    = unitOfWork;
        _logger        = logger;
    }

    public async Task<Result<OrdenCompraDto>> Handle(
        VincularFacturaAOrdenCompraCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        // 1. Cargar la OC con sus detalles
        var orden = await _ordenRepo.GetByIdAsync(tenantId, command.OrdenCompraId, ct);
        if (orden is null)
            return Result<OrdenCompraDto>.Failure("Orden de compra no encontrada.");

        if (orden.Status is not ("Aprobada" or "RecibidaParcial"))
            return Result<OrdenCompraDto>.Failure(
                $"Solo se puede vincular una factura a OC en Aprobada o RecibidaParcial (estado: {orden.Status}).");

        // 2. Cargar la factura con sus detalles
        var factura = await _compraRepo.GetByIdAsync(tenantId, command.PurchBillId, ct);
        if (factura is null)
            return Result<OrdenCompraDto>.Failure("Factura de compra no encontrada.");

        if (factura.Status != PurchaseStatus.Approved)
            return Result<OrdenCompraDto>.Failure(
                "Solo se pueden vincular facturas en estado Aprobado.");

        // 3. Verificar que no esté ya vinculada a esta OC
        var yaVinculada = await _ordenRepo.BillAlreadyLinkedAsync(
            tenantId, command.OrdenCompraId, command.PurchBillId, ct);
        if (yaVinculada)
            return Result<OrdenCompraDto>.Failure("Esta factura ya está vinculada a la orden de compra.");

        // 4. Matching por ProductoId, validación de cantidades y detección de discrepancias de precio
        const decimal ToleranciaPrecioPct = 0.01m; // 1 % — diferencias menores se ignoran
        var advertencias = new List<string>();

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            foreach (var detalleFactura in factura.Lines)
            {
                // Saltear líneas sin producto en catálogo (servicios, fletes, etc.)
                if (detalleFactura.ProductId is null) continue;

                var detalleOrden = orden.Lines
                    .FirstOrDefault(d => d.ProductId == detalleFactura.ProductId.Value);

                if (detalleOrden is null)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<OrdenCompraDto>.Failure(
                        $"El producto '{detalleFactura.Description}' de la factura no está incluido en esta orden de compra.");
                }

                var nuevoFacturado = detalleOrden.InvoicedQty + detalleFactura.Quantity;
                if (nuevoFacturado > detalleOrden.OrderedQty)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<OrdenCompraDto>.Failure(
                        $"La cantidad a facturar ({nuevoFacturado:F3}) excede la cantidad pedida " +
                        $"({detalleOrden.OrderedQty:F3}) para '{detalleFactura.Description}'. " +
                        $"Pendiente por facturar: {detalleOrden.PendingToInvoice:F3}.");
                }

                // Validación de precio: advertencia si la diferencia supera la tolerancia
                if (detalleOrden.UnitCost > 0)
                {
                    var diferenciaPct = Math.Abs(detalleFactura.UnitPrice - detalleOrden.UnitCost)
                                        / detalleOrden.UnitCost;
                    if (diferenciaPct > ToleranciaPrecioPct)
                    {
                        var aviso = $"Discrepancia de precio en '{detalleFactura.Description}': " +
                                    $"OC ${detalleOrden.UnitCost:F4} vs " +
                                    $"Factura ${detalleFactura.UnitPrice:F4} " +
                                    $"({diferenciaPct:P1} diferencia).";
                        advertencias.Add(aviso);
                        _logger.LogWarning(
                            "OC {OC} – {Aviso}", orden.OrderNumber, aviso);
                    }
                }

                detalleOrden.AddInvoicedQuantity(detalleFactura.Quantity, userId);
                detalleFactura.LinkPurchaseOrderLine(detalleOrden.Id, userId);
            }

            // 5. Crear la vinculación
            var vinculo = PurchaseOrderBill.Create(tenantId, orden.Id, factura.Id, userId);
            await _ordenRepo.AddOrderBillLinkAsync(vinculo, ct);

            // 6. Actualizar estado de la OC según cobertura
            var todoFacturado = orden.Lines.All(d => d.InvoicedQty >= d.OrderedQty);
            if (todoFacturado)
                orden.Close(userId);
            else if (orden.Status == "Aprobada")
                orden.MarkPartiallyReceived(userId);

            var actividadDesc = advertencias.Count > 0
                ? $"{orden.OrderNumber} ← {factura.InvoiceNumber} | ⚠ {advertencias.Count} advertencia(s) de precio"
                : $"{orden.OrderNumber} ← {factura.InvoiceNumber}";

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "compras", action: "orden-compra.vincular-factura",
                entityType: "PurchaseOrder", entityId: orden.Id,
                description: actividadDesc), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Factura {Factura} vinculada a OC {OC}. Estado: {Estado}. Advertencias: {N}",
                factura.InvoiceNumber, orden.OrderNumber, orden.Status, advertencias.Count);

            var Supplier = await _proveedorRepo.GetByIdAsync(tenantId, orden.SupplierId, ct);
            return Result<OrdenCompraDto>.Success(
                CrearOrdenCompraCommandHandler.ToDto(
                    orden,
                    Supplier?.LegalName ?? orden.SupplierId.ToString(),
                    advertencias));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al vincular factura a OC {OrdenId}", command.OrdenCompraId);
            return Result<OrdenCompraDto>.Failure($"No se pudo vincular la factura: {ex.Message}");
        }
    }
}
