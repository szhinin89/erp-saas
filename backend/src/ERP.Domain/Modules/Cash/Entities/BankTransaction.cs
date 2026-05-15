using ERP.Domain.Common;

namespace ERP.Domain.Modules.Cash.Entities;

public sealed class BankTransaction : BaseEntity
{
    public const int DescriptionMaxLen      = 500;
    public const int TransactionTypeMaxLen  = 20;
    public const int ReferenceMaxLen        = 120;
    public const int StatusMaxLen           = 20;

    public Guid     BankStatementId  { get; private set; }
    public DateTime TransactionDate  { get; private set; }
    public string   Description      { get; private set; } = null!;
    public decimal  Amount           { get; private set; }
    public string   TransactionType  { get; private set; } = null!;
    public string   Reference        { get; private set; } = "";
    public Guid?    JournalEntryId   { get; private set; }
    public string   Status           { get; private set; } = "Pending";

    private BankTransaction() { }

    internal static BankTransaction Create(
        Guid     bankStatementId,
        Guid     tenantId,
        DateTime transactionDate,
        string   description,
        decimal  amount,
        string   transactionType,
        string?  reference)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        if (string.IsNullOrWhiteSpace(transactionType))
            throw new ArgumentException("Transaction type (Debit/Credit) is required.", nameof(transactionType));

        return new BankTransaction
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            BankStatementId = bankStatementId,
            TransactionDate = transactionDate,
            Description     = description.Trim(),
            Amount          = amount,
            TransactionType = transactionType.Trim(),
            Reference       = reference?.Trim() ?? "",
            JournalEntryId  = null,
            Status          = "Pending",
        };
    }

    public void Reconcile(Guid journalEntryId)
    {
        if (Status == "Reconciled")
            throw new InvalidOperationException("Transaction is already reconciled.");
        JournalEntryId = journalEntryId;
        Status         = "Reconciled";
    }

    public void Unreconcile()
    {
        JournalEntryId = null;
        Status         = "Pending";
    }
}
