using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventario.DTOs;
using ERP.Domain.Bodegas.Interfaces;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Inventario.UseCases.GetKardex;

public sealed class GetKardexQueryHandler
    : IRequestHandler<GetKardexQuery, Result<KardexResponse>>
{
    private readonly IInventarioStockRepository _inventario;
    private readonly IProductRepository         _productos;
    private readonly IBodegaRepository          _bodegas;
    private readonly ICurrentTenant             _tenant;

    public GetKardexQueryHandler(
        IInventarioStockRepository inventario,
        IProductRepository         productos,
        IBodegaRepository          bodegas,
        ICurrentTenant             tenant)
    {
        _inventario = inventario;
        _productos  = productos;
        _bodegas    = bodegas;
        _tenant     = tenant;
    }

    public async Task<Result<KardexResponse>> Handle(
        GetKardexQuery query, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;

        var producto = await _productos.GetByIdAsync(query.ProductoId, tenantId, ct);
        if (producto is null)
            return Result<KardexResponse>.Failure("Producto no encontrado.");

        var bodega = await _bodegas.GetByIdAsync(tenantId, query.BodegaId, ct);
        if (bodega is null)
            return Result<KardexResponse>.Failure("Bodega no encontrada.");

        // Normalizar fechas a UTC. FechaFin incluye todo el día.
        DateTime? desdeUtc = query.FechaInicio.HasValue
            ? DateTime.SpecifyKind(query.FechaInicio.Value.Date, DateTimeKind.Utc)
            : null;
        DateTime? hastaUtc = query.FechaFin.HasValue
            ? DateTime.SpecifyKind(query.FechaFin.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc)
            : null;

        // Saldo inicial: promedio ponderado acumulado de todos los movimientos
        // anteriores al primer día del período.
        decimal saldoCantidad = 0m;
        decimal saldoValor    = 0m;
        decimal costoPromedio = 0m;

        if (desdeUtc.HasValue)
        {
            // Hasta 1 tick antes de la medianoche de FechaInicio
            var antesDeInicio = desdeUtc.Value.AddTicks(-1);
            var previos = await _inventario.GetMovimientosAsync(
                tenantId, query.ProductoId, query.BodegaId, null, antesDeInicio, ct);

            foreach (var m in previos)
                AplicarMovimiento(m, ref saldoCantidad, ref saldoValor, ref costoPromedio);
        }

        var inventarioInicialCantidad = saldoCantidad;
        var inventarioInicialValor    = saldoValor;

        // Movimientos del período
        var movimientos = await _inventario.GetMovimientosAsync(
            tenantId, query.ProductoId, query.BodegaId, desdeUtc, hastaUtc, ct);

        var rows = new List<MovimientoKardexDto>(movimientos.Count);

        foreach (var m in movimientos)
        {
            decimal entradaCant = 0m, entradaValor = 0m;
            decimal salidaCant  = 0m, salidaValor  = 0m;

            if (m.Cantidad > 0)
            {
                entradaCant  = m.Cantidad;
                var costoEntrada = m.CostoUnitario ?? 0m;
                entradaValor = entradaCant * costoEntrada;

                saldoValor    += entradaValor;
                saldoCantidad += entradaCant;
                costoPromedio  = saldoCantidad > 0m ? saldoValor / saldoCantidad : 0m;
            }
            else
            {
                salidaCant  = -m.Cantidad;
                salidaValor = salidaCant * costoPromedio;

                saldoCantidad -= salidaCant;
                saldoValor    -= salidaValor;
                if (saldoValor < 0m) saldoValor = 0m; // guardia contra redondeo
            }

            rows.Add(new MovimientoKardexDto(
                Fecha:                m.CreatedAt,
                TipoMovimiento:       DescripcionTipo(m.TipoMovimiento.ToString()),
                Referencia:           m.Referencia,
                EntradaCantidad:      entradaCant,
                EntradaValor:         Math.Round(entradaValor, 6),
                SalidaCantidad:       salidaCant,
                SalidaValor:          Math.Round(salidaValor, 6),
                SaldoCantidad:        saldoCantidad,
                SaldoValor:           Math.Round(saldoValor, 6),
                CostoUnitarioPromedio: Math.Round(costoPromedio, 6)));
        }

        var resumen = new ResumenKardexDto(
            InventarioInicialCantidad: inventarioInicialCantidad,
            InventarioInicialValor:    Math.Round(inventarioInicialValor, 6),
            EntradasCantidad:          rows.Sum(r => r.EntradaCantidad),
            EntradasValor:             Math.Round(rows.Sum(r => r.EntradaValor), 6),
            SalidasCantidad:           rows.Sum(r => r.SalidaCantidad),
            SalidasValor:              Math.Round(rows.Sum(r => r.SalidaValor), 6),
            InventarioFinalCantidad:   saldoCantidad,
            InventarioFinalValor:      Math.Round(saldoValor, 6),
            CostoPromedioFinal:        Math.Round(costoPromedio, 6));

        return Result<KardexResponse>.Success(new KardexResponse(
            new KardexProductoDto(producto.Id, producto.ShortName, producto.SaleCode),
            new KardexBodegaDto(bodega.Id, bodega.Nombre),
            rows,
            resumen));
    }

    // Calcula el saldo acumulado (sin emitir filas) para los movimientos previos al período.
    private static void AplicarMovimiento(
        InventarioMovimiento m,
        ref decimal          saldoCantidad,
        ref decimal          saldoValor,
        ref decimal          costoPromedio)
    {
        if (m.Cantidad > 0)
        {
            var costoEntrada = m.CostoUnitario ?? 0m;
            saldoValor    += m.Cantidad * costoEntrada;
            saldoCantidad += m.Cantidad;
            costoPromedio  = saldoCantidad > 0m ? saldoValor / saldoCantidad : 0m;
        }
        else
        {
            var salida = -m.Cantidad;
            saldoValor    -= salida * costoPromedio;
            saldoCantidad -= salida;
            if (saldoValor < 0m) saldoValor = 0m;
        }
    }

    private static string DescripcionTipo(string tipo) => tipo switch
    {
        "EntradaCompra"        => "Compra",
        "SalidaVenta"          => "Venta",
        "AjustePositivo"       => "Ajuste (+)",
        "AjusteNegativo"       => "Ajuste (-)",
        "TransferenciaEntrada" => "Transferencia entrada",
        "TransferenciaSalida"  => "Transferencia salida",
        "DevolucionCompra"     => "Devolución compra",
        "DevolucionVenta"      => "Devolución venta",
        _                      => tipo,
    };
}
