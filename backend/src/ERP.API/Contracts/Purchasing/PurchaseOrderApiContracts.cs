namespace ERP.API.Contracts.Purchasing;

/// <param name="PurchaseInvoiceId">ID de la factura de compra aprobada a vincular.</param>
public sealed record LinkInvoiceRequest(Guid PurchaseInvoiceId);
