using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchaseOrder;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.LinkInvoiceToPurchaseOrder;

public sealed class LinkInvoiceToPurchaseOrderCommandHandler
    : IRequestHandler<LinkInvoiceToPurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository  _ordenRepo;
    private readonly IPurchBillRepository       _compraRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _currentSubscriber;
    private readonly ICurrentUser            _currentUser;
    private readonly IUnitOfWork             _unitOfWork;
    private readonly ILogger<LinkInvoiceToPurchaseOrderCommandHandler> _logger;

    public LinkInvoiceToPurchaseOrderCommandHandler(
        IPurchaseOrderRepository ordenRepo,
        IPurchBillRepository compraRepo,
        IBusinessPartnerRepository bpRepo,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<LinkInvoiceToPurchaseOrderCommandHandler> logger)
    {
        _ordenRepo     = ordenRepo;
        _compraRepo    = compraRepo;
        _bpRepo = bpRepo;
        _activity      = activity;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
        _unitOfWork    = unitOfWork;
        _logger        = logger;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(
        LinkInvoiceToPurchaseOrderCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        // 1. Cargar la OC con sus detalles
        var orden = await _ordenRepo.GetByIdAsync(subscriberId, command.OrdenCompraId, ct);
        if (orden is null)
            return Result<PurchaseOrderDto>.Failure("Orden de compra no encontrada.");

        if (orden.Status is not ("Approved" or "PartiallyReceived"))
            return Result<PurchaseOrderDto>.Failure(
                $"Solo se puede vincular una factura a OC en Approved o PartiallyReceived (estado: {orden.Status}).");

        // 2. Cargar la factura con sus detalles
        var factura = await _compraRepo.GetByIdAsync(subscriberId, command.PurchBillId, ct);
        if (factura is null)
            return Result<PurchaseOrderDto>.Failure("Factura de compra no encontrada.");

        if (factura.Status != PurchaseStatus.Approved)
            return Result<PurchaseOrderDto>.Failure(
                "Solo se pueden vincular facturas en estado IsApproved.");

        // 3. Verificar que no estÃ© ya vinculada a esta OC
        var yaVinculada = await _ordenRepo.BillAlreadyLinkedAsync(
            subscriberId, command.OrdenCompraId, command.PurchBillId, ct);
        if (yaVinculada)
            return Result<PurchaseOrderDto>.Failure("Esta factura ya estÃ¡ vinculada a la orden de compra.");

        // 4. Matching por ProductoId, validaciÃ³n de cantidades y detecciÃ³n de discrepancias de precio
        const decimal ToleranciaPrecioPct = 0.01m; // 1 % â€” diferencias menores se ignoran
        var advertencias = new List<string>();

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            foreach (var detalleFactura in factura.Lines)
            {
                // Saltear lÃ­neas sin producto en catÃ¡logo (servicios, fletes, etc.)
                if (detalleFactura.ProductId is null) continue;

                var detalleOrden = orden.Lines
                    .FirstOrDefault(d => d.ProductId == detalleFactura.ProductId.Value);

                if (detalleOrden is null)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<PurchaseOrderDto>.Failure(
                        $"El producto '{detalleFactura.Description}' de la factura no estÃ¡ incluido en esta orden de compra.");
                }

                var nuevoFacturado = detalleOrden.InvoicedQty + detalleFactura.Quantity;
                if (nuevoFacturado > detalleOrden.OrderedQty)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<PurchaseOrderDto>.Failure(
                        $"La cantidad a facturar ({nuevoFacturado:F3}) excede la cantidad pedida " +
                        $"({detalleOrden.OrderedQty:F3}) para '{detalleFactura.Description}'. " +
                        $"Pendiente por facturar: {detalleOrden.PendingToInvoice:F3}.");
                }

                // ValidaciÃ³n de precio: advertencia si la diferencia supera la tolerancia
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
                            "OC {OC} â€“ {Aviso}", orden.OrderNumber, aviso);
                    }
                }

                detalleOrden.AddInvoicedQuantity(detalleFactura.Quantity, userId);
                detalleFactura.LinkPurchaseOrderLine(detalleOrden.Id, userId);
            }

            // 5. Crear la vinculaciÃ³n
            var vinculo = PurchaseOrderBill.Create(subscriberId, orden.Id, factura.Id, userId);
            await _ordenRepo.AddOrderBillLinkAsync(vinculo, ct);

            // 6. Actualizar estado de la OC segÃºn cobertura
            var todoFacturado = orden.Lines.All(d => d.InvoicedQty >= d.OrderedQty);
            if (todoFacturado)
                orden.Close(userId);
            else if (orden.Status == "Approved")
                orden.MarkPartiallyReceived(userId);

            var actividadDesc = advertencias.Count > 0
                ? $"{orden.OrderNumber} â† {factura.InvoiceNumber} | âš  {advertencias.Count} advertencia(s) de precio"
                : $"{orden.OrderNumber} â† {factura.InvoiceNumber}";

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "compras", action: "orden-compra.vincular-factura",
                entityType: "PurchaseOrder", entityId: orden.Id,
                description: actividadDesc), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Factura {Factura} vinculada a OC {OC}. Estado: {Estado}. Warnings: {N}",
                factura.InvoiceNumber, orden.OrderNumber, orden.Status, advertencias.Count);

            var bp = await _bpRepo.GetByIdAsync(orden.BusinessPartnerId, ct);
            return Result<PurchaseOrderDto>.Success(
                CreatePurchaseOrderCommandHandler.ToDto(
                    orden,
                    bp?.LegalName ?? orden.BusinessPartnerId.ToString(),
                    advertencias));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al vincular factura a OC {OrdenId}", command.OrdenCompraId);
            return Result<PurchaseOrderDto>.Failure($"No se pudo vincular la factura: {ex.Message}");
        }
    }
}
