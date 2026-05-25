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

    private readonly IStockRepository _inventario;
    private readonly IKardexSnapshotRepository _snapshots;
    private readonly IProductRepository _productos;
    private readonly IWarehouseRepository _bodegas;
    private readonly ICurrentSubscriber _subscriber;
    private readonly KardexOptions _opts;
    private readonly IKardexMaterializedDailySummariesReader _mvReader;

    public KardexService(
        IStockRepository inventario,
        IKardexSnapshotRepository snapshots,
        IProductRepository productos,
        IWarehouseRepository bodegas,
        ICurrentSubscriber subscriber,
        IOptions<KardexOptions> options,
        IKardexMaterializedDailySummariesReader mvReader)
    {
        _inventario = inventario;
        _snapshots  = snapshots;
        _productos  = productos;
        _bodegas    = bodegas;
        _subscriber = subscriber;
        _opts       = options?.Value ?? new KardexOptions();
        _mvReader   = mvReader;
    }

    public Task<Result<KardexResponse>> GenerarKardexEscalableAsync(
        GetKardexQuery query, CancellationToken cancellationToken = default)
        => GenerarInternalAsync(_subscriber.SubscriberId, query, cancellationToken);

    public Task<Result<KardexResponse>> GenerarKardexEscalableAsync(
        Guid subscriberId, GetKardexQuery query, CancellationToken cancellationToken = default)
        => GenerarInternalAsync(subscriberId, query, cancellationToken);

    private async Task<Result<KardexResponse>> GenerarInternalAsync(
        Guid subscriberId, GetKardexQuery query, CancellationToken ct)
    {
        var producto = await _productos.GetByIdAsync(query.ProductId, subscriberId, ct);
        if (producto is null)
            return Result<KardexResponse>.Failure("Producto no encontrado.");

        var Warehouse = await _bodegas.GetByIdAsync(subscriberId, query.WarehouseId, ct);
        if (Warehouse is null)
            return Result<KardexResponse>.Failure("Warehouse no encontrada.");

        DateTime? fromUtc = query.DateFrom.HasValue
            ? DateTime.SpecifyKind(query.DateFrom.Value.Date, DateTimeKind.Utc)
            : null;
        DateTime? toUtc = query.DateTo.HasValue
            ? DateTime.SpecifyKind(query.DateTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc)
            : null;

        decimal balanceQuantity = 0m;
        decimal balanceValue    = 0m;
        decimal averageCost = 0m;

        if (fromUtc.HasValue)
        {
            var anteriorAlPeriodo = fromUtc.Value.AddTicks(-1);
            var snapshot = _opts.UseScalableMode
                ? await _snapshots.GetLatestBeforeAsync(
                    subscriberId, query.ProductId, query.WarehouseId, anteriorAlPeriodo, ct)
                : null;

            if (snapshot is not null)
            {
                balanceQuantity = snapshot.BalanceQty;
                balanceValue    = snapshot.BalanceValue;
                averageCost = snapshot.AverageCost;

                var gapDesde = snapshot.SnapshotDate.AddDays(1);
                if (gapDesde < fromUtc.Value)
                {
                    (balanceQuantity, balanceValue, averageCost) = await AplicarHuecoSnapshotAsync(
                        subscriberId, query.ProductId, query.WarehouseId,
                        gapDesde, anteriorAlPeriodo,
                        balanceQuantity, balanceValue, averageCost, ct);
                }
            }
            else
            {
                var previos = await _inventario.GetMovementsAsync(
                    subscriberId, query.ProductId, query.WarehouseId,
                    null, anteriorAlPeriodo, ct);

                foreach (var m in previos)
                    KardexCalculator.ApplyMovement(m, ref balanceQuantity, ref balanceValue, ref averageCost);
            }
        }

        var inventarioInicialCantidad = balanceQuantity;
        var inventarioInicialValor    = balanceValue;

        var movimientos = await _inventario.GetMovementsAsync(
            subscriberId, query.ProductId, query.WarehouseId, fromUtc, toUtc, ct);

        var rows = new List<KardexMovementDto>(movimientos.Count);

        foreach (var m in movimientos)
        {
            decimal entradaCant = 0m, entradaValor = 0m;
            decimal salidaCant  = 0m, salidaValor  = 0m;

            if (m.Quantity > 0)
            {
                entradaCant  = m.Quantity;
                var costoEntrada = m.UnitCost ?? 0m;
                entradaValor = entradaCant * costoEntrada;

                balanceValue    += entradaValor;
                balanceQuantity += entradaCant;
                averageCost  = balanceQuantity > 0m ? balanceValue / balanceQuantity : 0m;
            }
            else
            {
                salidaCant  = -m.Quantity;
                salidaValor = salidaCant * averageCost;

                balanceQuantity -= salidaCant;
                balanceValue    -= salidaValor;
                if (balanceValue < 0m) balanceValue = 0m;
            }

            rows.Add(new KardexMovementDto(
                Fecha:                m.CreatedAt,
                MovementType:  DescripcionTipo(m.MovementType.ToString()),
                Referencia:           m.Reference,
                InboundQuantity:      entradaCant,
                InboundValue:         Math.Round(entradaValor, 6),
                OutboundQuantity:       salidaCant,
                OutboundValue:          Math.Round(salidaValor, 6),
                BalanceQuantity:        balanceQuantity,
                BalanceValue:           Math.Round(balanceValue, 6),
                AverageUnitCost: Math.Round(averageCost, 6)));
        }

        var resumen = new KardexSummaryDto(
            OpeningQuantity: inventarioInicialCantidad,
            OpeningValue:    Math.Round(inventarioInicialValor, 6),
            TotalInboundQuantity:          rows.Sum(r => r.InboundQuantity),
            TotalInboundValue:             Math.Round(rows.Sum(r => r.InboundValue), 6),
            TotalOutboundQuantity:           rows.Sum(r => r.OutboundQuantity),
            TotalOutboundValue:              Math.Round(rows.Sum(r => r.OutboundValue), 6),
            ClosingQuantity:   balanceQuantity,
            ClosingValue:      Math.Round(balanceValue, 6),
            FinalAverageCost:        Math.Round(averageCost, 6));

        return Result<KardexResponse>.Success(new KardexResponse(
            new KardexProductDto(producto.Id, producto.ShortName, producto.SaleCode),
            new KardexWarehouseDto(Warehouse.Id, Warehouse.Name),
            rows,
            resumen));
    }

    /// <summary>
    /// Aplica movimientos entre el día posterior al snapshot y el instante previo al inicio del período.
    /// Con <see cref="KardexOptions.UseMaterializedDailySummaries"/>, los días UTC completos intermedios
    /// se resumen vía <c>mv_saldos_diarios</c> (entradas del día primero, salidas después: aproximación
    /// frente al orden real intra-día).
    /// </summary>
    private async Task<(decimal balanceQuantity, decimal balanceValue, decimal averageCost)> AplicarHuecoSnapshotAsync(
        Guid subscriberId, Guid productoId, Guid bodegaId,
        DateTime gapDesdeUtc, DateTime gapHastaUtc,
        decimal balanceQuantity, decimal balanceValue, decimal averageCost,
        CancellationToken ct)
    {
        var d0 = gapDesdeUtc.Date;
        var d1 = gapHastaUtc.Date;

        if (d0 > d1)
            return (balanceQuantity, balanceValue, averageCost);

        if (d0 == d1)
        {
            return await AplicarMovimientosRangoAsync(
                subscriberId, productoId, bodegaId, gapDesdeUtc, gapHastaUtc,
                balanceQuantity, balanceValue, averageCost, ct);
        }

        var endFirstDay = EndOfUtcDay(gapDesdeUtc);
        var endA        = gapHastaUtc < endFirstDay ? gapHastaUtc : endFirstDay;
        if (gapDesdeUtc <= endA)
        {
            (balanceQuantity, balanceValue, averageCost) = await AplicarMovimientosRangoAsync(
                subscriberId, productoId, bodegaId, gapDesdeUtc, endA,
                balanceQuantity, balanceValue, averageCost, ct);
        }

        var firstFullDay = DateOnly.FromDateTime(d0.AddDays(1));
        var lastFullDay  = DateOnly.FromDateTime(d1.AddDays(-1));

        if (firstFullDay <= lastFullDay)
        {
            if (_opts.UseMaterializedDailySummaries)
            {
                var mvRows = await _mvReader.TryGetDailyAggregatesAsync(
                    subscriberId, productoId, bodegaId, firstFullDay, lastFullDay, ct);

                if (mvRows is { Count: > 0 })
                {
                    foreach (var row in mvRows.OrderBy(r => r.Date))
                    {
                        (balanceQuantity, balanceValue, averageCost) = AplicarAgregadoMvDia(
                            row, subscriberId, productoId, bodegaId,
                            balanceQuantity, balanceValue, averageCost);
                    }
                }
                else
                {
                    var fullStart = StartOfUtcDayFromDateOnly(firstFullDay);
                    var fullEnd   = EndOfUtcDayFromDateOnly(lastFullDay);
                    (balanceQuantity, balanceValue, averageCost) = await AplicarMovimientosRangoAsync(
                        subscriberId, productoId, bodegaId, fullStart, fullEnd,
                        balanceQuantity, balanceValue, averageCost, ct);
                }
            }
            else
            {
                var fullStart = StartOfUtcDayFromDateOnly(firstFullDay);
                var fullEnd   = EndOfUtcDayFromDateOnly(lastFullDay);
                (balanceQuantity, balanceValue, averageCost) = await AplicarMovimientosRangoAsync(
                    subscriberId, productoId, bodegaId, fullStart, fullEnd,
                    balanceQuantity, balanceValue, averageCost, ct);
            }
        }

        var startLastDay = d1;
        if (gapHastaUtc >= startLastDay && d0 != d1)
        {
            (balanceQuantity, balanceValue, averageCost) = await AplicarMovimientosRangoAsync(
                subscriberId, productoId, bodegaId, startLastDay, gapHastaUtc,
                balanceQuantity, balanceValue, averageCost, ct);
        }

        return (balanceQuantity, balanceValue, averageCost);
    }

    private static DateTime EndOfUtcDay(DateTime anyUtc)
        => anyUtc.Date.AddDays(1).AddTicks(-1);

    private static DateTime StartOfUtcDayFromDateOnly(DateOnly d)
        => DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static DateTime EndOfUtcDayFromDateOnly(DateOnly d)
        => DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

    private async Task<(decimal balanceQuantity, decimal balanceValue, decimal averageCost)> AplicarMovimientosRangoAsync(
        Guid subscriberId, Guid productoId, Guid bodegaId,
        DateTime fromUtc, DateTime toUtc,
        decimal balanceQuantity, decimal balanceValue, decimal averageCost,
        CancellationToken ct)
    {
        var movs = await _inventario.GetMovementsAsync(
            subscriberId, productoId, bodegaId, fromUtc, toUtc, ct);
        foreach (var m in movs)
            KardexCalculator.ApplyMovement(m, ref balanceQuantity, ref balanceValue, ref averageCost);

        return (balanceQuantity, balanceValue, averageCost);
    }

    private static (decimal balanceQuantity, decimal balanceValue, decimal averageCost) AplicarAgregadoMvDia(
        KardexMvDayAggregate row,
        Guid subscriberId, Guid productoId, Guid bodegaId,
        decimal balanceQuantity, decimal balanceValue, decimal averageCost)
    {
        if (row.EntryQty > 0m)
        {
            var costoUnit = row.EntryValue / row.EntryQty;
            var m = StockMovement.Create(
                subscriberId, productoId, bodegaId,
                StockMovementType.PurchaseEntry,
                row.EntryQty,
                balanceQuantity,
                $"MV día {row.Date:O}",
                null, null,
                SyntheticUserId,
                costoUnit);
            KardexCalculator.ApplyMovement(m, ref balanceQuantity, ref balanceValue, ref averageCost);
        }

        if (row.ExitQty > 0m)
        {
            var m = StockMovement.Create(
                subscriberId, productoId, bodegaId,
                StockMovementType.SaleExit,
                -row.ExitQty,
                balanceQuantity,
                $"MV día {row.Date:O}",
                null, null,
                SyntheticUserId,
                null);
            KardexCalculator.ApplyMovement(m, ref balanceQuantity, ref balanceValue, ref averageCost);
        }

        return (balanceQuantity, balanceValue, averageCost);
    }

    private static string DescripcionTipo(string tipo) => tipo switch
    {
        "PurchaseEntry"        => "Compra",
        "SaleExit"             => "Venta",
        "PositiveAdjust"       => "Ajuste (+)",
        "NegativeAdjust"       => "Ajuste (-)",
        "TransferEntry"        => "transfer entrada",
        "TransferExit"         => "transfer salida",
        "PurchaseReturn"       => "Devolución compra",
        "SaleReturn"           => "Devolución venta",
        "SupplierCreditNote"   => "Nota crédito proveedor",
        "SupplierDebitNote"    => "Nota débito proveedor",
        "EntradaCompra"        => "Compra",
        "SalidaVenta"          => "Venta",
        "AjustePositivo"       => "Ajuste (+)",
        "AjusteNegativo"       => "Ajuste (-)",
        "TransferenciaEntrada" => "transfer entrada",
        "TransferenciaSalida"  => "transfer salida",
        "DevolucionCompra"     => "Devolución compra",
        "DevolucionVenta"      => "Devolución venta",
        _                      => tipo,
    };
}

