using ERP.Application.Common;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases.GetPurchasesBySupplierReport;

/// <summary>
/// Reporte básico de compras por proveedor (piloto Sumak). Branch-scoped por exigencia de
/// contexto (defensa en profundidad vía BranchScopeBehavior/IBranchAccessGuard) — el resultado
/// es company-scoped (no filtra por sucursal), igual que GetDailySalesReportQuery.
/// </summary>
public sealed record GetPurchasesBySupplierReportQuery(
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    Guid? SupplierId = null
) : IRequest<Result<PurchasesBySupplierReportResponse>>, IBranchScopedRequest;

public sealed record PurchasesReportRowDto(
    Guid Id,
    DateOnly IssueDate,
    Guid SupplierId,
    string SupplierName,
    string SupplierTaxId,
    string InvoiceNumber,
    string Status,
    decimal Subtotal,
    decimal TotalVat,
    decimal TotalDiscount,
    decimal GrandTotal
);

public sealed record PurchasesReportTotalsDto(
    int Count,
    decimal Subtotal,
    decimal TotalVat,
    decimal TotalDiscount,
    decimal GrandTotal
);

public sealed record PurchasesBySupplierReportResponse(
    IReadOnlyList<PurchasesReportRowDto> Items,
    PurchasesReportTotalsDto Totals,
    DateOnly DateFrom,
    DateOnly DateTo
);

public sealed class GetPurchasesBySupplierReportQueryValidator
    : AbstractValidator<GetPurchasesBySupplierReportQuery>
{
    public GetPurchasesBySupplierReportQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => x.DateFrom is null || x.DateTo is null || x.DateFrom <= x.DateTo)
            .WithMessage("La fecha 'desde' no puede ser posterior a la fecha 'hasta'.");
    }
}
