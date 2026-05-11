using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Domain.Modules.Compras.Events;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Enums;
using ERP.Domain.Modules.Inventario.Interfaces;

namespace ERP.Application.Modules.Inventario.EventHandlers;

public sealed class CompraNotaProveedorAprobadaEventHandler : INotificationHandler<CompraNotaProveedorAprobadaEvent>
{
    private readonly IInventarioStockRepository _inventario;
    private readonly ILogger<CompraNotaProveedorAprobadaEventHandler> _logger;

    public CompraNotaProveedorAprobadaEventHandler(
        IInventarioStockRepository inventario,
        ILogger<CompraNotaProveedorAprobadaEventHandler> logger)
    {
        _inventario = inventario;
        _logger     = logger;
    }

    public async Task Handle(CompraNotaProveedorAprobadaEvent notification, CancellationToken ct)
    {
        var tenantId = notification.TenantId;
        var userId   = notification.UserId;

        var tipoMov = notification.TipoNota == "CREDITO"
            ? TipoMovimientoInventario.NotaCreditoProveedor
            : TipoMovimientoInventario.NotaDebitoProveedor;

        foreach (var line in notification.StockLines)
        {
            var stock = await _inventario.GetStockByTenantBodegaProductAsync(
                tenantId, line.BodegaId, line.ProductoId, ct);
            if (stock is null)
            {
                if (line.DeltaCantidad > 0)
                {
                    stock = StockActual.Create(tenantId, line.ProductoId, line.BodegaId, userId);
                    await _inventario.AddStockActualAsync(stock, ct);
                }
                else
                {
                    _logger.LogWarning(
                        "Nota proveedor {NotaId}: no hay stock en bodega {BodegaId} producto {ProductoId} para aplicar salida.",
                        notification.NotaId, line.BodegaId, line.ProductoId);
                    continue;
                }
            }

            var cantidadAnterior = stock.Cantidad;
            var costo            = line.CostoUnitario;
            if (line.DeltaCantidad < 0 && stock.Cantidad > 0)
                costo = stock.CostoPromedioActual;

            stock.AplicarMovimiento(line.DeltaCantidad, userId, costo);

            var movimiento = InventarioMovimiento.Create(
                tenantId,
                line.ProductoId,
                line.BodegaId,
                tipoMov,
                cantidad:            line.DeltaCantidad,
                cantidadAnterior:    cantidadAnterior,
                referencia:          notification.NumeroNota,
                documentoOrigenId:   notification.NotaId,
                documentoOrigenTipo: "CompraNotaProveedor",
                createdBy:           userId,
                costoUnitario:       costo);

            await _inventario.AddMovimientoAsync(movimiento, ct);
        }
    }
}
