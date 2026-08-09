using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Línea de concepto de una <see cref="PurchaseCreditNote"/> — diseño FLOW-READY-02C §2.2. Solo
/// cubre descuento/promoción: nunca tiene <c>ItemId</c>/<c>WarehouseId</c>/<c>Quantity</c>/
/// <c>PurchaseInvoiceDetailId</c>/<c>AffectsStock</c> porque este agregado nunca mueve inventario
/// (§0.1 — devolución física es responsabilidad exclusiva de <see cref="PurchaseReturn"/>).
/// </summary>
public sealed class PurchaseCreditNoteDetail : IMustHaveTenant
{
    public const int DescriptionMaxLen = 500;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PurchaseCreditNoteId { get; private set; }

    public string Description { get; private set; } = null!;

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
        decimal vatAmount
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
        if (subtotal <= 0)
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
            TotalAmount = subtotal + vatAmount,
        };
    }
}
