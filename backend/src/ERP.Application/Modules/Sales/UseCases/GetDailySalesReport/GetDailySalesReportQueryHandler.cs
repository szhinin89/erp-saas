using ERP.Application.Common;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetDailySalesReport;

public sealed class GetDailySalesReportQueryHandler
    : IRequestHandler<GetDailySalesReportQuery, Result<SalesReportResponse>>
{
    private readonly ISalesInvoiceRepository _repo;
    private readonly ICurrentTenant _t;

    public GetDailySalesReportQueryHandler(ISalesInvoiceRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<SalesReportResponse>> Handle(
        GetDailySalesReportQuery q,
        CancellationToken ct
    )
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dateFrom = q.DateFrom ?? today;
        var dateTo = q.DateTo ?? today;

        var invoices = await _repo.GetForDailyReportAsync(_t.TenantId, dateFrom, dateTo, ct);

        var rows = invoices
            .Select(i => new SalesReportRowDto(
                i.Id,
                i.InvoiceNumber,
                i.IssueDate,
                i.CustomerId,
                i.Customer.Name,
                i.Subtotal,
                i.TotalVat,
                i.TotalDiscount,
                i.GrandTotal,
                i.Status.ToString(),
                i.EmissionType.ToString()
            ))
            .ToList();

        var totals = new SalesReportTotalsDto(
            rows.Count,
            rows.Sum(r => r.Subtotal),
            rows.Sum(r => r.TotalVat),
            rows.Sum(r => r.TotalDiscount),
            rows.Sum(r => r.GrandTotal)
        );

        return Result<SalesReportResponse>.Success(
            new SalesReportResponse(rows, totals, dateFrom, dateTo)
        );
    }
}
