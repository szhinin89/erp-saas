namespace ERP.Application.Modules.Purchases.DTOs;

public sealed class PurchaseItemContextDto
{
    // ── ITEM DATA ────────────────────────────────────────────────
    public Guid ItemId { get; init; }
    public string Sku { get; init; } = default!;
    public string ShortName { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string? SupplierCode { get; init; }

    // ── STOCK (SSOT) ─────────────────────────────────────────────
    public decimal CurrentStock { get; init; }
    public decimal AvailableStock { get; init; }
    public decimal ReservedStock { get; init; }

    // ── COSTS (SSOT INVENTORY) ───────────────────────────────────
    public decimal AverageCost { get; init; }
    public decimal LastPurchaseCost { get; init; }

    // ── PRICING (SALES SIDE) ─────────────────────────────────────
    public decimal Pvp { get; init; }
    public decimal PreviousPrice { get; init; }
    public decimal MaxDiscountPercent { get; init; }

    // ── TAXES ─────────────────────────────────────────────────────
    public string? PurchaseVatCode { get; init; }
    public decimal VatPercent { get; init; }
    public string? ExciseTaxCode { get; init; }
    public decimal IcePercent { get; init; }
    public bool HasVat { get; init; }
    public bool HasIce { get; init; }

    // ── CALCULATED VALUES ────────────────────────────────────────
    public decimal CostMargin { get; init; }
    public decimal CostMarginPercent { get; init; }
}
