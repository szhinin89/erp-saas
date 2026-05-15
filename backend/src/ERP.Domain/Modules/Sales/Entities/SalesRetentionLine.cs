using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesRetentionLine : AuditableEntity, ITenantEntity
{
    public const int TaxTypeMaxLen       = 20;
    public const int RetentionCodeMaxLen = 10;

    public Guid    SalesRetentionId  { get; private set; }
    public string  TaxType           { get; private set; } = null!;
    public string  RetentionCode     { get; private set; } = null!;
    public decimal TaxableBase       { get; private set; }
    public decimal RetentionPct      { get; private set; }
    public decimal AmountRetained    { get; private set; }

    private SalesRetentionLine() { }

    public static SalesRetentionLine Create(
        Guid    tenantId,
        string  taxType,
        string  retentionCode,
        decimal taxableBase,
        decimal retentionPct,
        decimal amountRetained,
        Guid    createdBy)
    {
        var d = new SalesRetentionLine
        {
            Id                = Guid.NewGuid(),
            TenantId          = tenantId,
            SalesRetentionId  = Guid.Empty,
            TaxType           = (taxType ?? string.Empty).Trim().ToUpperInvariant(),
            RetentionCode     = (retentionCode ?? string.Empty).Trim(),
            TaxableBase       = taxableBase,
            RetentionPct      = retentionPct,
            AmountRetained    = amountRetained,
        };
        d.SetCreated(createdBy);
        return d;
    }

    public void AssignRetentionId(Guid retentionId)
    {
        if (SalesRetentionId != Guid.Empty)
            throw new InvalidOperationException("Line is already assigned to a retention.");
        SalesRetentionId = retentionId;
    }
}
