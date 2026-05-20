using ERP.Domain.Common;

namespace ERP.Domain.Modules.Cash.Entities;

public sealed class PettyCash : MasterEntity, ISubscriberScopedEntity
{
    public const int NameMaxLen = 200;

    public string  Name                    { get; private set; } = null!;
    public decimal AssignedBalance         { get; private set; }
    public decimal CurrentBalance          { get; private set; }
    public Guid?   ReplenishBankAccountId  { get; private set; }
    public Guid?   LedgerAccountId         { get; private set; }

    private PettyCash() { }

    public static PettyCash Create(
        Guid     subscriberId,
        string   name,
        decimal  assignedBalance,
        Guid     createdBy,
        Guid?    replenishBankAccountId = null,
        Guid?    ledgerAccountId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (assignedBalance < 0)
            throw new ArgumentException("Assigned balance cannot be negative.", nameof(assignedBalance));

        var c = new PettyCash
        {
            Id                   = Guid.NewGuid(),
            SubscriberId             = subscriberId,
            Name                 = name.Trim(),
            AssignedBalance      = assignedBalance,
            CurrentBalance       = assignedBalance,
            ReplenishBankAccountId = replenishBankAccountId,
            LedgerAccountId      = ledgerAccountId,
        };
        c.SetCreated(createdBy);
        return c;
    }

    public void Update(
        string   name,
        decimal  assignedBalance,
        Guid?    replenishBankAccountId,
        Guid?    ledgerAccountId,
        Guid     updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name                   = name.Trim();
        AssignedBalance        = assignedBalance;
        ReplenishBankAccountId = replenishBankAccountId;
        LedgerAccountId        = ledgerAccountId;
        SetUpdated(updatedBy);
    }

    public void RegisterExpense(decimal amount, Guid updatedBy)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        if (CurrentBalance < amount)
            throw new InvalidOperationException("Insufficient balance in petty cash.");
        CurrentBalance -= amount;
        SetUpdated(updatedBy);
    }

    public void RegisterReplenishment(decimal amount, Guid updatedBy)
    {
        if (amount <= 0)
            throw new ArgumentException("Replenishment amount must be greater than zero.", nameof(amount));
        CurrentBalance += amount;
        SetUpdated(updatedBy);
    }

    public void ApplyBalanceFromCount(decimal physicalCash, Guid updatedBy)
    {
        if (physicalCash < 0)
            throw new ArgumentException("Physical cash cannot be negative.", nameof(physicalCash));
        CurrentBalance = physicalCash;
        SetUpdated(updatedBy);
    }
}
