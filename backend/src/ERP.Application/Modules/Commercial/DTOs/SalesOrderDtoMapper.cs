using ERP.Application.Modules.Commercial.DTOs;
using ERP.Domain.Modules.Commercial.Entities;

namespace ERP.Application.Modules.Commercial.DTOs;

internal static class SalesOrderDtoMapper
{
    internal static SalesOrderDto ToDto(SalesOrder order) => new(
        order.PublicId,
        order.OrderNumber,
        order.BusinessPartnerId,
        order.WarehouseId,
        order.IssueDate,
        order.RequiredDate,
        order.Status,
        order.CurrencyCode,
        order.Subtotal,
        order.TaxTotal,
        order.Total,
        order.PaymentTermDays,
        order.Notes,
        order.Lines.Select(ToLineDto).ToList(),
        order.CreatedAt);

    internal static SalesOrderListItemDto ToListItem(SalesOrder order, string businessPartnerName) => new(
        order.PublicId,
        order.OrderNumber,
        order.BusinessPartnerId,
        businessPartnerName,
        order.IssueDate,
        order.RequiredDate,
        order.Status,
        order.Total,
        order.CreatedAt);

    internal static SalesOrderDetailDto ToDetail(
        SalesOrder order,
        string businessPartnerName,
        string warehouseName,
        Guid? relatedInvoicePublicId = null) => new(
        order.PublicId,
        order.OrderNumber,
        order.BusinessPartnerId,
        businessPartnerName,
        order.WarehouseId,
        warehouseName,
        order.IssueDate,
        order.RequiredDate,
        order.Status,
        order.CurrencyCode,
        order.Subtotal,
        order.TaxTotal,
        order.Total,
        order.PaymentTermDays,
        order.Notes,
        order.Lines.Select(ToLineDto).ToList(),
        order.CreatedAt,
        relatedInvoicePublicId);

    private static SalesOrderLineDto ToLineDto(SalesOrderDetail line) => new(
        line.LineNo,
        line.ProductId,
        line.ProductNameSnapshot,
        line.SkuSnapshot,
        line.Quantity,
        line.UnitPrice,
        line.TaxRateSnapshot,
        line.LineTotal);
}
