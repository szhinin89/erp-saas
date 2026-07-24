using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Modules.Sales;

internal static class SalesMapper
{
    /// <summary>
    /// Fase 10: <see cref="ElectronicDocument"/> es la única fuente de verdad del estado
    /// electrónico — <c>SalesInvoice</c> ya no lo administra. <paramref name="electronicDocument"/>
    /// se resuelve en el handler (vía <c>IElectronicDocumentRepository.GetBySourceAsync</c>,
    /// <c>SourceModule="Sales"</c>) y puede ser <c>null</c> si todavía no existe (factura sin
    /// documento electrónico registrado, o módulo con <c>EmissionType</c> no electrónico).
    /// </summary>
    public static SalesInvoiceDto ToDto(
        SalesInvoice i, ElectronicDocument? electronicDocument = null, string? electronicIssueError = null) => new(
        i.Id, i.CustomerId, i.Customer.Name, i.Customer.TaxId,
        i.Customer.IdentificationType, i.Customer.Email, i.Customer.Address,
        i.DocTypeCode, i.SriPaymentMethodCode, i.InvoiceNumber, i.IssueDate,
        i.CashSessionId, i.EmissionPointId, i.EmissionType.ToString(),
        i.CurrencyCode, i.ExchangeRate,
        i.PaymentTerm.Id, i.PaymentTerm.Name,
        i.PaymentTerm.Installments, i.PaymentTerm.DaysBetween, i.CreditTermDays,
        i.DueDate, i.Notes, i.Status.ToString(),
        electronicDocument?.CurrentState.ToString() ?? "None",
        electronicDocument?.AccessKey?.Value,
        electronicDocument?.AuthorizationNumber?.Value,
        electronicDocument?.AuthorizationDate,
        i.Subtotal, i.TotalDiscount, i.TotalIce, i.TotalVat, i.TotalTax,
        i.GrandTotal,
        i.Payments.Select(MapPayment).ToList(),
        i.Lines.OrderBy(l => l.SortOrder).Select(MapDetail).ToList(),
        i.CreatedAt, i.UpdatedAt,
        electronicIssueError);

    private static SalesInvoicePaymentDto MapPayment(SalesInvoicePayment p) => new(
        p.Id, p.PaymentMethodId, p.PaymentMethodCode, p.PaymentMethodName,
        p.Amount, p.Reference,
        p.CardDetail is not null ? new PaymentCardDetailDto(
            p.CardDetail.CardBrand, p.CardDetail.CardLastFour, p.CardDetail.BankName,
            p.CardDetail.AuthorizationCode, p.CardDetail.LotNumber) : null,
        p.TransferDetail is not null ? new PaymentTransferDetailDto(
            p.TransferDetail.BankName, p.TransferDetail.ReceiptNumber, p.TransferDetail.TransferDate) : null,
        p.ChequeDetail is not null ? new PaymentChequeDetailDto(
            p.ChequeDetail.BankName, p.ChequeDetail.ChequeNumber, p.ChequeDetail.HolderName, p.ChequeDetail.CashDate) : null);

    public static SalesInvoiceDetailDto MapDetail(SalesInvoiceDetail l) => new(
        l.Id, l.ItemId, l.WarehouseId, l.Description,
        l.SnapshotSku, l.SnapshotItemName,
        l.UomCode, l.ConversionFactor, l.QuantityInBaseUom,
        l.Quantity, l.UnitPrice, l.DiscountPct, l.DiscountAmount,
        l.TaxableBase,
        l.VatCode, l.VatRate, l.VatAmount, l.SnapshotVatName,
        l.IceCode, l.IceRate, l.IceAmount, l.SnapshotIceName,
        l.TaxInclusiveTotal,
        l.Notes, l.SortOrder);
}
