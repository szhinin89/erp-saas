using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases.GetPurchasesBySupplierReport;

public sealed class GetPurchasesBySupplierReportQueryHandler
    : IRequestHandler<GetPurchasesBySupplierReportQuery, Result<PurchasesBySupplierReportResponse>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;

    public GetPurchasesBySupplierReportQueryHandler(
        IPurchaseInvoiceRepository repo,
        ICurrentTenant t
    )
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<PurchasesBySupplierReportResponse>> Handle(
        GetPurchasesBySupplierReportQuery q,
        CancellationToken ct
    )
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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

        var totals = new PurchasesReportTotalsDto(
            rows.Count,
            rows.Sum(r => r.Subtotal),
            rows.Sum(r => r.TotalVat),
            rows.Sum(r => r.TotalDiscount),
            rows.Sum(r => r.GrandTotal)
        );

        return Result<PurchasesBySupplierReportResponse>.Success(
            new PurchasesBySupplierReportResponse(rows, totals, dateFrom, dateTo)
        );
    }
}
