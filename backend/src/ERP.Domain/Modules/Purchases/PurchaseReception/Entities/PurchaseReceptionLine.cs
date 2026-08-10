using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

/// <summary>
/// Línea de detalle de un <see cref="PurchaseReceptionDocument"/>, extraída del XML autorizado del
/// comprobante al momento de la verificación SRI (ver <c>DownloadPurchaseReceptionXmlHandler</c>).
/// <see cref="ItemId"/>/<see cref="MatchStatus"/> representan la conciliación contra el catálogo de
/// Items — resuelta automáticamente por código de proveedor exacto, o manualmente por el usuario
/// (individual o en lote) vía el motor de Item Matching.
/// </summary>
public sealed class PurchaseReceptionLine : IMustHaveTenant
{
    public const int SupplierCodeMaxLen = 50;
    public const int DescriptionMaxLen = 300;
    public const int VatCodeMaxLen = 10;
    public const int TaxCodeMaxLen = 10;
    public const int IceCodeMaxLen = 10;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PurchaseReceptionDocumentId { get; private set; }

    public string? SupplierCode { get; private set; }
    public string? SupplierAuxCode { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    /// <summary>
    /// Snapshot tributario y de descuento extraído del XML autorizado — persistido para que
    /// <c>CreatePurchaseReceptionDraftHandler</c> nunca necesite volver a parsear el XML.
    /// </summary>
    public string VatCode { get; private set; } = null!;
    public string TaxCode { get; private set; } = null!;
    public decimal VatPercentage { get; private set; }
    public decimal TaxValue { get; private set; }
    public string? IceCode { get; private set; }
    public decimal IceValue { get; private set; }
    public decimal DiscountPct { get; private set; }
    public decimal Discount { get; private set; }
    public decimal LineSubtotal { get; private set; }
    public decimal TotalLine { get; private set; }

    /// <summary>Ítem del ERP ya conciliado con esta línea — null mientras <see cref="MatchStatus"/> no sea Auto/ManuallyMatched.</summary>
    public Guid? ItemId { get; private set; }

    public ItemMatchStatus MatchStatus { get; private set; } = ItemMatchStatus.Pending;
    public DateTime? MatchedAt { get; private set; }
    public Guid? MatchedBy { get; private set; }

    /// <summary>
    /// FLOW-READY-02F.1 — snapshot fiel de todo &lt;impuesto&gt; del XML (IVA/ICE/IRBPNR), incluyendo
    /// códigos que <see cref="VatCode"/>/<see cref="IceCode"/> no representan (p. ej. IRBPNR). Se
    /// persiste aquí porque "Crear compra" puede ocurrir mucho después del parseo del XML.
    /// </summary>
    private readonly List<PurchaseReceptionLineTax> _taxes = new();
    public IReadOnlyList<PurchaseReceptionLineTax> Taxes => _taxes.AsReadOnly();

    private PurchaseReceptionLine() { }

    public static PurchaseReceptionLine Create(
        Guid documentId,
        Guid tenantId,
        string description,
        decimal quantity,
        decimal unitPrice,
        string vatCode,
        string taxCode,
        decimal vatPercentage,
        decimal taxValue,
        decimal discountPct,
        decimal discount,
        decimal lineSubtotal,
        decimal totalLine,
        string? iceCode = null,
        decimal iceValue = 0m,
        string? supplierCode = null,
        string? supplierAuxCode = null,
        Guid? itemId = null,
        ItemMatchStatus matchStatus = ItemMatchStatus.Pending,
        IEnumerable<(string TaxCode, string TaxRateCode, decimal Tarifa, decimal TaxableBase, decimal TaxAmount)>? taxes =
            null
    )
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException(
                "El documento de recepción es obligatorio.",
                nameof(documentId)
            );
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("La descripción es obligatoria.", nameof(description));
        if (quantity <= 0)
            throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(quantity));
        if (unitPrice < 0)
            throw new ArgumentException(
                "El precio unitario no puede ser negativo.",
                nameof(unitPrice)
            );
        if (string.IsNullOrWhiteSpace(vatCode))
            throw new ArgumentException("El código de IVA es obligatorio.", nameof(vatCode));
        if (string.IsNullOrWhiteSpace(taxCode))
            throw new ArgumentException("El código de impuesto es obligatorio.", nameof(taxCode));
        if (
            matchStatus is ItemMatchStatus.AutoMatched or ItemMatchStatus.ManuallyMatched
            && itemId is null
        )
            throw new ArgumentException(
                "Un estado de conciliación resuelto requiere un ítem.",
                nameof(itemId)
            );

        var line = new PurchaseReceptionLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseReceptionDocumentId = documentId,
            SupplierCode = supplierCode?.Trim(),
            SupplierAuxCode = supplierAuxCode?.Trim(),
            Description = description.Trim(),
            Quantity = quantity,
            UnitPrice = unitPrice,
            VatCode = vatCode.Trim(),
            TaxCode = taxCode.Trim(),
            VatPercentage = vatPercentage,
            TaxValue = taxValue,
            IceCode = iceCode?.Trim(),
            IceValue = iceValue,
            DiscountPct = discountPct,
            Discount = discount,
            LineSubtotal = lineSubtotal,
            TotalLine = totalLine,
            ItemId = itemId,
            MatchStatus = matchStatus,
        };

        if (taxes is not null)
        {
            foreach (var t in taxes)
                line._taxes.Add(
                    PurchaseReceptionLineTax.Create(
                        line.Id,
                        tenantId,
                        t.TaxCode,
                        t.TaxRateCode,
                        t.Tarifa,
                        t.TaxableBase,
                        t.TaxAmount
                    )
                );
        }

        return line;
    }

    /// <summary>Resolución automática al persistir la línea — solo por código de proveedor exacto, sin intervención del usuario.</summary>
    public void AutoMatch(Guid itemId)
    {
        if (itemId == Guid.Empty)
            throw new ArgumentException("El ítem es obligatorio.", nameof(itemId));

        ItemId = itemId;
        MatchStatus = ItemMatchStatus.AutoMatched;
        MatchedAt = null;
        MatchedBy = null;
    }

    /// <summary>Marca la línea con sugerencias pendientes de revisión humana (sin código exacto).</summary>
    public void MarkNeedsReview()
    {
        if (MatchStatus is ItemMatchStatus.AutoMatched or ItemMatchStatus.ManuallyMatched)
            return;
        MatchStatus = ItemMatchStatus.NeedsReview;
    }

    /// <summary>
    /// Confirmación del usuario — individual o en lote. Permite re-vincular una línea ya
    /// conciliada (el usuario puede corregir una sugerencia automática o una elección anterior).
    /// </summary>
    public void ManualMatch(Guid itemId, Guid matchedBy, DateTime matchedAtUtc)
    {
        if (itemId == Guid.Empty)
            throw new ArgumentException("El ítem es obligatorio.", nameof(itemId));
        if (matchedBy == Guid.Empty)
            throw new ArgumentException(
                "El usuario que concilia es obligatorio.",
                nameof(matchedBy)
            );

        ItemId = itemId;
        MatchStatus = ItemMatchStatus.ManuallyMatched;
        MatchedAt = matchedAtUtc;
        MatchedBy = matchedBy;
    }

    /// <summary>
    /// Revierte una conciliación resuelta incorrectamente — Auto o Manual — de vuelta a
    /// <see cref="ItemMatchStatus.Pending"/>, para que el usuario pueda rehacer el matching desde
    /// cero. No toca ningún campo del snapshot tributario/XML: solo <see cref="ItemId"/>,
    /// <see cref="MatchStatus"/>, <see cref="MatchedAt"/> y <see cref="MatchedBy"/>.
    /// </summary>
    public void UnmatchItem()
    {
        if (
            MatchStatus is not (ItemMatchStatus.AutoMatched or ItemMatchStatus.ManuallyMatched)
            || ItemId is null
        )
            throw new InvalidOperationException(
                "La línea no tiene un ítem asociado para desvincular."
            );

        ItemId = null;
        MatchStatus = ItemMatchStatus.Pending;
        MatchedAt = null;
        MatchedBy = null;
    }
}
