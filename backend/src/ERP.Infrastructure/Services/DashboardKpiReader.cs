using ERP.Application.Common;
using ERP.Application.Modules.Dashboard;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Lee los KPIs operativos del dashboard desde Ventas, Cuentas por Cobrar/Pagar e Inventario
/// reales — alcance company-wide (tenant + empresa actual), igual que el resto de reportes
/// (Ventas/Compras/Inventario/Kardex/Caja), sin filtro de sucursal.
/// </summary>
public sealed class DashboardKpiReader : IDashboardKpiReader
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public DashboardKpiReader(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    public async Task<DashboardKpisDto> ReadAsync(
        Guid tenantId,
        Guid companyId,
        DateTime asOf,
        CancellationToken cancellationToken = default
    )
    {
        var asOfDate = DateOnly.FromDateTime(asOf);
        var monthStart = new DateOnly(asOfDate.Year, asOfDate.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var yearStart = new DateOnly(asOfDate.Year, 1, 1);
        var yearEnd = new DateOnly(asOfDate.Year, 12, 31);

        var (salesMtd, invoicesMtd) = await ReadSalesTotalsAsync(
            tenantId,
            monthStart,
            monthEnd,
            cancellationToken
        );
        var salesYtd = await ReadSalesTotalAsync(tenantId, yearStart, yearEnd, cancellationToken);
        var (pendingArTotal, pendingArCount, overdueArTotal, overdueArCount) =
            await ReadReceivablesAsync(tenantId, asOfDate, cancellationToken);
        var (pendingApTotal, pendingApCount, overdueApTotal, overdueApCount) =
            await ReadPayablesAsync(tenantId, asOfDate, cancellationToken);
        var (lowStockCount, outOfStockCount) = await ReadStockAsync(tenantId, cancellationToken);

        return new DashboardKpisDto(
            SalesMtd: salesMtd,
            InvoicesMtd: invoicesMtd,
            SalesYtd: salesYtd,
            PendingArTotal: pendingArTotal,
            PendingArCount: pendingArCount,
            OverdueArTotal: overdueArTotal,
            OverdueArCount: overdueArCount,
            PendingApTotal: pendingApTotal,
            PendingApCount: pendingApCount,
            OverdueApTotal: overdueApTotal,
            OverdueApCount: overdueApCount,
            LowStockSkuCount: lowStockCount,
            OutOfStockSkuCount: outOfStockCount,
            AsOf: asOf,
            Month: asOfDate.Month,
            Year: asOfDate.Year
        );
    }

    /// <summary>
    /// Ventas del mes — solo facturas Authorized (Draft/Cancelled nunca suman ingresos, mismo
    /// criterio que <c>GetDailySalesReportQueryHandler</c>, ERP-CORE-CLOSEOUT-08). Se usa
    /// <c>AuthorizedGrandTotal</c> directo (columna) en vez de <c>GrandTotal</c> (propiedad
    /// calculada con fallback a la suma de líneas) porque toda factura Authorized ya tiene ese
    /// valor persistido — evita depender de traducción EF de una propiedad calculada.
    /// </summary>
    private async Task<(decimal Total, int Count)> ReadSalesTotalsAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct
    )
    {
        var query = _db
            .SalesInvoices.AsNoTracking()
            .ForOperationalScope(tenantId, _company)
            .Where(i => i.Status == SalesInvoiceStatus.Authorized)
            .Where(i => i.IssueDate >= from && i.IssueDate <= to);

        var total = await query.SumAsync(i => i.AuthorizedGrandTotal ?? 0m, ct);
        var count = await query.CountAsync(ct);
        return (total, count);
    }

    private async Task<decimal> ReadSalesTotalAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct
    ) =>
        await _db
            .SalesInvoices.AsNoTracking()
            .ForOperationalScope(tenantId, _company)
            .Where(i => i.Status == SalesInvoiceStatus.Authorized)
            .Where(i => i.IssueDate >= from && i.IssueDate <= to)
            .SumAsync(i => i.AuthorizedGrandTotal ?? 0m, ct);

    /// <summary>
    /// CxC pendiente/vencida. Replica el mismo criterio ya probado en
    /// <c>SalesReceivableRepository.GetPagedAsync</c>: "pending" = no cancelada y saldo
    /// (<c>OriginalAmount - PaidAmount</c>) mayor a cero — se usa la resta directa en vez de
    /// <c>BalanceDue</c> (propiedad calculada) siguiendo ese mismo precedente. "Vencida" agrega
    /// que exista al menos una cuota (<c>SalesReceivableInstallment</c>) con <c>DueDate</c>
    /// anterior a <paramref name="asOf"/> — el pago se registra a nivel de cabecera (no hay
    /// seguimiento de pago por cuota en este módulo), por lo que la fecha de vencimiento de
    /// cualquier cuota ya vencida es la señal disponible de mora.
    /// </summary>
    private async Task<(
        decimal PendingTotal,
        int PendingCount,
        decimal OverdueTotal,
        int OverdueCount
    )> ReadReceivablesAsync(Guid tenantId, DateOnly asOf, CancellationToken ct)
    {
        var pendingBase = _db
            .SalesReceivables.AsNoTracking()
            .ForOperationalScope(tenantId, _company)
            .Where(r => r.Status != "cancelled")
            .Where(r => r.OriginalAmount - r.PaidAmount > 0);

        var pendingTotal = await pendingBase.SumAsync(r => r.OriginalAmount - r.PaidAmount, ct);
        var pendingCount = await pendingBase.CountAsync(ct);

        var overdueBase = pendingBase.Where(r => r.Installments.Any(i => i.DueDate < asOf));
        var overdueTotal = await overdueBase.SumAsync(r => r.OriginalAmount - r.PaidAmount, ct);
        var overdueCount = await overdueBase.CountAsync(ct);

        return (pendingTotal, pendingCount, overdueTotal, overdueCount);
    }

    /// <summary>
    /// CxP pendiente/vencida. <c>AccountsPayable</c> no tiene columnas propias de saldo — todo
    /// se deriva sumando <c>AccountsPayableInstallment</c> (única fuente de saldo vivo, ver
    /// comentario en la entidad). Se agrupa por cabecera para no contar cuotas sueltas como si
    /// fueran cuentas por pagar independientes.
    /// </summary>
    private async Task<(
        decimal PendingTotal,
        int PendingCount,
        decimal OverdueTotal,
        int OverdueCount
    )> ReadPayablesAsync(Guid tenantId, DateOnly asOf, CancellationToken ct)
    {
        var installmentRows =
            from ap in _db.AccountsPayables.AsNoTracking().ForOperationalScope(tenantId, _company)
            where ap.Status != AccountsPayableStatus.Cancelled
            join inst in _db.AccountsPayableInstallments.AsNoTracking()
                on ap.Id equals inst.AccountsPayableId
            select new
            {
                ap.Id,
                Outstanding =
                    inst.Amount
                    - inst.PaidAmount
                    - inst.RetainedAmount
                    - inst.ReturnCreditAmount
                    - inst.SupplierCreditAmount
                    - inst.CreditNoteAmount,
                inst.DueDate,
            };

        var byHeader = await installmentRows
            .GroupBy(x => x.Id)
            .Select(g => new
            {
                Outstanding = g.Sum(x => x.Outstanding),
                HasOverdue = g.Any(x => x.DueDate < asOf && x.Outstanding > 0),
            })
            .ToListAsync(ct);

        var pending = byHeader.Where(x => x.Outstanding > 0).ToList();
        var overdue = pending.Where(x => x.HasOverdue).ToList();

        return (
            pending.Sum(x => x.Outstanding),
            pending.Count,
            overdue.Sum(x => x.Outstanding),
            overdue.Count
        );
    }

    /// <summary>
    /// Stock bajo/sin stock por SKU (no por fila bodega x producto, a diferencia del reporte de
    /// stock actual) — se agrega la cantidad de <c>CurrentStock</c> por <c>ProductId</c> en todas
    /// las bodegas de la empresa antes de comparar contra <c>MinStockQty</c>, para no contar el
    /// mismo producto más de una vez si está repartido en varias bodegas. Solo ítems activos que
    /// llevan control de inventario (<c>StockConfig.TracksStock</c>); un ítem sin ninguna fila de
    /// <c>CurrentStock</c> (nunca tuvo movimiento) cuenta como cantidad cero (no aparece en el
    /// diccionario agregado, <c>GetValueOrDefault</c> resuelve a 0).
    /// </summary>
    private async Task<(int LowStock, int OutOfStock)> ReadStockAsync(
        Guid tenantId,
        CancellationToken ct
    )
    {
        // El LEFT JOIN entre el agregado por SKU (GroupBy) y los ítems, con acceso condicional al
        // lado derecho, no lo traduce el provider de Npgsql en una sola consulta — se materializan
        // dos proyecciones pequeñas (Guid + decimal, no entidades completas) y se combinan en
        // memoria; el volumen de SKUs/existencias de un catálogo ERP no justifica el riesgo de una
        // consulta compuesta no traducible.
        var stockByProduct = await _db
            .CurrentStocks.AsNoTracking()
            .ForOperationalScope(tenantId, _company)
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, TotalQuantity = g.Sum(s => s.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.TotalQuantity, ct);

        var trackedItems = await _db
            .Items.AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.IsActive && i.StockConfig.TracksStock)
            .Select(i => new { i.Id, i.StockConfig.MinStockQty })
            .ToListAsync(ct);

        var outOfStock = 0;
        var lowStock = 0;
        foreach (var item in trackedItems)
        {
            var quantity = stockByProduct.GetValueOrDefault(item.Id);
            if (quantity <= 0)
                outOfStock++;
            else if (item.MinStockQty.HasValue && quantity <= item.MinStockQty.Value)
                lowStock++;
        }

        return (lowStock, outOfStock);
    }
}
