using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchaseWithholdingLine : AuditableEntity, ISubscriberScopedEntity
{
    public const int TaxTypeMaxLen       = 20;
    public const int RetentionCodeMaxLen = 10;

    public Guid   PurchaseWithholdingId { get; private set; }
    public string TaxType               { get; private set; } = null!;
    public string RetentionCode         { get; private set; } = null!;
    public decimal TaxableBase          { get; private set; }
    public decimal RetentionPct         { get; private set; }
    public decimal AmountRetained       { get; private set; }

    private PurchaseWithholdingLine() { }

    public static PurchaseWithholdingLine Rehydrate() => new();

    public void AssignWithholdingId(Guid withholdingId)
    {
        if (PurchaseWithholdingId != Guid.Empty)
            throw new InvalidOperationException("Line is already assigned to a withholding.");
        PurchaseWithholdingId = withholdingId;
    }
}
