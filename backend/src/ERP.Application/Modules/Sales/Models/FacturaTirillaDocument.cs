using ERP.Domain.Modules.Fiscal.Entities;

namespace ERP.Application.Sales.Models;

public sealed class FacturaTirillaDocument
{
    public string DocType { get; init; } = null!;
    public string EstabCode { get; init; } = null!;
    public string EmPointCode { get; init; } = null!;
    public string Sequential { get; init; } = null!;
    public DateTime IssueDate { get; init; }
    public string Status { get; init; } = null!;
    public string? AuthNumber { get; init; }
    public string AccessKey { get; init; } = null!;
    public decimal Subtotal { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal Total { get; init; }
    public IReadOnlyList<FacturaTirillaLine> Lines { get; init; } = Array.Empty<FacturaTirillaLine>();

    public static FacturaTirillaDocument FromInvoice(Invoice invoice) =>
        new()
        {
            DocType = invoice.DocType,
            EstabCode = invoice.EstabCode,
            EmPointCode = invoice.EmPointCode,
            Sequential = invoice.Sequential,
            IssueDate = invoice.IssueDate,
            Status = invoice.Status,
            AuthNumber = invoice.Electronic?.AuthorizationNumber,
            AccessKey = invoice.AccessKey,
            Subtotal = invoice.Subtotal,
            TaxTotal = invoice.TaxTotal,
            Total = invoice.Total,
            Lines = invoice.Lines
                .Select(l => new FacturaTirillaLine(
                    l.DescriptionSnapshot,
                    l.Quantity,
                    l.UnitPriceSnapshot,
                    l.LineTotal))
                .ToList(),
        };
}

public sealed record FacturaTirillaLine(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Total);
