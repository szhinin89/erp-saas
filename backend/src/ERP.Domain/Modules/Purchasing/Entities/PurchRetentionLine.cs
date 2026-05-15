using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchRetentionLine : AuditableEntity, ITenantEntity
{
    public const int TaxTypeMaxLen        = 20;
    public const int RetentionCodeMaxLen  = 10;
    public const int RelatedInvoiceMaxLen = 50;

    public Guid    IssuedRetentionId    { get; private set; }
    public string  TaxType              { get; private set; } = null!;
    public string  RetentionCode        { get; private set; } = null!;
    public decimal TaxableBase          { get; private set; }
    public decimal RetentionPct         { get; private set; }
    public decimal AmountRetained       { get; private set; }
    public string? RelatedInvoice       { get; private set; }

    private PurchRetentionLine() { }

    public static PurchRetentionLine Create(
        Guid    tenantId,
        string  taxType,
        string  retentionCode,
        decimal taxableBase,
        decimal retentionPct,
        decimal amountRetained,
        string? relatedInvoice,
        Guid    createdBy)
    {
        var d = new PurchRetentionLine
        {
            Id                = Guid.NewGuid(),
            TenantId          = tenantId,
            IssuedRetentionId = Guid.Empty,
            TaxType           = (taxType ?? string.Empty).Trim().ToUpperInvariant(),
            RetentionCode     = (retentionCode ?? string.Empty).Trim(),
            TaxableBase       = taxableBase,
            RetentionPct      = retentionPct,
            AmountRetained    = amountRetained,
            RelatedInvoice    = string.IsNullOrWhiteSpace(relatedInvoice) ? null : relatedInvoice.Trim(),
        };
        d.SetCreated(createdBy);
        return d;
    }

    public void AssignRetentionId(Guid retentionId)
    {
        if (IssuedRetentionId != Guid.Empty)
            throw new InvalidOperationException("Line is already assigned to a retention.");
        IssuedRetentionId = retentionId;
    }
}
