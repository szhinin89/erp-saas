using ERP.Domain.Common;

namespace ERP.Domain.Modules.Cash.Entities;

public sealed class CashCount : AuditableEntity, ICompanyOperationalEntity
{
    public const int NotesMaxLen = 2000;

    public Guid?   CompanyId    { get; private set; }
    public Guid    PettyCashId  { get; private set; }
    public DateTime CountDate   { get; private set; }
    public decimal PhysicalCash { get; private set; }
    public decimal Difference   { get; private set; }
    public string  Notes        { get; private set; } = "";
    public bool    IsApproved   { get; private set; }

    private CashCount() { }

    public static CashCount Create(
        Guid     subscriberId,
        Guid     pettyCashId,
        DateTime countDate,
        decimal  physicalCash,
        decimal  expectedBalance,
        string?  notes,
        Guid     createdBy,
        Guid?    companyId = null)
    {
        if (physicalCash < 0)
            throw new ArgumentException("Physical cash cannot be negative.", nameof(physicalCash));

        var a = new CashCount
        {
            Id           = Guid.NewGuid(),
            SubscriberId = subscriberId,
            CompanyId    = companyId,
            PettyCashId  = pettyCashId,
            CountDate    = countDate,
            PhysicalCash = physicalCash,
            Difference   = physicalCash - expectedBalance,
            Notes        = notes?.Trim() ?? "",
            IsApproved   = false,
        };
        a.SetCreated(createdBy);
        return a;
    }

    public void Approve(Guid updatedBy)
    {
        if (IsApproved)
            throw new InvalidOperationException("Cash count is already approved.");
        IsApproved = true;
        SetUpdated(updatedBy);
    }
}
