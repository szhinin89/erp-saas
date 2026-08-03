using ERP.Application.Common;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetDailySalesReport;

/// <summary>
/// Reporte básico de ventas por rango de fechas (piloto Sumak). Branch-scoped por exigencia de
/// contexto (defensa en profundidad vía BranchScopeBehavior/IBranchAccessGuard) — SalesInvoice
/// no tiene BranchId de cabecera consultable desde este reporte, así que el resultado es
/// company-scoped (no filtra por sucursal), igual que GetSalesInvoiceListQuery.
/// </summary>
public sealed record GetDailySalesReportQuery(DateOnly? DateFrom = null, DateOnly? DateTo = null)
    : IRequest<Result<SalesReportResponse>>,
        IBranchScopedRequest;

public sealed record SalesReportRowDto(
    Guid Id,
    string InvoiceNumber,
    DateOnly IssueDate,
    Guid CustomerId,
    string CustomerName,
    decimal Subtotal,
    decimal TotalVat,
    decimal TotalDiscount,
    decimal GrandTotal,
    string Status,
    string EmissionType
);

public sealed record SalesReportTotalsDto(
    int Count,
    decimal Subtotal,
    decimal TotalVat,
    decimal TotalDiscount,
    decimal GrandTotal
);

public sealed record SalesReportResponse(
    IReadOnlyList<SalesReportRowDto> Items,
    SalesReportTotalsDto Totals,
    DateOnly DateFrom,
    DateOnly DateTo
);

public sealed class GetDailySalesReportQueryValidator
    : AbstractValidator<GetDailySalesReportQuery>
{
    public GetDailySalesReportQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => x.DateFrom is null || x.DateTo is null || x.DateFrom <= x.DateTo)
            .WithMessage("La fecha 'desde' no puede ser posterior a la fecha 'hasta'.");
    }
}
