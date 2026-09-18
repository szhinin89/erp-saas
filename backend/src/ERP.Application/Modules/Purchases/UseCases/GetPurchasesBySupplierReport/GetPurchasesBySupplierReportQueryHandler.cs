using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases.GetPurchasesBySupplierReport;

public sealed class GetPurchasesBySupplierReportQueryHandler
    : IRequestHandler<GetPurchasesBySupplierReportQuery, Result<PurchasesBySupplierReportResponse>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICompanyClock _companyClock;

    public GetPurchasesBySupplierReportQueryHandler(
        IPurchaseInvoiceRepository repo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICompanyClock companyClock
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
        _companyClock = companyClock;
    }

    public async Task<Result<PurchasesBySupplierReportResponse>> Handle(
        GetPurchasesBySupplierReportQuery q,
        CancellationToken ct
    )
    {
        var today = await _companyClock.TodayAsync(_c.CompanyId, _t.TenantId, ct);
        var dateFrom = q.DateFrom ?? today;
        var dateTo = q.DateTo ?? today;

        var invoices = await _repo.GetForSupplierReportAsync(
            _t.TenantId,
            dateFrom,
            dateTo,
            q.SupplierId,
            ct
        );

        var rows = invoices
            .Select(i => new PurchasesReportRowDto(
                i.Id,
                i.IssueDate,
                i.SupplierId,
                i.SupplierName,
                i.SupplierTaxId,
                i.InvoiceNumber,
                i.Status.ToString(),
                i.Subtotal,
                i.TotalVat,
                i.TotalDiscount,
                i.GrandTotal
            ))
            .ToList();

        // ERP-CORE-CLOSEOUT-08: los totales del reporte son compras reales — Draft (aún no
        // confirmada) y Cancelled nunca deben sumar al gasto del período, aunque las filas sigan
        // visibles (con su Status real) para auditoría/trazabilidad.
        var confirmedInvoices = invoices.Where(i => i.Status == PurchaseStatus.Confirmed).ToList();
        var totals = new PurchasesReportTotalsDto(
            confirmedInvoices.Count,
            confirmedInvoices.Sum(i => i.Subtotal),
            confirmedInvoices.Sum(i => i.TotalVat),
            confirmedInvoices.Sum(i => i.TotalDiscount),
            confirmedInvoices.Sum(i => i.GrandTotal)
        );

        return Result<PurchasesBySupplierReportResponse>.Success(
            new PurchasesBySupplierReportResponse(rows, totals, dateFrom, dateTo)
        );
    }
}
