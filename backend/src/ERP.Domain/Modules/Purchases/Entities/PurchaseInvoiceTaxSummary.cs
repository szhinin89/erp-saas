using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// FLOW-READY-02D.1 — resumen fiscal persistido de una <see cref="PurchaseInvoice"/> confirmada,
/// agrupado por combinación de impuesto (VatCode/VatRate/IceCode/IceRate). Fuente única: snapshots
/// ya congelados de <see cref="PurchaseInvoiceDetail"/> (<c>TaxableBase</c>/<c>VatAmount</c>/
/// <c>IceAmount</c>/<c>SnapshotVatName</c>/<c>SnapshotIceName</c>) — nunca recalcula desde catálogos
/// vivos ni reimplementa <see cref="SriTaxCalculator"/>. Regenerado íntegramente y exclusivamente
/// por <see cref="PurchaseInvoice"/> al confirmar (nunca editable de forma independiente — sin
/// factory pública, sin métodos de mutación).
/// </summary>
public sealed class PurchaseInvoiceTaxSummary : ICompanyOperationalEntity
{
    public const int VatCodeMaxLen = 10;
    public const int IceCodeMaxLen = 10;
    public const int VatNameMaxLen = 100;
    public const int IceNameMaxLen = 100;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }

    /// <summary>Snapshot de <see cref="PurchaseInvoice.BranchId"/> — Branch Ownership Rule, nunca mutable.</summary>
    public Guid BranchId { get; private set; }

    public Guid PurchaseInvoiceId { get; private set; }

    // ── Identidad del grupo de impuesto (clave de agrupación) ────────────
    public string VatCode { get; private set; } = null!;
    public decimal VatRate { get; private set; }
    public string? VatName { get; private set; }

    public string? IceCode { get; private set; }
    public decimal IceRate { get; private set; }
    public string? IceName { get; private set; }

    /// <summary>
    /// FLOW-READY-02F.1 — dimensión adicional de la clave de agrupación. Para facturas sin IRBPNR
    /// (100% de los datos históricos) es siempre null/0 en todas las líneas, así que no cambia el
    /// agrupamiento existente por (VatCode, VatRate, IceCode, IceRate).
    /// </summary>
    public string? IrbpnrCode { get; private set; }
    public decimal IrbpnrRate { get; private set; }
    public string? IrbpnrName { get; private set; }

    // ── Montos agregados (suma de líneas del grupo, snapshot congelado) ──
    public decimal TaxableBase { get; private set; }
    public decimal IceAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal IrbpnrAmount { get; private set; }
    public decimal TotalAmount { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private PurchaseInvoiceTaxSummary() { }

    /// <summary>
    /// Factory interno — invocable únicamente desde <see cref="PurchaseInvoice"/> (mismo assembly,
    /// misma agregación). No hay forma pública de construir ni editar un resumen de forma
    /// independiente: la única vía es <c>PurchaseInvoice.Confirm()</c>.
    /// </summary>
    internal static PurchaseInvoiceTaxSummary Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid purchaseInvoiceId,
        string vatCode,
        decimal vatRate,
        string? vatName,
        string? iceCode,
        decimal iceRate,
        string? iceName,
        decimal taxableBase,
        decimal iceAmount,
        decimal vatAmount,
        string? irbpnrCode = null,
        decimal irbpnrRate = 0m,
        string? irbpnrName = null,
        decimal irbpnrAmount = 0m
    )
    {
        if (purchaseInvoiceId == Guid.Empty)
            throw new ArgumentException(
                "La factura de compra es obligatoria.",
                nameof(purchaseInvoiceId)
            );
        if (string.IsNullOrWhiteSpace(vatCode))
            throw new ArgumentException("El código IVA es obligatorio.", nameof(vatCode));

        return new PurchaseInvoiceTaxSummary
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            PurchaseInvoiceId = purchaseInvoiceId,
            VatCode = vatCode.Trim(),
            VatRate = vatRate,
            VatName = string.IsNullOrWhiteSpace(vatName) ? null : vatName.Trim(),
            IceCode = OptionalCode.Normalize(iceCode),
            IceRate = iceRate,
            IceName = string.IsNullOrWhiteSpace(iceName) ? null : iceName.Trim(),
            IrbpnrCode = OptionalCode.Normalize(irbpnrCode),
            IrbpnrRate = irbpnrRate,
            IrbpnrName = string.IsNullOrWhiteSpace(irbpnrName) ? null : irbpnrName.Trim(),
            TaxableBase = taxableBase,
            IceAmount = iceAmount,
            VatAmount = vatAmount,
            IrbpnrAmount = irbpnrAmount,
            TotalAmount = taxableBase + iceAmount + vatAmount + irbpnrAmount,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
