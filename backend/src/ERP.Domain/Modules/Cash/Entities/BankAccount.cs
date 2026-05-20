using ERP.Domain.Common;

namespace ERP.Domain.Modules.Cash.Entities;

public sealed class BankAccount : MasterEntity, ISubscriberScopedEntity
{
    public const int NameMaxLen          = 200;
    public const int AccountNumberMaxLen = 40;
    public const int AccountTypeMaxLen   = 40;
    public const int CurrencyMaxLen      = 3;

    public string  Name            { get; private set; } = null!;
    public string  AccountNumber   { get; private set; } = null!;
    public string  AccountType     { get; private set; } = null!;
    public string  Currency        { get; private set; } = "USD";
    public decimal InitialBalance  { get; private set; }
    public decimal CurrentBalance  { get; private set; }
    public Guid?   LedgerAccountId { get; private set; }

    private BankAccount() { }

    public static BankAccount Create(
        Guid     subscriberId,
        string   name,
        string   accountNumber,
        string   accountType,
        string   currency,
        decimal  initialBalance,
        Guid     createdBy,
        Guid?    ledgerAccountId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("Account number is required.", nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(accountType))
            throw new ArgumentException("Account type is required.", nameof(accountType));

        var curr = (currency ?? "USD").Trim().ToUpperInvariant();
        if (curr.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));

        var c = new BankAccount
        {
            Id              = Guid.NewGuid(),
            SubscriberId        = subscriberId,
            Name            = name.Trim(),
            AccountNumber   = accountNumber.Trim(),
            AccountType     = accountType.Trim(),
            Currency        = curr,
            InitialBalance  = initialBalance,
            CurrentBalance  = initialBalance,
            LedgerAccountId = ledgerAccountId,
        };
        c.SetCreated(createdBy);
        return c;
    }

    public void Update(
        string   name,
        string   accountNumber,
        string   accountType,
        string   currency,
        Guid?    ledgerAccountId,
        Guid     updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name            = name.Trim();
        AccountNumber   = accountNumber.Trim();
        AccountType     = accountType.Trim();
        Currency        = (currency ?? "USD").Trim().ToUpperInvariant();
        LedgerAccountId = ledgerAccountId;
        SetUpdated(updatedBy);
    }

    public void AdjustBalance(decimal balance, Guid updatedBy)
    {
        CurrentBalance = balance;
        SetUpdated(updatedBy);
    }

    public void IncrementBalance(decimal delta, Guid updatedBy)
    {
        CurrentBalance += delta;
        SetUpdated(updatedBy);
    }
}
