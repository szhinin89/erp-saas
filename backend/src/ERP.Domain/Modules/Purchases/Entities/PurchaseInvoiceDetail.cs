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
    public const int BaseUomCodeMaxLen = 10;
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
    public Guid? PackagingLevelId { get; private set; }
    public string UomCode { get; private set; } = "UNIT";
    public string BaseUomCode { get; private set; } = "UNIT";
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

    // ── ICE — TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Fase 3) ────────
    // Legacy compatibility mirror: solo lectura, derivados de _taxes — mismo patrón exacto que
    // IrbpnrCode/IrbpnrRate/IrbpnrAmount (abajo). Nunca se escriben directamente; ApplyTaxes()/
    // RecalcTaxes() escriben únicamente a _taxes vía UpsertTaxRow/RemoveTaxRow.
    public string? IceCode => _taxes.FirstOrDefault(t => t.TaxCode == IceSriTaxCode)?.TaxRateCode;
    public decimal IceRate => _taxes.FirstOrDefault(t => t.TaxCode == IceSriTaxCode)?.Rate ?? 0m;
    public decimal IceAmount => _taxes.Where(t => t.TaxCode == IceSriTaxCode).Sum(t => t.TaxAmount);
    public string? SnapshotIceName =>
        _taxes.FirstOrDefault(t => t.TaxCode == IceSriTaxCode)?.TaxName;

    /// <summary>
    /// FLOW-READY-02F.1 — cómo se obtuvo <see cref="IceAmount"/>: <c>Percentage</c> (default,
    /// comportamiento histórico) recalcula siempre <c>TaxableBase * IceRate / 100</c>;
    /// <c>Specific</c> (p. ej. ICE código 3053, bebidas azucaradas) fija <see cref="IceAmount"/> al
    /// monto exacto del XML y nunca lo recalcula desde una tarifa porcentual.
    /// </summary>
    public SriTaxCalculationType IceCalculationType =>
        _taxes.FirstOrDefault(t => t.TaxCode == IceSriTaxCode)?.CalculationType
            ?? SriTaxCalculationType.Percentage;

    // ── Impuestos por línea (FLOW-READY-02F.1) — snapshot fiel del XML, incluye IVA/ICE/IRBPNR ──
    private readonly List<PurchaseInvoiceDetailTax> _taxes = new();
    public IReadOnlyList<PurchaseInvoiceDetailTax> Taxes => _taxes.AsReadOnly();

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

    /// <summary>Monto exacto de IVA tal como vino en el XML (Source=Xml) — snapshot fiel del comprobante.</summary>
    public decimal? XmlVatAmount =>
        _taxes.FirstOrDefault(t => t.TaxCode == VatSriTaxCode && t.Source == PurchaseTaxSource.Xml)
            ?.TaxAmount;

    /// <summary>Monto exacto de ICE tal como vino en el XML (Source=Xml) — snapshot fiel del comprobante.</summary>
    public decimal? XmlIceAmount =>
        _taxes.FirstOrDefault(t => t.TaxCode == IceSriTaxCode && t.Source == PurchaseTaxSource.Xml)
            ?.TaxAmount;

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
            // ConfirmedGrandTotal/AccountsPayable sin tocar esos call sites.
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
        Guid? purchaseReceptionLineId = null,
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
        if (string.IsNullOrWhiteSpace(baseUomCode ?? uomCode))
            throw new ArgumentException(
                "La unidad base de inventario es obligatoria.",
                nameof(baseUomCode)
            );
        if (conversionFactor <= 0)
            throw new ArgumentException(
                "El factor de conversión debe ser mayor a cero.",
                nameof(conversionFactor)
            );
        if (packagingLevelId == Guid.Empty)
            throw new ArgumentException(
                "El nivel de empaque no es válido.",
                nameof(packagingLevelId)
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
        // Mismo criterio que el objeto-initializer anterior: solo el código se conoce en Create()
        // (tarifa/nombre/monto llegan después vía ApplyTaxes) — se registra igual como fila inicial
        // para que IceCode ya sea consultable inmediatamente tras Create(), sin esperar ApplyTaxes.
        var normalizedIceCode = OptionalCode.Normalize(iceCode);
        if (!string.IsNullOrWhiteSpace(normalizedIceCode))
            line.UpsertTaxRow(
                IceSriTaxCode,
                normalizedIceCode,
                "ICE",
                null,
                SriTaxCalculationType.Percentage,
                0m,
                PurchaseTaxSource.Calculated
            );
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

        // FLOW-READY-02F.1 — capturados ANTES de tocar _taxes: si la línea ya tiene un monto exacto
        // documental (Source=Xml) para IVA/ICE, ese monto prevalece sobre cualquier recálculo por
        // tarifa hecho más abajo — evita que redondeos o diferencias de tarifa entre el XML y el
        // catálogo SRI hagan que la compra deje de cuadrar contra el comprobante original. Aplica en
        // Create/Update/Confirm/DistributeCost por igual, sin tocar esos call sites.
        var xmlVatAmount = XmlVatAmount;
        var xmlIceAmount = iceCalculationType == SriTaxCalculationType.Percentage ? XmlIceAmount : null;

        VatCode = vatCode.Trim();
        VatRate = vatRate;
        SnapshotVatName = vatName?.Trim();

        var normalizedIceCode = OptionalCode.Normalize(iceCode);
        var iceSource =
            _taxes.FirstOrDefault(t => t.TaxCode == IceSriTaxCode)?.Source
            ?? PurchaseTaxSource.Calculated;
        if (!string.IsNullOrWhiteSpace(normalizedIceCode))
        {
            // ICE "específico" (p. ej. código 3053) no se recalcula desde una tarifa porcentual — se
            // fija aquí al monto exacto del XML; RecalcTaxes lo preserva y solo recalcula VatAmount
            // sobre él.
            var initialIceAmount =
                iceCalculationType == SriTaxCalculationType.Specific ? (iceExactAmount ?? 0m) : 0m;
            UpsertTaxRow(
                IceSriTaxCode,
                normalizedIceCode,
                string.IsNullOrWhiteSpace(iceName) ? "ICE" : iceName.Trim(),
                iceRate,
                iceCalculationType,
                initialIceAmount,
                iceSource
            );
        }
        else
        {
            RemoveTaxRow(IceSriTaxCode);
        }

        RecalcTaxes();

        if (xmlVatAmount.HasValue)
            VatAmount = xmlVatAmount.Value;
        if (xmlIceAmount.HasValue)
        {
            var iceRow = _taxes.FirstOrDefault(t => t.TaxCode == IceSriTaxCode);
            if (iceRow is not null)
                UpsertTaxRow(
                    IceSriTaxCode,
                    iceRow.TaxRateCode,
                    iceRow.TaxName,
                    iceRow.Rate,
                    iceRow.CalculationType,
                    xmlIceAmount.Value,
                    iceRow.Source
                );
        }

        SyncVatIntoCollection();
    }

    /// <summary>
    /// FLOW-READY-02F.1 — reemplaza el detalle fiel de impuestos de la línea (IVA/ICE/IRBPNR, tal
    /// como vinieron en el XML). Aditivo respecto a VAT: no toca <c>VatCode/VatRate/VatAmount</c>
    /// (siguen actualizándose únicamente vía <see cref="ApplyTaxes"/>) — pero SÍ reemplaza la fila
    /// ICE, que desde ADR-032 §3.3/Fase 3 vive únicamente en <c>_taxes</c> (ya no hay escalar
    /// paralelo que preservar). Se llama junto con <see cref="ApplyTaxes"/> al crear/actualizar una
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
        SyncVatIntoCollection();
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

    /// <summary>
    /// Recalcula <see cref="VatAmount"/> y la fila ICE en <see cref="_taxes"/> a partir del estado
    /// actual (identidad ICE ya presente en <c>_taxes</c> — código/tarifa/nombre/tipo de cálculo —
    /// más <see cref="TaxableBase"/> vigente). No conoce ni necesita conocer quién la invocó
    /// (<see cref="ApplyTaxes"/> tras fijar una identidad nueva, o <see cref="ApplyDiscount"/> tras
    /// cambiar la base imponible): siempre relee la fila ICE actual, nunca un escalar paralelo.
    /// </summary>
    private void RecalcTaxes()
    {
        var iceRow = _taxes.FirstOrDefault(t => t.TaxCode == IceSriTaxCode);
        var iceCalculationType = iceRow?.CalculationType ?? SriTaxCalculationType.Percentage;
        decimal iceAmount;

        if (iceCalculationType == SriTaxCalculationType.Specific)
        {
            // El monto exacto ya quedó fijado (en ApplyTaxes, o en la fila ya existente) al valor
            // del XML — un impuesto específico (p. ej. ICE código 3053, USD por cada 100g de azúcar)
            // no es proporcional a la base imponible ni al descuento, así que nunca se recalcula
            // aquí. Solo se recalcula VatAmount, incluyendo ese ICE ya fijo en la base del IVA
            // (regla SRI: IVA se calcula sobre base + ICE, sin importar cómo se determinó el ICE).
            iceAmount = iceRow?.TaxAmount ?? 0m;
            var vatBase = TaxableBase + iceAmount;
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
            (iceAmount, VatAmount, _) = SriTaxCalculator.Compute(
                TaxableBase,
                VatRate,
                iceRow is not null ? (iceRow.Rate ?? 0m) : 0m
            );
        }

        if (iceRow is not null)
            UpsertTaxRow(
                IceSriTaxCode,
                iceRow.TaxRateCode,
                iceRow.TaxName,
                iceRow.Rate,
                iceCalculationType,
                iceAmount,
                iceRow.Source
            );
    }

    // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3) ────────────────────────
    /// <summary>
    /// Sincroniza el campo escalar VAT hacia su fila correspondiente en <see cref="_taxes"/> —
    /// upsert por <c>TaxCode</c>, nunca toca otras filas (ICE/IRBPNR, cada una con su propio
    /// mecanismo). Preserva el <c>Source</c> ya presente en la fila existente (Xml si la línea vino
    /// de Recepción Electrónica, Calculated si es manual/catálogo) — nunca lo infiere de nuevo aquí.
    /// </summary>
    private void SyncVatIntoCollection()
    {
        var vatSource = _taxes.FirstOrDefault(t => t.TaxCode == VatSriTaxCode)?.Source
            ?? PurchaseTaxSource.Calculated;
        UpsertTaxRow(
            VatSriTaxCode,
            VatCode,
            string.IsNullOrWhiteSpace(SnapshotVatName) ? "IVA" : SnapshotVatName,
            VatRate,
            SriTaxCalculationType.Percentage,
            VatAmount,
            vatSource
        );
    }

    private void UpsertTaxRow(
        string taxCode,
        string taxRateCode,
        string taxName,
        decimal? rate,
        SriTaxCalculationType calculationType,
        decimal taxAmount,
        PurchaseTaxSource source
    )
    {
        _taxes.RemoveAll(t => t.TaxCode == taxCode);
        _taxes.Add(
            PurchaseInvoiceDetailTax.Create(
                Id,
                TenantId,
                taxCode,
                taxRateCode,
                taxName,
                rate,
                calculationType,
                TaxableBase,
                taxAmount,
                source
            )
        );
    }

    private void RemoveTaxRow(string taxCode) => _taxes.RemoveAll(t => t.TaxCode == taxCode);

    private void RecalcCosts()
    {
        TotalLineCost = Math.Round(
            TaxableBase + FreightAllocated + OtherCostsAllocated,
            FiscalPrecision.UnitCost,
            MidpointRounding.AwayFromZero
        );

        LandedUnitCost =
            QuantityInBaseUom > 0
                ? Math.Round(
                    TotalLineCost / QuantityInBaseUom,
                    FiscalPrecision.UnitCost,
                    MidpointRounding.AwayFromZero
                )
                : 0;
    }
}
