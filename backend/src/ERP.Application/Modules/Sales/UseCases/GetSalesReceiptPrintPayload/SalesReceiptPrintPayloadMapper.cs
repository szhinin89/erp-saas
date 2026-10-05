using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesReceiptPrintPayload;

internal static class SalesReceiptPrintPayloadMapper
{
    public static SalesReceiptLineDto MapLine(SalesInvoiceDetail line) =>
        new(
            ProductName: line.SnapshotItemName ?? line.Description,
            Sku: line.SnapshotSku,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            Discount: line.DiscountAmount,
            Subtotal: line.TaxableBase,
            VatRate: line.VatRate,
            VatAmount: line.VatAmount,
            Total: line.TaxInclusiveTotal,
            UomCode: line.UomCode,
            ConversionFactor: line.ConversionFactor
        );

    public static SalesReceiptPaymentDto MapPayment(SalesInvoicePayment payment) =>
        new(payment.PaymentMethodName, payment.Amount, payment.Reference);

    /// <summary>
    /// POS-CASH-TENDERED-01 — "Efectivo recibido" / "Vuelto" de la tirilla, reconstruidos SOLO de
    /// lo persistido en los pagos (TenderedAmount): la tirilla inicial y cualquier reimpresión
    /// muestran lo mismo sin depender del estado del POS. Sin efectivo entregado registrado
    /// (venta sin efectivo o anterior a este dato) → ambos null y la tirilla los omite.
    /// </summary>
    public static (decimal? CashReceived, decimal? CashChange) ResolveCashTendered(
        IEnumerable<SalesInvoicePayment> payments
    )
    {
        var tendered = payments.Where(p => p.TenderedAmount.HasValue).ToList();
        if (tendered.Count == 0)
            return (null, null);
        return (
            tendered.Sum(p => p.TenderedAmount!.Value),
            tendered.Sum(p => p.ChangeAmount!.Value)
        );
    }

    public static (string? EstablishmentCode, string? EmissionPointCode) ResolveSriCodes(
        SalesInvoice invoice,
        string? cashSessionEmissionPointCode,
        string? emissionPointEstablishmentCode,
        string? emissionPointCode
    )
    {
        var split = invoice.InvoiceNumber.Split('-', StringSplitOptions.TrimEntries);
        var establishmentCode =
            !string.IsNullOrWhiteSpace(emissionPointEstablishmentCode)
                ? emissionPointEstablishmentCode
            : split.Length >= 1 ? split[0]
            : null;

        var resolvedEmissionPointCode =
            !string.IsNullOrWhiteSpace(cashSessionEmissionPointCode) ? cashSessionEmissionPointCode
            : !string.IsNullOrWhiteSpace(emissionPointCode) ? emissionPointCode
            : split.Length >= 2 ? split[1]
            : null;

        return (establishmentCode, resolvedEmissionPointCode);
    }
}
