using ERP.Domain.Common;

namespace ERP.Domain.Modules.Cash.Entities;

public sealed class BankStatement : AuditableEntity, ITenantEntity
{
    private readonly List<BankTransaction> _transactions = new();

    public IReadOnlyList<BankTransaction> Transactions => _transactions.AsReadOnly();

    public Guid     BankAccountId   { get; private set; }
    public DateTime PeriodFrom      { get; private set; }
    public DateTime PeriodTo        { get; private set; }
    public decimal  OpeningBalance  { get; private set; }
    public decimal  ClosingBalance  { get; private set; }
    public DateTime LoadedAt        { get; private set; }
    public bool     IsReconciled    { get; private set; }

    private BankStatement() { }

    public static BankStatement Create(
        Guid     tenantId,
        Guid     bankAccountId,
        DateTime periodFrom,
        DateTime periodTo,
        decimal  openingBalance,
        decimal  closingBalance,
        Guid     createdBy)
    {
        if (periodTo < periodFrom)
            throw new ArgumentException("Period end cannot be before period start.");

        var e = new BankStatement
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            BankAccountId  = bankAccountId,
            PeriodFrom     = periodFrom,
            PeriodTo       = periodTo,
            OpeningBalance = openingBalance,
            ClosingBalance = closingBalance,
            LoadedAt       = DateTime.UtcNow,
            IsReconciled   = false,
        };
        e.SetCreated(createdBy);
        return e;
    }

    public void AddTransaction(
        DateTime transactionDate,
        string   description,
        decimal  amount,
        string   transactionType,
        string?  reference,
        Guid     updatedBy)
    {
        var t = BankTransaction.Create(Id, TenantId, transactionDate, description, amount, transactionType, reference);
        _transactions.Add(t);
        IsReconciled = false;
        SetUpdated(updatedBy);
    }

    public void MarkReconciledIfComplete(Guid updatedBy)
    {
        if (_transactions.Count == 0)
            return;
        IsReconciled = _transactions.All(x => x.Status == "Reconciled");
        SetUpdated(updatedBy);
    }

    public void UnmarkReconciled(Guid updatedBy)
    {
        IsReconciled = false;
        SetUpdated(updatedBy);
    }
}
