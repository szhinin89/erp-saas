using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Events;

namespace ERP.Application.Modules.Inventory.EventHandlers;

public sealed class VentaAutorizadaEventHandler : INotificationHandler<VentaAutorizadaEvent>
{
    private readonly IInventarioStockRepository _inventario;
    private readonly ILogger<VentaAutorizadaEventHandler> _logger;

    public VentaAutorizadaEventHandler(
        IInventarioStockRepository inventario,
        ILogger<VentaAutorizadaEventHandler> logger)
    {
        _inventario = inventario;
        _logger     = logger;
    }

    public async Task Handle(VentaAutorizadaEvent notification, CancellationToken ct)
    {
        var tenantId = notification.TenantId;
        var userId   = notification.UserId;
        var bodegaId = notification.BodegaId;

        foreach (var line in notification.StockLines)
        {
            var stock = await _inventario.GetStockByTenantBodegaProductAsync(
                tenantId, bodegaId, line.ProductoId, ct);

            if (stock is null)
            {
                _logger.LogWarning(
                    "Venta {FacturaId}: sin stock registrado para producto {ProductoId} en bodega {BodegaId}; se omite descuento.",
                    notification.VentaFacturaId, line.ProductoId, bodegaId);
                continue;
            }

            var cantidadAnterior = stock.Cantidad;
            var costoPromedioVenta = stock.CostoPromedioActual;
            stock.AplicarMovimiento(-line.Cantidad, userId, costoPromedioVenta);

            var movimiento = InventarioMovimiento.Create(
                tenantId,
                line.ProductoId,
                bodegaId,
                TipoMovimientoInventario.SalidaVenta,
                cantidad:            -line.Cantidad,
                cantidadAnterior:    cantidadAnterior,
                referencia:          notification.NumeroFactura,
                documentoOrigenId:   notification.VentaFacturaId,
                documentoOrigenTipo: "VentasFactura",
                createdBy:           userId,
                costoUnitario:       costoPromedioVenta);

            await _inventario.AddMovimientoAsync(movimiento, ct);
        }
    }
}
