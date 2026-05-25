using ERP.Domain.Modules.Commercial.Entities;

namespace ERP.Application.Modules.Commercial.DTOs;

internal static class QuoteDtoMapper
{
    internal static QuoteDto ToDto(Quote quote) => new(
        quote.PublicId,
        quote.QuoteNumber,
        quote.BusinessPartnerId,
        quote.IssueDate,
        quote.ValidUntil,
        quote.Status,
        quote.CurrencyCode,
        quote.Subtotal,
        quote.TaxTotal,
        quote.Total,
        quote.PaymentTermDays,
        quote.Notes,
        quote.Lines.Select(ToLineDto).ToList(),
        quote.CreatedAt);

    internal static QuoteListItemDto ToListItem(Quote quote, string businessPartnerName) => new(
        quote.PublicId,
        quote.QuoteNumber,
        quote.BusinessPartnerId,
        businessPartnerName,
        quote.IssueDate,
        quote.ValidUntil,
        quote.Status,
        quote.Total,
        quote.CreatedAt);

    internal static QuoteDetailDto ToDetail(
        Quote quote,
        string businessPartnerName,
        Guid? relatedSalesOrderPublicId = null) => new(
        quote.PublicId,
        quote.QuoteNumber,
        quote.BusinessPartnerId,
        businessPartnerName,
        quote.IssueDate,
        quote.ValidUntil,
        quote.Status,
        quote.CurrencyCode,
        quote.Subtotal,
        quote.TaxTotal,
        quote.Total,
        quote.PaymentTermDays,
        quote.Notes,
        quote.Lines.Select(ToLineDto).ToList(),
        quote.CreatedAt,
        relatedSalesOrderPublicId);

    private static QuoteLineDto ToLineDto(QuoteDetail line) => new(
        line.LineNo,
        line.ProductId,
        line.ProductNameSnapshot,
        line.SkuSnapshot,
        line.Quantity,
        line.UnitPrice,
        line.TaxRateSnapshot,
        line.LineTotal);
}
