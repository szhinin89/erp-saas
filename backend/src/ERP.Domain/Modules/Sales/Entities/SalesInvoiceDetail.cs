using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;

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
    public Guid? PackagingLevelId { get; private set; }
    public string UomCode { get; private set; } = "UNIT";
    public string BaseUomCode { get; private set; } = "UNIT";
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

    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3) — paridad con
    /// <see cref="ERP.Domain.Modules.Purchases.Entities.PurchaseInvoiceDetail.IceCalculationType"/>:
    /// <c>Percentage</c> (default) recalcula siempre <c>TaxableBase * IceRate / 100</c>; <c>Specific</c>
    /// fija <see cref="IceAmount"/> al monto exacto resuelto (p. ej. tarifa por unidad del catálogo
    /// SRI) y nunca lo recalcula desde una tarifa porcentual.
    /// </summary>
    public SriTaxCalculationType IceCalculationType { get; private set; } =
        SriTaxCalculationType.Percentage;

    // ── Impuestos por línea (ADR-032 §3.3) — fuente de verdad; IVA/ICE/IRBPNR ──────────────────
    private readonly List<SalesInvoiceDetailTax> _taxes = new();
    public IReadOnlyList<SalesInvoiceDetailTax> Taxes => _taxes.AsReadOnly();

    private const string IrbpnrSriTaxCode = SriTaxCategoryCodes.Irbpnr;
    private const string VatSriTaxCode = SriTaxCategoryCodes.Vat;
    private const string IceSriTaxCode = SriTaxCategoryCodes.Ice;

    /// <summary>IRBPNR nunca se trata como ICE — código, catálogo y resolución siempre separados.</summary>
    public string? IrbpnrCode =>
        _taxes.FirstOrDefault(t => t.TaxCode == IrbpnrSriTaxCode)?.TaxRateCode;
    public decimal? IrbpnrRate => _taxes.FirstOrDefault(t => t.TaxCode == IrbpnrSriTaxCode)?.Rate;
    public string? SnapshotIrbpnrName =>
        _taxes.FirstOrDefault(t => t.TaxCode == IrbpnrSriTaxCode)?.TaxName;
    public decimal IrbpnrAmount => _taxes.Where(t => t.TaxCode == IrbpnrSriTaxCode).Sum(t => t.TaxAmount);

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
            // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3) — paridad con Compras: IRBPNR forma parte
            // del valor real de la línea y del total de la factura.
            TaxableBase + IceAmount + VatAmount + IrbpnrAmount,
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
        Guid? warehouseId = null,
        string? baseUomCode = null,
        Guid? packagingLevelId = null
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
            PackagingLevelId = packagingLevelId,
            UomCode = uomCode.Trim().ToUpperInvariant(),
            BaseUomCode = (baseUomCode ?? uomCode).Trim().ToUpperInvariant(),
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
    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3) — extendido con <paramref name="iceCalculationType"/>/
    /// <paramref name="iceExactAmount"/> para paridad con
    /// <see cref="ERP.Domain.Modules.Purchases.Entities.PurchaseInvoiceDetail.ApplyTaxes"/> (cierra el
    /// gap de ICE "específico" en Ventas). Cambio de contrato sobre infraestructura FROZEN
    /// (docs/architecture/frozen-infrastructure.md § Configuración Tributaria, Regla 4) amparado por
    /// ADR-032 — parámetros nuevos con default, ningún caller existente requiere cambios.
    /// </summary>
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
        // Paridad con Compras: un ICE "específico" no se recalcula desde una tarifa porcentual — se
        // fija aquí al monto exacto resuelto; RecalcTaxes lo preserva y solo recalcula VatAmount.
        if (iceCalculationType == SriTaxCalculationType.Specific)
            IceAmount = iceExactAmount ?? 0m;
        RecalcTaxes();

        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3) — SalesInvoiceDetailTax es la fuente de verdad
        // desde el día uno: toda escritura de IVA/ICE sincroniza aquí su fila, sin tocar otras filas
        // ya presentes (p. ej. IRBPNR, poblado aparte vía ReplaceTaxes por el consumidor de Ventas).
        // Los campos escalares de arriba quedan como legacy compatibility mirror.
        SyncScalarTaxesIntoCollection();
    }

    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3) — espejo de
    /// <see cref="ERP.Domain.Modules.Purchases.Entities.PurchaseInvoiceDetail.ReplaceTaxes"/>: fija
    /// impuestos que <see cref="ApplyTaxes"/> no administra por código fijo (hoy, IRBPNR — sin campos
    /// escalares legacy propios). Reemplaza únicamente las filas de los códigos presentes en
    /// <paramref name="taxes"/> — nunca toca VAT/ICE, administrados exclusivamente por
    /// <see cref="ApplyTaxes"/>/<see cref="SyncScalarTaxesIntoCollection"/>. Llamar antes de
    /// <see cref="ApplyTaxes"/> si ambos se invocan en el mismo ciclo (mismo orden que Compras).
    /// </summary>
    public void ReplaceTaxes(IEnumerable<SalesInvoiceDetailTax> taxes)
    {
        EnsureNotFrozen();
        foreach (var code in taxes.Select(t => t.TaxCode).Distinct())
            RemoveTaxRow(code);
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
        SyncScalarTaxesIntoCollection();
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
        SyncScalarTaxesIntoCollection();
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
        if (IceCalculationType == SriTaxCalculationType.Specific)
        {
            // Paridad con Compras — un impuesto específico (p. ej. ICE código 3053, USD por cada
            // 100g de azúcar) no es proporcional a la base imponible ni al descuento: IceAmount ya
            // quedó fijado en ApplyTaxes y nunca se recalcula aquí. Solo se recalcula VatAmount,
            // incluyendo ese ICE ya fijo en la base del IVA (regla SRI: IVA se calcula sobre base +
            // ICE, sin importar cómo se determinó el ICE).
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

    // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3) ────────────────────────
    /// <summary>
    /// Sincroniza los campos escalares IVA/ICE (legacy compatibility mirror) hacia su fila
    /// correspondiente en <see cref="_taxes"/> — upsert por <c>TaxCode</c>, nunca toca otras filas
    /// (p. ej. IRBPNR, poblado aparte vía <see cref="ReplaceTaxes"/>).
    /// </summary>
    private void SyncScalarTaxesIntoCollection()
    {
        UpsertTaxRow(
            VatSriTaxCode,
            VatCode,
            string.IsNullOrWhiteSpace(SnapshotVatName) ? "IVA" : SnapshotVatName,
            VatRate,
            SriTaxCalculationType.Percentage,
            VatAmount
        );

        if (!string.IsNullOrWhiteSpace(IceCode))
        {
            var existingIce = _taxes.FirstOrDefault(t => t.TaxCode == IceSriTaxCode);
            UpsertTaxRow(
                IceSriTaxCode,
                IceCode,
                string.IsNullOrWhiteSpace(SnapshotIceName) ? "ICE" : SnapshotIceName,
                IceCalculationType == SriTaxCalculationType.Percentage ? IceRate : existingIce?.Rate,
                IceCalculationType,
                IceAmount
            );
        }
        else
        {
            RemoveTaxRow(IceSriTaxCode);
        }
    }

    private void UpsertTaxRow(
        string taxCode,
        string taxRateCode,
        string taxName,
        decimal? rate,
        SriTaxCalculationType calculationType,
        decimal taxAmount
    )
    {
        _taxes.RemoveAll(t => t.TaxCode == taxCode);
        _taxes.Add(
            SalesInvoiceDetailTax.Create(
                Id,
                TenantId,
                taxCode,
                taxRateCode,
                taxName,
                rate,
                calculationType,
                TaxableBase,
                taxAmount,
                SalesTaxSource.Calculated
            )
        );
    }

    private void RemoveTaxRow(string taxCode) => _taxes.RemoveAll(t => t.TaxCode == taxCode);
}
