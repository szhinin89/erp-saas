using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Línea de una <see cref="PurchaseReturn"/> — diseño P0-02 §7.2. A diferencia de
/// <c>SalesReturnDetail</c> (que congela su snapshot financiero al crearse), esta línea solo fija
/// <see cref="OriginalInvoiceDetailId"/>/<see cref="ItemId"/>/<see cref="Quantity"/>/
/// <see cref="WarehouseId"/> en <see cref="Create"/> (borrador) — el snapshot financiero
/// (<see cref="UnitCost"/>, IVA/ICE, montos prorrateados, costo histórico) solo se conoce y se
/// congela una única vez en <see cref="PurchaseReturn.Authorize"/>, vía <see cref="Freeze"/>,
/// porque el remanente y el costo solo son ciertos en el momento de autorizar, no al crear el
/// borrador (§7.2, §11.1, §19.1bis).
/// </summary>
public sealed class PurchaseReturnDetail : IMustHaveTenant
{
    // ── Identity ────────────────────────────────────────────────────────
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PurchaseReturnId { get; private set; }

    /// <summary>Referencia inequívoca a la línea exacta de <c>PurchaseInvoiceDetail</c> — nunca al producto/bodega de forma ambigua (§10.1).</summary>
    public Guid OriginalInvoiceDetailId { get; private set; }

    /// <summary>Snapshot de <c>PurchaseInvoiceDetail.ItemId</c> — inmutable.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>Cantidad devuelta — editable mientras la devolución esté en <c>Draft</c>, congelada tras <see cref="Freeze"/>.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>
    /// Snapshot de <c>PurchaseInvoiceDetail.WarehouseId</c> — congela la bodega histórica, nunca
    /// seleccionable por el usuario: una devolución sale de donde entró.
    /// </summary>
    public Guid WarehouseId { get; private set; }

    // ── Snapshot financiero (solo tras Authorize()) ─────────────────────
    public decimal? UnitCost { get; private set; }
    public string? VatCode { get; private set; }
    public decimal? VatRate { get; private set; }
    public string? IceCode { get; private set; }
    public decimal? IceRate { get; private set; }
    public decimal? ReturnedSubtotal { get; private set; }
    public decimal? ReturnedDiscountAmount { get; private set; }
    public decimal? ReturnedVatAmount { get; private set; }
    public decimal? ReturnedIceAmount { get; private set; }
    public decimal? HistoricalCostAmount { get; private set; }

    public bool IsFrozen { get; private set; }

    // ── Calculated (NOT persisted) ──────────────────────────────────────
    public decimal LineGrandTotal =>
        (ReturnedSubtotal ?? 0m) + (ReturnedVatAmount ?? 0m) + (ReturnedIceAmount ?? 0m);

    private PurchaseReturnDetail() { }

    // ── Factory (Draft) ─────────────────────────────────────────────────
    public static PurchaseReturnDetail Create(
        Guid purchaseReturnId,
        Guid tenantId,
        Guid originalInvoiceDetailId,
        Guid itemId,
        decimal quantity,
        Guid warehouseId
    )
    {
        if (purchaseReturnId == Guid.Empty)
            throw new ArgumentException(
                "La devolución destino es obligatoria.",
                nameof(purchaseReturnId)
            );
        if (originalInvoiceDetailId == Guid.Empty)
            throw new ArgumentException(
                "La línea de factura original es obligatoria.",
                nameof(originalInvoiceDetailId)
            );
        if (itemId == Guid.Empty)
            throw new ArgumentException("El producto es obligatorio.", nameof(itemId));
        if (quantity <= 0)
            throw new ArgumentException(
                "La cantidad devuelta debe ser mayor a cero.",
                nameof(quantity)
            );
        if (warehouseId == Guid.Empty)
            throw new ArgumentException("La bodega es obligatoria.", nameof(warehouseId));

        return new PurchaseReturnDetail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseReturnId = purchaseReturnId,
            OriginalInvoiceDetailId = originalInvoiceDetailId,
            ItemId = itemId,
            Quantity = quantity,
            WarehouseId = warehouseId,
            IsFrozen = false,
        };
    }

    /// <summary>
    /// Congela el snapshot financiero de la línea — invocado exclusivamente desde
    /// <see cref="PurchaseReturn.Authorize"/> con los valores ya calculados por la fórmula de §11.1
    /// (proporción sobre la línea original) y el costo histórico de §19.1bis. Irreversible.
    /// </summary>
    internal void Freeze(
        decimal unitCost,
        string vatCode,
        decimal vatRate,
        string? iceCode,
        decimal iceRate,
        decimal returnedSubtotal,
        decimal returnedDiscountAmount,
        decimal returnedVatAmount,
        decimal returnedIceAmount,
        decimal historicalCostAmount
    )
    {
        EnsureNotFrozen();
        if (string.IsNullOrWhiteSpace(vatCode))
            throw new ArgumentException("El código IVA es obligatorio.", nameof(vatCode));

        UnitCost = unitCost;
        VatCode = vatCode.Trim();
        VatRate = vatRate;
        IceCode = OptionalCode.Normalize(iceCode);
        IceRate = iceRate;
        ReturnedSubtotal = returnedSubtotal;
        ReturnedDiscountAmount = returnedDiscountAmount;
        ReturnedVatAmount = returnedVatAmount;
        ReturnedIceAmount = returnedIceAmount;
        HistoricalCostAmount = historicalCostAmount;
        IsFrozen = true;
    }

    private void EnsureNotFrozen()
    {
        if (IsFrozen)
            throw new InvalidOperationException(
                "La línea de devolución ya fue autorizada y no puede modificarse."
            );
    }
}
