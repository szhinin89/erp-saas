using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

/// <summary>
/// Línea de una <see cref="SalesReturn"/>. Hereda el snapshot fiscal (VAT/ICE/precio unitario) de
/// la línea de factura original congelada — nunca resuelve impuestos contra el ítem vigente
/// (Infraestructura CLOSED — Configuración Tributaria). Los montos se calculan una única vez, al
/// crear la línea, con la misma fórmula que <c>SalesInvoiceDetail</c> pero aplicada a la cantidad
/// devuelta — no es un prorrateo del total de la factura.
/// </summary>
public sealed class SalesReturnDetail : IMustHaveTenant
{
    public const int DescriptionMaxLen = 300;
    public const int VatCodeMaxLen = 10;
    public const int IceCodeMaxLen = 10;
    public const int SkuMaxLen = 50;
    public const int ItemNameMaxLen = 254;
    public const int UomCodeMaxLen = 10;

    // ── Identity ────────────────────────────────────────────────────────
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ReturnId { get; private set; }

    /// <summary>Trazabilidad línea a línea contra la factura original.</summary>
    public Guid OriginalInvoiceDetailId { get; private set; }

    // ── Product Snapshot (immutable — heredado de la línea original) ────
    public Guid? ItemId { get; private set; }
    public string Description { get; private set; } = null!;
    public string? SnapshotSku { get; private set; }
    public string? SnapshotItemName { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public Guid? PackagingLevelId { get; private set; }
    public string UomCode { get; private set; } = "UNIT";
    public string BaseUomCode { get; private set; } = "UNIT";
    public decimal ConversionFactor { get; private set; } = 1m;
    public decimal QuantityInBaseUom { get; private set; }

    // ── Quantity & Price (snapshot heredado — nunca recalculado contra el ítem) ──
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPct { get; private set; }
    public decimal DiscountAmount { get; private set; }

    // ── VAT (snapshot heredado de la factura original) ──────────────────
    public string VatCode { get; private set; } = null!;
    public decimal VatRate { get; private set; }
    public decimal VatAmount { get; private set; }

    // ── ICE (snapshot heredado de la factura original) ──────────────────
    public string? IceCode { get; private set; }
    public decimal IceRate { get; private set; }
    public decimal IceAmount { get; private set; }

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

    private SalesReturnDetail() { }

    // ── Factory ─────────────────────────────────────────────────────────
    public static SalesReturnDetail Create(
        Guid returnId,
        Guid tenantId,
        Guid originalInvoiceDetailId,
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal discountPct,
        string vatCode,
        decimal vatRate,
        string uomCode,
        Guid? itemId = null,
        Guid? warehouseId = null,
        string? snapshotSku = null,
        string? snapshotItemName = null,
        string? iceCode = null,
        decimal iceRate = 0m,
        Guid? packagingLevelId = null,
        decimal conversionFactor = 1m,
        string? baseUomCode = null
    )
    {
        if (originalInvoiceDetailId == Guid.Empty)
            throw new ArgumentException(
                "La línea de factura original es obligatoria.",
                nameof(originalInvoiceDetailId)
            );
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "La descripción de la línea es obligatoria.",
                nameof(description)
            );
        if (quantity <= 0)
            throw new ArgumentException(
                "La cantidad devuelta debe ser mayor a cero.",
                nameof(quantity)
            );
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
        if (vatRate < 0)
            throw new ArgumentException("La tasa IVA no puede ser negativa.", nameof(vatRate));
        if (iceRate < 0)
            throw new ArgumentException("La tasa ICE no puede ser negativa.", nameof(iceRate));
        if (string.IsNullOrWhiteSpace(uomCode))
            throw new ArgumentException("La unidad de medida es obligatoria.", nameof(uomCode));
        if (conversionFactor <= 0)
            throw new ArgumentException(
                "El factor de conversión debe ser mayor a cero.",
                nameof(conversionFactor)
            );
        if (string.IsNullOrWhiteSpace(baseUomCode ?? uomCode))
            throw new ArgumentException(
                "La unidad base de inventario es obligatoria.",
                nameof(baseUomCode)
            );
        if (packagingLevelId == Guid.Empty)
            throw new ArgumentException(
                "El nivel de empaque no es válido.",
                nameof(packagingLevelId)
            );

        var line = new SalesReturnDetail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReturnId = returnId,
            OriginalInvoiceDetailId = originalInvoiceDetailId,
            ItemId = itemId,
            WarehouseId = warehouseId,
            PackagingLevelId = packagingLevelId,
            Description = description.Trim(),
            SnapshotSku = snapshotSku?.Trim(),
            SnapshotItemName = snapshotItemName?.Trim(),
            UomCode = uomCode.Trim().ToUpperInvariant(),
            BaseUomCode = (baseUomCode ?? uomCode).Trim().ToUpperInvariant(),
            ConversionFactor = conversionFactor,
            QuantityInBaseUom = Math.Round(
                quantity * conversionFactor,
                FiscalPrecision.Quantity,
                MidpointRounding.AwayFromZero
            ),
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPct = discountPct,
            VatCode = vatCode.Trim(),
            VatRate = vatRate,
            IceCode = OptionalCode.Normalize(iceCode),
            IceRate = iceRate,
            IsFrozen = false,
        };

        line.DiscountAmount =
            discountPct > 0
                ? Math.Round(
                    line.LineSubtotal * discountPct / 100m,
                    FiscalPrecision.UnitCost,
                    MidpointRounding.AwayFromZero
                )
                : 0m;

        (line.IceAmount, line.VatAmount, _) = SriTaxCalculator.Compute(
            line.TaxableBase,
            line.VatRate,
            !string.IsNullOrWhiteSpace(line.IceCode) ? line.IceRate : 0m
        );

        return line;
    }

    // ── Freeze (called once on SalesReturn.Authorize — irreversible) ────
    internal void Freeze()
    {
        IsFrozen = true;
    }
}
