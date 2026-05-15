using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.EventHandlers;

public sealed class CompraAprobadaEventHandler : INotificationHandler<CompraAprobadaEvent>
{
    private readonly IInventarioStockRepository _inventario;
    private readonly ILogger<CompraAprobadaEventHandler> _logger;

    public CompraAprobadaEventHandler(
        IInventarioStockRepository inventario,
        ILogger<CompraAprobadaEventHandler> logger)
    {
        _inventario = inventario;
        _logger     = logger;
    }

    public async Task Handle(CompraAprobadaEvent notification, CancellationToken ct)
    {
        var tenantId = notification.TenantId;
        var userId   = notification.ApprovedByUserId;

        foreach (var line in notification.StockLines)
        {
            if (!line.ProductoId.HasValue)
            {
                _logger.LogWarning(
                    "Compra {CompraId}: línea sin producto; no se actualiza inventario (detalle {DetalleId}, bodega {BodegaId}).",
                    notification.CompraFacturaId, line.CompraDetalleId, line.BodegaId);
                continue;
            }

            var productoId = line.ProductoId.Value;

            var stock = await _inventario.GetStockByTenantBodegaProductAsync(
                tenantId, line.BodegaId, productoId, ct);
            if (stock is null)
            {
                stock = StockActual.Create(tenantId, productoId, line.BodegaId, userId);
                await _inventario.AddStockActualAsync(stock, ct);
            }

            var cantidadAnterior = stock.Cantidad;
            stock.AplicarMovimiento(line.Cantidad, userId, line.CostoUnitarioNeto);

            var movimiento = InventarioMovimiento.Create(
                tenantId,
                productoId,
                line.BodegaId,
                TipoMovimientoInventario.EntradaCompra,
                cantidad:            line.Cantidad,
                cantidadAnterior:    cantidadAnterior,
                referencia:          notification.NumeroFactura,
                documentoOrigenId:   notification.CompraFacturaId,
                documentoOrigenTipo: "CompraFactura",
                createdBy:           userId,
                costoUnitario:       line.CostoUnitarioNeto);

            await _inventario.AddMovimientoAsync(movimiento, ct);
        }
    }
}
