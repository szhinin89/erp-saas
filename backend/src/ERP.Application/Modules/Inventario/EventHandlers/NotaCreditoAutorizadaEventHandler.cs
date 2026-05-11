using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Enums;
using ERP.Domain.Modules.Inventario.Interfaces;
using ERP.Domain.Modules.Ventas.Events;

namespace ERP.Application.Modules.Inventario.EventHandlers;

public sealed class NotaCreditoAutorizadaEventHandler : INotificationHandler<NotaCreditoAutorizadaEvent>
{
    private readonly IInventarioStockRepository _inventario;
    private readonly ILogger<NotaCreditoAutorizadaEventHandler> _logger;

    public NotaCreditoAutorizadaEventHandler(
        IInventarioStockRepository inventario,
        ILogger<NotaCreditoAutorizadaEventHandler> logger)
    {
        _inventario = inventario;
        _logger     = logger;
    }

    public async Task Handle(NotaCreditoAutorizadaEvent notification, CancellationToken ct)
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
                    "Nota crédito {NotaId}: sin stock para producto {ProductoId} en bodega {BodegaId}; se omite reingreso.",
                    notification.NotaId, line.ProductoId, bodegaId);
                continue;
            }

            var cantidadAnterior = stock.Cantidad;
            var costo            = stock.CostoPromedioActual;
            stock.AplicarMovimiento(line.Cantidad, userId, costo);

            var movimiento = InventarioMovimiento.Create(
                tenantId,
                line.ProductoId,
                bodegaId,
                TipoMovimientoInventario.DevolucionVenta,
                cantidad:            line.Cantidad,
                cantidadAnterior:    cantidadAnterior,
                referencia:          notification.NumeroNota,
                documentoOrigenId:   notification.NotaId,
                documentoOrigenTipo: "VentasNotaCreditoDebito",
                createdBy:           userId,
                costoUnitario:       costo);

            await _inventario.AddMovimientoAsync(movimiento, ct);
        }
    }
}
