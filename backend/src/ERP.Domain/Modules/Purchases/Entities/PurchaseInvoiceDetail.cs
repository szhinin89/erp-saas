using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Domain.Modules.Purchases.Entities;

public sealed class PurchaseInvoiceDetail : IMustHaveTenant
{
    public const int DescriptionMaxLen = 300;
    public const int NotesMaxLen = 300;
    public const int VatCodeMaxLen = 10;
    public const int IceCodeMaxLen = 10;
    public const int SkuMaxLen = 50;
    public const int ItemNameMaxLen = 254;
    public const int SupplierCodeMaxLen = 50;
    public const int UomCodeMaxLen = 10;
    public const int VatNameMaxLen = 100;
    public const int IceNameMaxLen = 100;
    public const int WarehouseCodeMaxLen = 20;

    // ── Identity ────────────────────────────────────────────────────────
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }

    // ── Product Snapshot (immutable after creation) ─────────────────────
    public Guid? ItemId { get; private set; }
    public string Description { get; private set; } = null!;
    public string? SnapshotSku { get; private set; }
    public string? SnapshotItemName { get; private set; }
    public string? SnapshotSupplierCode { get; private set; }

    // ── UoM ─────────────────────────────────────────────────────────────
    public string UomCode { get; private set; } = "UNIT";
    public decimal ConversionFactor { get; private set; } = 1m;
    public decimal QuantityInBaseUom { get; private set; }

    // ── Quantity & Price ────────────────────────────────────────────────
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPct { get; private set; }
    public decimal DiscountAmount { get; private set; }

    // ── Distributed Costs ──────────────────────────────────────────────
    public decimal FreightAllocated { get; private set; }
    public decimal OtherCostsAllocated { get; private set; }

    // ── Landed Cost (frozen on Confirm — single source of truth) ───────
    public decimal TotalLineCost { get; private set; }
    public decimal LandedUnitCost { get; private set; }
    public bool IsFrozen { get; private set; }

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

    /// <summary>
    /// FLOW-READY-02F.1 — cómo se obtuvo <see cref="IceAmount"/>: <c>Percentage</c> (default,
    /// comportamiento histórico) recalcula siempre <c>TaxableBase * IceRate / 100</c>;
    /// <c>Specific</c> (p. ej. ICE código 3053, bebidas azucaradas) fija <see cref="IceAmount"/> al
    /// monto exacto del XML y nunca lo recalcula desde una tarifa porcentual.
    /// </summary>
    public SriTaxCalculationType IceCalculationType { get; private set; } =
        SriTaxCalculationType.Percentage;

    // ── Impuestos por línea (FLOW-READY-02F.1) — snapshot fiel del XML, incluye IVA/ICE/IRBPNR ──
    private readonly List<PurchaseInvoiceDetailTax> _taxes = new();
    public IReadOnlyList<PurchaseInvoiceDetailTax> Taxes => _taxes.AsReadOnly();

    private const string IrbpnrSriTaxCode = "5";

    /// <summary>IRBPNR nunca se trata como ICE — código, catálogo y resolución siempre separados.</summary>
    public string? IrbpnrCode =>
        _taxes.FirstOrDefault(t => t.TaxCode == IrbpnrSriTaxCode)?.TaxRateCode;
    public decimal? IrbpnrRate => _taxes.FirstOrDefault(t => t.TaxCode == IrbpnrSriTaxCode)?.Rate;
    public string? SnapshotIrbpnrName =>
        _taxes.FirstOrDefault(t => t.TaxCode == IrbpnrSriTaxCode)?.TaxName;
    public decimal IrbpnrAmount => _taxes.Where(t => t.TaxCode == IrbpnrSriTaxCode).Sum(t => t.TaxAmount);

    // ── Warehouse (logistic reference) ──────────────────────────────────
    public Guid? WarehouseId { get; private set; }
    public string? SnapshotWarehouseCode { get; private set; }

    // ── Analytic Snapshot (PVP at purchase time — read-only history) ────
    public decimal SnapshotItemPvp { get; private set; }

    // ── Purchase Order Traceability ─────────────────────────────────────
    public Guid? PurchaseOrderDetailId { get; private set; }
    public decimal? OrderedQuantity { get; private set; }

    // ── Purchase Reception Traceability ─────────────────────────────────
    /// <summary>
    /// Id de la <c>PurchaseReceptionLine</c> de origen cuando esta línea se precargó desde
    /// Recepción Electrónica — null en líneas manuales. Permite que la pantalla de Compras
    /// reutilice el mismo backend de Item Matching de Recepción (ADR-028) para vincular/
    /// desvincular sin duplicar lógica. No implica ningún vínculo vivo de datos: el resto de
    /// los campos de esta línea siguen siendo una copia congelada al momento de crear/actualizar
    /// el borrador, igual que el resto del "Product Snapshot".
    /// </summary>
    public Guid? PurchaseReceptionLineId { get; private set; }

    // ── Meta ────────────────────────────────────────────────────────────
    public string? Notes { get; private set; }
    public short SortOrder { get; private set; }

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
            // FLOW-READY-02F.2 — IRBPNR forma parte del valor real del XML y de la cuenta por
            // pagar al proveedor: se incluye aquí, propagando automáticamente a GrandTotal/
            // ConfirmedGrandTotal/PurchasePayable sin tocar esos call sites.
            TaxableBase + IceAmount + VatAmount + IrbpnrAmount,
            FiscalPrecision.TaxAmount,
            MidpointRounding.AwayFromZero
        );

    // ── Constructor ─────────────────────────────────────────────────────
    private PurchaseInvoiceDetail() { }

    // ── Factory ─────────────────────────────────────────────────────────
    public static PurchaseInvoiceDetail Create(
        Guid invoiceId,
        Guid tenantId,
        string description,
        decimal quantity,
        decimal unitPrice,
        string vatCode,
        string uomCode,
        Guid? itemId = null,
        Guid? warehouseId = null,
        string? notes = null,
        decimal discountPct = 0,
        string? iceCode = null,
        string? snapshotSku = null,
        string? snapshotItemName = null,
        string? snapshotSupplierCode = null,
        decimal conversionFactor = 1m,
        string? snapshotWarehouseCode = null,
        Guid? purchaseOrderDetailId = null,
        decimal? orderedQuantity = null,
        Guid? purchaseReceptionLineId = null
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

        var line = new PurchaseInvoiceDetail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoiceId,
            ItemId = itemId,
            Description = description.Trim(),
            SnapshotSku = snapshotSku?.Trim(),
            SnapshotItemName = snapshotItemName?.Trim(),
            SnapshotSupplierCode = snapshotSupplierCode?.Trim(),
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
            WarehouseId = warehouseId,
            SnapshotWarehouseCode = snapshotWarehouseCode?.Trim(),
            PurchaseOrderDetailId = purchaseOrderDetailId,
            OrderedQuantity = orderedQuantity,
            PurchaseReceptionLineId = purchaseReceptionLineId,
            Notes = notes?.Trim(),
            IsFrozen = false,
        };
        line.RecalcDiscount();
        line.RecalcCosts();
        return line;
    }

    // ── Tax Application ─────────────────────────────────────────────────
    public void ApplyTaxes(
        string vatCode,
        decimal vatRate,
        string? vatName,
        string? iceCode,
        decimal iceRate,
        string? iceName,
        SriTaxCalculationType iceCalculationType = SriTaxCalculationType.Percentage,
        decimal? iceExactAmount = null
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
        IceCalculationType = iceCalculationType;
        // ICE "específico" (p. ej. código 3053) no se recalcula desde una tarifa porcentual — se fija
        // aquí al monto exacto del XML; RecalcTaxes lo preserva y solo recalcula VatAmount sobre él.
        if (iceCalculationType == SriTaxCalculationType.Specific)
            IceAmount = iceExactAmount ?? 0m;
        RecalcTaxes();
    }

    /// <summary>
    /// FLOW-READY-02F.1 — reemplaza el detalle fiel de impuestos de la línea (IVA/ICE/IRBPNR, tal
    /// como vinieron en el XML). Aditivo: no toca los campos escalares legacy (VatCode/VatRate/
    /// VatAmount, IceCode/IceRate/IceAmount) — esos siguen actualizándose únicamente vía
    /// <see cref="ApplyTaxes"/>. Se llama junto con <see cref="ApplyTaxes"/> al crear/actualizar una
    /// línea desde Recepción Electrónica.
    /// </summary>
    public void ReplaceTaxes(IEnumerable<PurchaseInvoiceDetailTax> taxes)
    {
        EnsureNotFrozen();
        _taxes.Clear();
        _taxes.AddRange(taxes);
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
        RecalcCosts();
    }

    // ── Freight & Other Costs ───────────────────────────────────────────
    public void SetFreightAllocated(decimal amount)
    {
        EnsureNotFrozen();
        if (amount < 0)
            throw new ArgumentException("El flete asignado no puede ser negativo.", nameof(amount));
        FreightAllocated = amount;
        RecalcCosts();
    }

    public void SetOtherCostsAllocated(decimal amount)
    {
        EnsureNotFrozen();
        if (amount < 0)
            throw new ArgumentException(
                "Los otros costos asignados no pueden ser negativos.",
                nameof(amount)
            );
        OtherCostsAllocated = amount;
        RecalcCosts();
    }

    // ── Analytic PVP Snapshot ───────────────────────────────────────────
    public void SetItemPvpSnapshot(decimal pvp)
    {
        EnsureNotFrozen();
        if (pvp < 0)
            throw new ArgumentException("El PVP no puede ser negativo.", nameof(pvp));
        SnapshotItemPvp = pvp;
    }

    // ── Sort ────────────────────────────────────────────────────────────
    internal void SetSortOrder(short order) => SortOrder = order;

    // ── Freeze (called once on Confirm — irreversible) ─────────────────
    internal void FreezeCosts()
    {
        if (IsFrozen)
            return;
        RecalcCosts();
        IsFrozen = true;
    }

    // ── Invariant Guards ────────────────────────────────────────────────
    private void EnsureNotFrozen()
    {
        if (IsFrozen)
            throw new InvalidOperationException(
                "La línea de compra está confirmada y no puede ser modificada."
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
        if (IceCalculationType == SriTaxCalculationType.Specific)
        {
            // IceAmount ya quedó fijado en ApplyTaxes al monto exacto del XML — un impuesto
            // específico (p. ej. ICE código 3053, USD por cada 100g de azúcar) no es proporcional a
            // la base imponible ni al descuento, así que nunca se recalcula aquí. Solo se recalcula
            // VatAmount, incluyendo ese ICE ya fijo en la base del IVA (regla SRI: IVA se calcula
            // sobre base + ICE, sin importar cómo se determinó el ICE).
            var vatBase = TaxableBase + IceAmount;
            VatAmount =
                VatRate > 0
                    ? Math.Round(
                        vatBase * VatRate / 100m,
                        FiscalPrecision.TaxAmount,
                        MidpointRounding.AwayFromZero
                    )
                    : 0m;
        }
        else
        {
            (IceAmount, VatAmount, _) = SriTaxCalculator.Compute(
                TaxableBase,
                VatRate,
                !string.IsNullOrWhiteSpace(IceCode) ? IceRate : 0m
            );
        }
    }

    private void RecalcCosts()
    {
        TotalLineCost = Math.Round(
            TaxableBase + FreightAllocated + OtherCostsAllocated,
            FiscalPrecision.UnitCost,
            MidpointRounding.AwayFromZero
        );

        LandedUnitCost =
            Quantity > 0
                ? Math.Round(
                    TotalLineCost / Quantity,
                    FiscalPrecision.UnitCost,
                    MidpointRounding.AwayFromZero
                )
                : 0;
    }
}
