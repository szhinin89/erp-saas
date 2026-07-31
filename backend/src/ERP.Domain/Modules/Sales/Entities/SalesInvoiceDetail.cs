using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesInvoiceDetail : IMustHaveTenant
{
    public const int DescriptionMaxLen = 300;
    public const int NotesMaxLen = 300;
    public const int VatCodeMaxLen = 10;
    public const int IceCodeMaxLen = 10;
    public const int SkuMaxLen = 50;
    public const int ItemNameMaxLen = 254;
    public const int UomCodeMaxLen = 10;
    public const int VatNameMaxLen = 100;
    public const int IceNameMaxLen = 100;

    // ── Identity ────────────────────────────────────────────────────────
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }

    // ── Product Snapshot (immutable after creation) ─────────────────────
    public Guid? ItemId { get; private set; }
    public string Description { get; private set; } = null!;
    public string? SnapshotSku { get; private set; }
    public string? SnapshotItemName { get; private set; }

    // ── Bodega de despacho — obligatoria para ítems que controlan inventario;
    // una misma factura puede despachar líneas desde bodegas distintas. ──────
    public Guid? WarehouseId { get; private set; }

    // ── UoM ─────────────────────────────────────────────────────────────
    public string UomCode { get; private set; } = "UNIT";
    public decimal ConversionFactor { get; private set; } = 1m;
    public decimal QuantityInBaseUom { get; private set; }

    // ── Quantity & Price (snapshot — never recalculated from master) ────
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPct { get; private set; }
    public decimal DiscountAmount { get; private set; }

    // ── VAT (fiscal snapshot) ───────────────────────────────────────────
    public string VatCode { get; private set; } = null!;
    public decimal VatRate { get; private set; }
    public decimal VatAmount { get; private set; }
    public string? SnapshotVatName { get; private set; }

    // ── ICE (fiscal snapshot) ───────────────────────────────────────────
    public string? IceCode { get; private set; }
    public decimal IceRate { get; private set; }
    public decimal IceAmount { get; private set; }
    public string? SnapshotIceName { get; private set; }

    // ── Meta ────────────────────────────────────────────────────────────
    public string? Notes { get; private set; }
    public short SortOrder { get; private set; }
    public bool IsFrozen { get; private set; }

    // ── Calculated (NOT persisted) ──────────────────────────────────────
    public decimal LineSubtotal => Quantity * UnitPrice;
    public decimal TaxableBase =>
        Math.Round(
            LineSubtotal - DiscountAmount,
            FiscalPrecision.TaxAmount,
            MidpointRounding.AwayFromZero
        );
    public decimal TaxInclusiveTotal =>
        Math.Round(
            TaxableBase + IceAmount + VatAmount,
            FiscalPrecision.TaxAmount,
            MidpointRounding.AwayFromZero
        );

    // ── Constructor ─────────────────────────────────────────────────────
    private SalesInvoiceDetail() { }

    // ── Factory ─────────────────────────────────────────────────────────
    public static SalesInvoiceDetail Create(
        Guid invoiceId,
        Guid tenantId,
        string description,
        decimal quantity,
        decimal unitPrice,
        string vatCode,
        string uomCode,
        Guid? itemId = null,
        string? notes = null,
        decimal discountPct = 0,
        string? iceCode = null,
        string? snapshotSku = null,
        string? snapshotItemName = null,
        decimal conversionFactor = 1m,
        Guid? warehouseId = null
    )
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "La descripción de la línea es obligatoria.",
                nameof(description)
            );
        if (quantity <= 0)
            throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(quantity));
        if (unitPrice < 0)
            throw new ArgumentException(
                "El precio unitario no puede ser negativo.",
                nameof(unitPrice)
            );
        if (discountPct is < 0 or > 100)
            throw new ArgumentException(
                "El descuento debe estar entre 0 y 100.",
                nameof(discountPct)
            );
        if (string.IsNullOrWhiteSpace(vatCode))
            throw new ArgumentException("El código IVA es obligatorio.", nameof(vatCode));
        if (string.IsNullOrWhiteSpace(uomCode))
            throw new ArgumentException("La unidad de medida es obligatoria.", nameof(uomCode));
        if (conversionFactor <= 0)
            throw new ArgumentException(
                "El factor de conversión debe ser mayor a cero.",
                nameof(conversionFactor)
            );

        var line = new SalesInvoiceDetail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoiceId,
            ItemId = itemId,
            WarehouseId = warehouseId,
            Description = description.Trim(),
            SnapshotSku = snapshotSku?.Trim(),
            SnapshotItemName = snapshotItemName?.Trim(),
            UomCode = uomCode.Trim().ToUpperInvariant(),
            ConversionFactor = conversionFactor,
            Quantity = quantity,
            QuantityInBaseUom = Math.Round(
                quantity * conversionFactor,
                FiscalPrecision.Quantity,
                MidpointRounding.AwayFromZero
            ),
            UnitPrice = unitPrice,
            DiscountPct = discountPct,
            VatCode = vatCode.Trim(),
            IceCode = OptionalCode.Normalize(iceCode),
            Notes = notes?.Trim(),
            IsFrozen = false,
        };
        line.RecalcDiscount();
        return line;
    }

    // ── Tax Application ─────────────────────────────────────────────────
    public void ApplyTaxes(
        string vatCode,
        decimal vatRate,
        string? vatName,
        string? iceCode,
        decimal iceRate,
        string? iceName
    )
    {
        EnsureNotFrozen();
        if (string.IsNullOrWhiteSpace(vatCode))
            throw new ArgumentException("El código IVA es obligatorio.", nameof(vatCode));
        if (vatRate < 0)
            throw new ArgumentException("La tasa IVA no puede ser negativa.", nameof(vatRate));
        if (iceRate < 0)
            throw new ArgumentException("La tasa ICE no puede ser negativa.", nameof(iceRate));

        VatCode = vatCode.Trim();
        VatRate = vatRate;
        SnapshotVatName = vatName?.Trim();
        IceCode = OptionalCode.Normalize(iceCode);
        IceRate = iceRate;
        SnapshotIceName = iceName?.Trim();
        RecalcTaxes();
    }

    // ── Discount ────────────────────────────────────────────────────────
    public void ApplyDiscount(decimal pct)
    {
        EnsureNotFrozen();
        if (pct is < 0 or > 100)
            throw new ArgumentException("El descuento debe estar entre 0 y 100.", nameof(pct));
        DiscountPct = pct;
        RecalcDiscount();
        RecalcTaxes();
    }

    // ── Sort ────────────────────────────────────────────────────────────
    internal void SetSortOrder(short order)
    {
        EnsureNotFrozen();
        SortOrder = order;
    }

    // ── Freeze (called once on Authorize — irreversible) ────────────────
    internal void Freeze()
    {
        if (IsFrozen)
            return;
        RecalcTaxes();
        IsFrozen = true;
    }

    // ── Invariant Guards ────────────────────────────────────────────────
    private void EnsureNotFrozen()
    {
        if (IsFrozen)
            throw new InvalidOperationException(
                "La línea de venta está autorizada y no puede ser modificada."
            );
    }

    // ── Private Calculations ────────────────────────────────────────────
    private void RecalcDiscount()
    {
        DiscountAmount =
            DiscountPct > 0
                ? Math.Round(
                    LineSubtotal * DiscountPct / 100m,
                    FiscalPrecision.UnitCost,
                    MidpointRounding.AwayFromZero
                )
                : 0;
    }

    private void RecalcTaxes()
    {
        (IceAmount, VatAmount, _) = SriTaxCalculator.Compute(
            TaxableBase,
            VatRate,
            !string.IsNullOrWhiteSpace(IceCode) ? IceRate : 0m
        );
    }
}
