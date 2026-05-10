using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventario.DTOs;
using ERP.Domain.Bodegas.Interfaces;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Interfaces;
using ERP.Domain.Products.Interfaces;
using ERP.Application.Inventario;

namespace ERP.Application.Inventario.UseCases.GetKardex;

public sealed class GetKardexQueryHandler
    : IRequestHandler<GetKardexQuery, Result<KardexResponse>>
{
    private readonly IInventarioStockRepository _inventario;
    private readonly IKardexSnapshotRepository  _snapshots;
    private readonly IProductRepository         _productos;
    private readonly IBodegaRepository          _bodegas;
    private readonly ICurrentTenant             _tenant;

    public GetKardexQueryHandler(
        IInventarioStockRepository inventario,
        IKardexSnapshotRepository  snapshots,
        IProductRepository         productos,
        IBodegaRepository          bodegas,
        ICurrentTenant             tenant)
    {
        _inventario = inventario;
        _snapshots  = snapshots;
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

        // Normalizar fechas a UTC
        DateTime? desdeUtc = query.FechaInicio.HasValue
            ? DateTime.SpecifyKind(query.FechaInicio.Value.Date, DateTimeKind.Utc)
            : null;
        DateTime? hastaUtc = query.FechaFin.HasValue
            ? DateTime.SpecifyKind(query.FechaFin.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc)
            : null;

        // ── SALDO INICIAL ─────────────────────────────────────────────────────
        // Estrategia: si hay filtro de fecha, intenta usar un snapshot como punto de partida.
        // El snapshot evita recorrer todo el historial anterior al período (O(1) vs O(n)).
        // Si no existe snapshot → fallback al cálculo completo (comportamiento original).

        decimal saldoCantidad = 0m;
        decimal saldoValor    = 0m;
        decimal costoPromedio = 0m;
        DateTime? movimientosDesde = null; // null = desde el origen del tiempo

        if (desdeUtc.HasValue)
        {
            var anteriorAlPeriodo = desdeUtc.Value.AddTicks(-1);

            var snapshot = await _snapshots.GetLatestBeforeAsync(
                tenantId, query.ProductoId, query.BodegaId, anteriorAlPeriodo, ct);

            if (snapshot is not null)
            {
                // Usar snapshot como base; solo hay que calcular el "gap" entre el snapshot
                // y el inicio del período (en caso de que el snapshot sea de días anteriores).
                saldoCantidad = snapshot.CantidadSaldo;
                saldoValor    = snapshot.ValorSaldo;
                costoPromedio = snapshot.CostoPromedio;

                var gapDesde = snapshot.FechaSnapshot.AddDays(1);
                if (gapDesde < desdeUtc.Value)
                {
                    // Hay movimientos entre el snapshot y el inicio del período que no están cubiertos
                    var movGap = await _inventario.GetMovimientosAsync(
                        tenantId, query.ProductoId, query.BodegaId,
                        gapDesde, anteriorAlPeriodo, ct);

                    foreach (var m in movGap)
                        KardexCalculator.AplicarMovimiento(m, ref saldoCantidad, ref saldoValor, ref costoPromedio);
                }
                // No hay gap: el snapshot ya cubre hasta el día previo → saldos listos

                movimientosDesde = null; // movimientos del período se leen con desdeUtc (abajo)
            }
            else
            {
                // Fallback: sin snapshot, recorrer todo el historial previo al período
                var previos = await _inventario.GetMovimientosAsync(
                    tenantId, query.ProductoId, query.BodegaId,
                    null, anteriorAlPeriodo, ct);

                foreach (var m in previos)
                    KardexCalculator.AplicarMovimiento(m, ref saldoCantidad, ref saldoValor, ref costoPromedio);
            }
        }

        var inventarioInicialCantidad = saldoCantidad;
        var inventarioInicialValor    = saldoValor;

        // ── MOVIMIENTOS DEL PERÍODO ───────────────────────────────────────────
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
                if (saldoValor < 0m) saldoValor = 0m;
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
