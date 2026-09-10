using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Línea fiscal de una NC. Para devolución conserva la referencia exacta a la línea comprada,
/// la cantidad y los importes históricos prorrateados. El inventario y la contabilidad se aplican
/// exclusivamente mediante la <see cref="PurchaseReturn"/> vinculada.
/// </summary>
public sealed class PurchaseCreditNoteDetail : IMustHaveTenant
{
    public const int DescriptionMaxLen = 500;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PurchaseCreditNoteId { get; private set; }

    public string Description { get; private set; } = null!;

    public Guid? PurchaseInvoiceDetailId { get; private set; }
    public decimal? Quantity { get; private set; }
    public decimal IceAmount { get; private set; }
    public decimal IrbpnrAmount { get; private set; }

    public decimal Subtotal { get; private set; }
    public string? VatCode { get; private set; }
    public decimal? VatRate { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal TotalAmount { get; private set; }

    private PurchaseCreditNoteDetail() { }

    public static PurchaseCreditNoteDetail Create(
        Guid purchaseCreditNoteId,
        Guid tenantId,
        string description,
        decimal subtotal,
        string? vatCode,
        decimal? vatRate,
        decimal vatAmount,
        Guid? purchaseInvoiceDetailId = null,
        decimal? quantity = null,
        decimal iceAmount = 0m,
        decimal irbpnrAmount = 0m
    )
    {
        if (purchaseCreditNoteId == Guid.Empty)
            throw new ArgumentException(
                "La nota de crédito destino es obligatoria.",
                nameof(purchaseCreditNoteId)
            );
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "La descripción de la línea es obligatoria.",
                nameof(description)
            );
        if (purchaseInvoiceDetailId is not null && (purchaseInvoiceDetailId == Guid.Empty || quantity is null or <= 0))
            throw new ArgumentException("La línea de factura y cantidad a devolver son obligatorias.");
        if (iceAmount < 0 || irbpnrAmount < 0)
            throw new ArgumentException("Los impuestos no pueden ser negativos.");
        if (subtotal < 0 || (subtotal == 0 && purchaseInvoiceDetailId is null))
            throw new ArgumentException(
                "El subtotal de la línea debe ser mayor a cero.",
                nameof(subtotal)
            );
        if (vatAmount < 0)
            throw new ArgumentException(
                "El IVA de la línea no puede ser negativo.",
                nameof(vatAmount)
            );

        return new PurchaseCreditNoteDetail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseCreditNoteId = purchaseCreditNoteId,
            Description = description.Trim(),
            Subtotal = subtotal,
            VatCode = OptionalCode.Normalize(vatCode),
            VatRate = vatRate,
            VatAmount = vatAmount,
            PurchaseInvoiceDetailId = purchaseInvoiceDetailId,
            Quantity = quantity,
            IceAmount = iceAmount,
            IrbpnrAmount = irbpnrAmount,
            TotalAmount = subtotal + vatAmount + iceAmount + irbpnrAmount,
        };
    }
}
