using ERP.Application.Common;
using ERP.Domain.Modules.Sales.Enums;
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

        // ERP-CORE-CLOSEOUT-08: los totales del reporte son ingresos reales — Draft (aún no
        // emitida) y Cancelled nunca deben sumar a la venta del período, aunque las filas sigan
        // visibles (con su Status real) para auditoría/trazabilidad.
        var authorizedInvoices = invoices
            .Where(i => i.Status == SalesInvoiceStatus.Authorized)
            .ToList();
        var totals = new SalesReportTotalsDto(
            authorizedInvoices.Count,
            authorizedInvoices.Sum(i => i.Subtotal),
            authorizedInvoices.Sum(i => i.TotalVat),
            authorizedInvoices.Sum(i => i.TotalDiscount),
            authorizedInvoices.Sum(i => i.GrandTotal)
        );

        return Result<SalesReportResponse>.Success(
            new SalesReportResponse(rows, totals, dateFrom, dateTo)
        );
    }
}
