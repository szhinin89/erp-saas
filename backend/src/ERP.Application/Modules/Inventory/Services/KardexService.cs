using Microsoft.Extensions.Options;
using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventory;
using ERP.Application.Inventory.DTOs;
using ERP.Application.Inventory.UseCases.GetKardex;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Modules.Inventory.Services;

/// <summary>
/// Implementación del kardex valorizado; el nombre <see cref="GenerarKardexEscalableAsync"/> refleja
/// el uso de <see cref="KardexOptions.UseScalableMode"/> y extensiones opcionales (MV diaria).
/// </summary>
public sealed class KardexService : IKardexService
{
    private static readonly Guid SyntheticUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IInventarioStockRepository _inventario;
    private readonly IKardexSnapshotRepository _snapshots;
    private readonly IProductRepository _productos;
    private readonly IBodegaRepository _bodegas;
    private readonly ICurrentTenant _tenant;
    private readonly KardexOptions _opts;
    private readonly IKardexMaterializedDailySummariesReader _mvReader;

    public KardexService(
        IInventarioStockRepository inventario,
        IKardexSnapshotRepository snapshots,
        IProductRepository productos,
        IBodegaRepository bodegas,
        ICurrentTenant tenant,
        IOptions<KardexOptions> options,
        IKardexMaterializedDailySummariesReader mvReader)
    {
        _inventario = inventario;
        _snapshots  = snapshots;
        _productos  = productos;
        _bodegas    = bodegas;
        _tenant     = tenant;
        _opts       = options?.Value ?? new KardexOptions();
        _mvReader   = mvReader;
    }

    public Task<Result<KardexResponse>> GenerarKardexEscalableAsync(
        GetKardexQuery query, CancellationToken cancellationToken = default)
        => GenerarInternalAsync(_tenant.TenantId, query, cancellationToken);

    public Task<Result<KardexResponse>> GenerarKardexEscalableAsync(
        Guid tenantId, GetKardexQuery query, CancellationToken cancellationToken = default)
        => GenerarInternalAsync(tenantId, query, cancellationToken);

    private async Task<Result<KardexResponse>> GenerarInternalAsync(
        Guid tenantId, GetKardexQuery query, CancellationToken ct)
    {
        var producto = await _productos.GetByIdAsync(query.ProductoId, tenantId, ct);
        if (producto is null)
            return Result<KardexResponse>.Failure("Producto no encontrado.");

        var bodega = await _bodegas.GetByIdAsync(tenantId, query.BodegaId, ct);
        if (bodega is null)
            return Result<KardexResponse>.Failure("Bodega no encontrada.");

        DateTime? desdeUtc = query.FechaInicio.HasValue
            ? DateTime.SpecifyKind(query.FechaInicio.Value.Date, DateTimeKind.Utc)
            : null;
        DateTime? hastaUtc = query.FechaFin.HasValue
            ? DateTime.SpecifyKind(query.FechaFin.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc)
            : null;

        decimal saldoCantidad = 0m;
        decimal saldoValor    = 0m;
        decimal costoPromedio = 0m;

        if (desdeUtc.HasValue)
        {
            var anteriorAlPeriodo = desdeUtc.Value.AddTicks(-1);
            var snapshot = _opts.UseScalableMode
                ? await _snapshots.GetLatestBeforeAsync(
                    tenantId, query.ProductoId, query.BodegaId, anteriorAlPeriodo, ct)
                : null;

            if (snapshot is not null)
            {
                saldoCantidad = snapshot.CantidadSaldo;
                saldoValor    = snapshot.ValorSaldo;
                costoPromedio = snapshot.CostoPromedio;

                var gapDesde = snapshot.FechaSnapshot.AddDays(1);
                if (gapDesde < desdeUtc.Value)
                {
                    (saldoCantidad, saldoValor, costoPromedio) = await AplicarHuecoSnapshotAsync(
                        tenantId, query.ProductoId, query.BodegaId,
                        gapDesde, anteriorAlPeriodo,
                        saldoCantidad, saldoValor, costoPromedio, ct);
                }
            }
            else
            {
                var previos = await _inventario.GetMovimientosAsync(
                    tenantId, query.ProductoId, query.BodegaId,
                    null, anteriorAlPeriodo, ct);

                foreach (var m in previos)
                    KardexCalculator.AplicarMovimiento(m, ref saldoCantidad, ref saldoValor, ref costoPromedio);
            }
        }

        var inventarioInicialCantidad = saldoCantidad;
        var inventarioInicialValor    = saldoValor;

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

    /// <summary>
    /// Aplica movimientos entre el día posterior al snapshot y el instante previo al inicio del período.
    /// Con <see cref="KardexOptions.UseMaterializedDailySummaries"/>, los días UTC completos intermedios
    /// se resumen vía <c>mv_saldos_diarios</c> (entradas del día primero, salidas después: aproximación
    /// frente al orden real intra-día).
    /// </summary>
    private async Task<(decimal saldoCantidad, decimal saldoValor, decimal costoPromedio)> AplicarHuecoSnapshotAsync(
        Guid tenantId, Guid productoId, Guid bodegaId,
        DateTime gapDesdeUtc, DateTime gapHastaUtc,
        decimal saldoCantidad, decimal saldoValor, decimal costoPromedio,
        CancellationToken ct)
    {
        var d0 = gapDesdeUtc.Date;
        var d1 = gapHastaUtc.Date;

        if (d0 > d1)
            return (saldoCantidad, saldoValor, costoPromedio);

        if (d0 == d1)
        {
            return await AplicarMovimientosRangoAsync(
                tenantId, productoId, bodegaId, gapDesdeUtc, gapHastaUtc,
                saldoCantidad, saldoValor, costoPromedio, ct);
        }

        var endFirstDay = EndOfUtcDay(gapDesdeUtc);
        var endA        = gapHastaUtc < endFirstDay ? gapHastaUtc : endFirstDay;
        if (gapDesdeUtc <= endA)
        {
            (saldoCantidad, saldoValor, costoPromedio) = await AplicarMovimientosRangoAsync(
                tenantId, productoId, bodegaId, gapDesdeUtc, endA,
                saldoCantidad, saldoValor, costoPromedio, ct);
        }

        var firstFullDay = DateOnly.FromDateTime(d0.AddDays(1));
        var lastFullDay  = DateOnly.FromDateTime(d1.AddDays(-1));

        if (firstFullDay <= lastFullDay)
        {
            if (_opts.UseMaterializedDailySummaries)
            {
                var mvRows = await _mvReader.TryGetDailyAggregatesAsync(
                    tenantId, productoId, bodegaId, firstFullDay, lastFullDay, ct);

                if (mvRows is { Count: > 0 })
                {
                    foreach (var row in mvRows.OrderBy(r => r.Fecha))
                    {
                        (saldoCantidad, saldoValor, costoPromedio) = AplicarAgregadoMvDia(
                            row, tenantId, productoId, bodegaId,
                            saldoCantidad, saldoValor, costoPromedio);
                    }
                }
                else
                {
                    var fullStart = StartOfUtcDayFromDateOnly(firstFullDay);
                    var fullEnd   = EndOfUtcDayFromDateOnly(lastFullDay);
                    (saldoCantidad, saldoValor, costoPromedio) = await AplicarMovimientosRangoAsync(
                        tenantId, productoId, bodegaId, fullStart, fullEnd,
                        saldoCantidad, saldoValor, costoPromedio, ct);
                }
            }
            else
            {
                var fullStart = StartOfUtcDayFromDateOnly(firstFullDay);
                var fullEnd   = EndOfUtcDayFromDateOnly(lastFullDay);
                (saldoCantidad, saldoValor, costoPromedio) = await AplicarMovimientosRangoAsync(
                    tenantId, productoId, bodegaId, fullStart, fullEnd,
                    saldoCantidad, saldoValor, costoPromedio, ct);
            }
        }

        var startLastDay = d1;
        if (gapHastaUtc >= startLastDay && d0 != d1)
        {
            (saldoCantidad, saldoValor, costoPromedio) = await AplicarMovimientosRangoAsync(
                tenantId, productoId, bodegaId, startLastDay, gapHastaUtc,
                saldoCantidad, saldoValor, costoPromedio, ct);
        }

        return (saldoCantidad, saldoValor, costoPromedio);
    }

    private static DateTime EndOfUtcDay(DateTime anyUtc)
        => anyUtc.Date.AddDays(1).AddTicks(-1);

    private static DateTime StartOfUtcDayFromDateOnly(DateOnly d)
        => DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static DateTime EndOfUtcDayFromDateOnly(DateOnly d)
        => DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

    private async Task<(decimal saldoCantidad, decimal saldoValor, decimal costoPromedio)> AplicarMovimientosRangoAsync(
        Guid tenantId, Guid productoId, Guid bodegaId,
        DateTime desdeUtc, DateTime hastaUtc,
        decimal saldoCantidad, decimal saldoValor, decimal costoPromedio,
        CancellationToken ct)
    {
        var movs = await _inventario.GetMovimientosAsync(
            tenantId, productoId, bodegaId, desdeUtc, hastaUtc, ct);
        foreach (var m in movs)
            KardexCalculator.AplicarMovimiento(m, ref saldoCantidad, ref saldoValor, ref costoPromedio);

        return (saldoCantidad, saldoValor, costoPromedio);
    }

    private static (decimal saldoCantidad, decimal saldoValor, decimal costoPromedio) AplicarAgregadoMvDia(
        KardexMvDayAggregate row,
        Guid tenantId, Guid productoId, Guid bodegaId,
        decimal saldoCantidad, decimal saldoValor, decimal costoPromedio)
    {
        if (row.EntradasCantidad > 0m)
        {
            var costoUnit = row.EntradasValor / row.EntradasCantidad;
            var m = InventarioMovimiento.Create(
                tenantId, productoId, bodegaId,
                TipoMovimientoInventario.EntradaCompra,
                row.EntradasCantidad,
                saldoCantidad,
                $"MV día {row.Fecha:O}",
                null, null,
                SyntheticUserId,
                costoUnit);
            KardexCalculator.AplicarMovimiento(m, ref saldoCantidad, ref saldoValor, ref costoPromedio);
        }

        if (row.SalidasCantidad > 0m)
        {
            var m = InventarioMovimiento.Create(
                tenantId, productoId, bodegaId,
                TipoMovimientoInventario.SalidaVenta,
                -row.SalidasCantidad,
                saldoCantidad,
                $"MV día {row.Fecha:O}",
                null, null,
                SyntheticUserId,
                null);
            KardexCalculator.AplicarMovimiento(m, ref saldoCantidad, ref saldoValor, ref costoPromedio);
        }

        return (saldoCantidad, saldoValor, costoPromedio);
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
