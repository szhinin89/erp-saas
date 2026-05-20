using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesWithholdingLine : AuditableEntity, ITenantEntity
{
    public const int TaxTypeMaxLen       = 20;
    public const int RetentionCodeMaxLen = 10;

    public Guid   SalesWithholdingId { get; private set; }
    public string TaxType            { get; private set; } = null!;
    public string RetentionCode      { get; private set; } = null!;
    public decimal TaxableBase       { get; private set; }
    public decimal RetentionPct      { get; private set; }
    public decimal AmountRetained    { get; private set; }

    private SalesWithholdingLine() { }

    public static SalesWithholdingLine Rehydrate() => new();

    public void AssignWithholdingId(Guid withholdingId)
    {
        if (SalesWithholdingId != Guid.Empty)
            throw new InvalidOperationException("Line is already assigned to a withholding.");
        SalesWithholdingId = withholdingId;
    }
}
